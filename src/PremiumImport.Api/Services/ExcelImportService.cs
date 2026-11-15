using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using PremiumImport.Api.Contracts;
using PremiumImport.Api.Data;

namespace PremiumImport.Api.Services;

public sealed class ExcelImportService(AppDbContext db, ILogger<ExcelImportService> logger)
{
    private const int ImportChunkSize = 250;

    public async Task<ImportResultDto> ImportAsync(
        int metricId,
        Stream file,
        string fileName,
        int reportingYear,
        string reportingPeriod,
        CancellationToken cancellationToken)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(file);
        if (!await db.Metrics.AnyAsync(x => x.Id == metricId, cancellationToken))
            throw new KeyNotFoundException("Metric was not found.");

        if (!reader.Read())
            throw new InvalidDataException("The workbook is empty.");

        var headers = Enumerable.Range(0, reader.FieldCount)
            .Select(i => Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)?.Trim() ?? "")
            .ToArray();
        if (headers.Length == 0 || headers.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Every column must have a non-empty header.");
        if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw new InvalidDataException("Column headers must be unique (case-insensitive).");

        var previewRows = new List<object?[]>();
        while (previewRows.Count < 100 && reader.Read())
            previewRows.Add(ReadCurrentRow(reader, headers.Length));

        var fingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(headers))));
        var schema = await db.MetricSchemaVersions
            .Include(x => x.Columns).ThenInclude(x => x.MetricField)
            .SingleOrDefaultAsync(x => x.MetricId == metricId && x.Fingerprint == fingerprint, cancellationToken);
        var newSchema = schema is null;

        if (schema is null)
        {
            schema = await CreateSchemaAsync(metricId, headers, fingerprint, previewRows, cancellationToken);
        }

        var fieldByOrdinal = schema.Columns
            .OrderBy(x => x.Ordinal)
            .Select(x => x.MetricField)
            .ToArray();
        if (fieldByOrdinal.Length != headers.Length)
            throw new InvalidOperationException("The saved schema does not match the workbook columns.");

        var batch = new ImportBatch
        {
            MetricId = metricId,
            SchemaVersionId = schema.Id,
            OriginalFileName = Path.GetFileName(fileName),
            ReportingYear = reportingYear,
            ReportingPeriod = reportingPeriod.Trim(),
            StartedAtUtc = DateTime.UtcNow,
            Status = "Processing"
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var sourceRowNumber = 1;
            var rowCount = 0;
            var chunk = new List<ImportedRow>(ImportChunkSize);

            foreach (var values in previewRows)
            {
                sourceRowNumber++;
                if (TryCreateRow(values, sourceRowNumber, fieldByOrdinal, batch.Id, out var row))
                    chunk.Add(row);
                if (chunk.Count == ImportChunkSize)
                {
                    await SaveChunkAsync(chunk, cancellationToken);
                    rowCount += chunk.Count;
                    chunk.Clear();
                }
            }

            while (reader.Read())
            {
                sourceRowNumber++;
                var values = ReadCurrentRow(reader, headers.Length);
                if (TryCreateRow(values, sourceRowNumber, fieldByOrdinal, batch.Id, out var row))
                    chunk.Add(row);
                if (chunk.Count == ImportChunkSize)
                {
                    await SaveChunkAsync(chunk, cancellationToken);
                    rowCount += chunk.Count;
                    chunk.Clear();
                }
            }

            if (chunk.Count > 0)
            {
                await SaveChunkAsync(chunk, cancellationToken);
                rowCount += chunk.Count;
            }

            var completedBatch = await db.ImportBatches.SingleAsync(x => x.Id == batch.Id, cancellationToken);
            completedBatch.Status = "Completed";
            completedBatch.RowCount = rowCount;
            completedBatch.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ImportResultDto(batch.Id, rowCount, schema.VersionNumber, newSchema);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var failedBatch = await db.ImportBatches.SingleAsync(x => x.Id == batch.Id, cancellationToken);
            failedBatch.Status = "Failed";
            failedBatch.CompletedAtUtc = DateTime.UtcNow;
            failedBatch.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogError(ex, "Import batch {BatchId} for metric {MetricId} failed", batch.Id, metricId);
            throw;
        }
    }

    private async Task<MetricSchemaVersion> CreateSchemaAsync(
        int metricId,
        string[] headers,
        string fingerprint,
        IReadOnlyList<object?[]> previewRows,
        CancellationToken cancellationToken)
    {
        var existingFields = await db.MetricFields
            .Where(x => x.MetricId == metricId)
            .ToListAsync(cancellationToken);
        var versionNumber = (await db.MetricSchemaVersions
            .Where(x => x.MetricId == metricId)
            .Select(x => (int?)x.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0) + 1;
        var schema = new MetricSchemaVersion
        {
            MetricId = metricId,
            Fingerprint = fingerprint,
            VersionNumber = versionNumber,
            CreatedAtUtc = DateTime.UtcNow
        };

        for (var ordinal = 0; ordinal < headers.Length; ordinal++)
        {
            var header = headers[ordinal];
            var field = existingFields.FirstOrDefault(x =>
                string.Equals(x.DisplayName, header, StringComparison.OrdinalIgnoreCase));
            if (field is null)
            {
                var key = CreateUniqueKey(header, existingFields);
                field = new MetricField
                {
                    MetricId = metricId,
                    Key = key,
                    DisplayName = header,
                    DataType = InferDataType(previewRows, ordinal),
                    IsRelevant = true,
                    IsFilterable = true,
                    IsSortable = true
                };
                existingFields.Add(field);
                db.MetricFields.Add(field);
            }

            schema.Columns.Add(new MetricSchemaColumn
            {
                MetricField = field,
                SourceHeader = header,
                Ordinal = ordinal
            });
        }

        db.MetricSchemaVersions.Add(schema);
        await db.SaveChangesAsync(cancellationToken);
        return schema;
    }

    private static string CreateUniqueKey(string header, IReadOnlyCollection<MetricField> existingFields)
    {
        var key = new string(header.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());
        key = string.Join('-', key.Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(key))
            key = "field";

        var candidate = key;
        var suffix = 2;
        while (existingFields.Any(x => x.Key == candidate))
            candidate = $"{key}-{suffix++}";
        return candidate;
    }

    private static string InferDataType(IReadOnlyList<object?[]> rows, int ordinal)
    {
        var values = rows.Select(row => row[ordinal]).Where(value => value is not null).ToArray();
        if (values.Length == 0)
            return "text";
        if (values.All(value => value is DateTime))
            return "date";
        if (values.All(value => value is bool))
            return "boolean";
        if (values.All(IsNumeric))
            return "number";
        return "text";
    }

    private static bool IsNumeric(object? value) =>
        value is byte or short or int or long or float or double or decimal;

    private static object?[] ReadCurrentRow(IExcelDataReader reader, int columnCount) =>
        Enumerable.Range(0, columnCount).Select(reader.GetValue).ToArray();

    private static bool TryCreateRow(
        object?[] sourceValues,
        int sourceRowNumber,
        IReadOnlyList<MetricField> fields,
        long batchId,
        out ImportedRow row)
    {
        row = new ImportedRow { ImportBatchId = batchId, SourceRowNumber = sourceRowNumber };
        var containsData = false;
        for (var ordinal = 0; ordinal < fields.Count; ordinal++)
        {
            var raw = sourceValues[ordinal];
            if (raw is null || string.IsNullOrWhiteSpace(Convert.ToString(raw, CultureInfo.InvariantCulture)))
                continue;

            containsData = true;
            var rawText = Convert.ToString(raw, CultureInfo.InvariantCulture);
            var value = new ImportedValue
            {
                MetricFieldId = fields[ordinal].Id,
                RawValue = rawText
            };

            switch (fields[ordinal].DataType)
            {
                case "number":
                    if (TryParseDecimal(raw, out var number))
                        value.NumberValue = number;
                    else
                        value.TextValue = rawText;
                    break;
                case "date":
                    if (raw is DateTime date)
                        value.DateValue = date;
                    else if (DateTime.TryParse(rawText, CultureInfo.InvariantCulture, out date))
                        value.DateValue = date;
                    else
                        value.TextValue = rawText;
                    break;
                case "boolean":
                    if (raw is bool boolean)
                        value.BooleanValue = boolean;
                    else if (bool.TryParse(rawText, out boolean))
                        value.BooleanValue = boolean;
                    else
                        value.TextValue = rawText;
                    break;
                default:
                    value.TextValue = rawText;
                    break;
            }

            row.Values.Add(value);
        }

        return containsData;
    }

    private static bool TryParseDecimal(object raw, out decimal result)
    {
        if (raw is IConvertible && IsNumeric(raw))
        {
            result = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
            return true;
        }

        var text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out result)
            || decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
    }

    private async Task SaveChunkAsync(List<ImportedRow> rows, CancellationToken cancellationToken)
    {
        db.ImportedRows.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }
}
