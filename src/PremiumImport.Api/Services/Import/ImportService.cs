using Microsoft.EntityFrameworkCore;
using PremiumImport.Api.Contracts;
using PremiumImport.Api.Data;
using PremiumImport.Api.Domain;

namespace PremiumImport.Api.Services.Import;

public interface IImportService
{
    Task<ImportResultDto> ImportAsync(int metricId, ImportFileRequest request, CancellationToken ct);
    Task<IReadOnlyList<ImportBatchDto>> GetHistoryAsync(int metricId, CancellationToken ct);
}

/// <summary>
/// Orchestrates an upload: read file -> resolve structure -> create batch -> insert rows in chunks.
/// Rows are inserted inside a transaction; on failure the batch stays in history as Failed with the error.
/// </summary>
public sealed class ImportService(
    AppDbContext db,
    IEnumerable<ITabularFileReader> readers,
    SchemaResolver schemaResolver,
    FieldValueConverters converters,
    ILogger<ImportService> logger) : IImportService
{
    private const int SampleSize = 100;
    private const int ChunkSize = 1000;

    public async Task<ImportResultDto> ImportAsync(int metricId, ImportFileRequest request, CancellationToken ct)
    {
        var metric = await db.Metrics.AsNoTracking().SingleOrDefaultAsync(x => x.Id == metricId, ct)
            ?? throw new NotFoundException($"Metric {metricId} was not found.");
        if (metric.SourceType != SourceType.Excel)
            throw new BadInputException("File upload is only supported for metrics with an Excel source.");
        if (!ReportingPeriod.IsValid(request.ReportingYear, request.PeriodType, request.PeriodNumber))
            throw new BadInputException($"Period number must be between 1 and {ReportingPeriod.MaxNumber(request.PeriodType)} for {request.PeriodType}.");
        if (request.File.Length == 0)
            throw new BadInputException("The uploaded file is empty.");

        var fileName = Path.GetFileName(request.File.FileName);
        var reader = readers.FirstOrDefault(r => r.CanRead(fileName))
            ?? throw new BadInputException("Unsupported file type. Use .xlsx, .xls or .csv.");

        await using var stream = request.File.OpenReadStream();
        using var data = OpenFile(reader, stream);
        using var rows = data.ReadRows().GetEnumerator();

        // Buffer a sample to infer data types of new columns, then continue streaming.
        var sample = new List<object?[]>(SampleSize);
        while (sample.Count < SampleSize && rows.MoveNext())
            sample.Add(rows.Current);

        var schema = await schemaResolver.ResolveAsync(metricId, data.Headers, sample, ct);

        var batch = new ImportBatch
        {
            MetricId = metricId,
            SchemaVersionId = schema.Version.Id,
            OriginalFileName = fileName,
            ReportingYear = request.ReportingYear,
            PeriodType = request.PeriodType,
            PeriodNumber = request.PeriodNumber,
            StartedAtUtc = DateTime.UtcNow
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);

        try
        {
            var rowCount = await InsertRowsAsync(batch.Id, schema.FieldsByOrdinal, sample.Concat(Remaining(rows)), ct);
            return new ImportResultDto(batch.Id, rowCount, schema.Version.VersionNumber, schema.IsNew, schema.NewFields);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Import batch {BatchId} for metric {MetricId} failed", batch.Id, metricId);
            db.ChangeTracker.Clear();
            var failed = await db.ImportBatches.SingleAsync(x => x.Id == batch.Id, CancellationToken.None);
            failed.MarkFailed(ex.Message);
            await db.SaveChangesAsync(CancellationToken.None);
            throw new BadInputException($"Import failed: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<ImportBatchDto>> GetHistoryAsync(int metricId, CancellationToken ct)
    {
        if (!await db.Metrics.AnyAsync(x => x.Id == metricId, ct))
            throw new NotFoundException($"Metric {metricId} was not found.");

        var completed = db.ImportBatches.Where(x => x.MetricId == metricId && x.Status == ImportStatus.Completed);
        return await db.ImportBatches.AsNoTracking()
            .Where(x => x.MetricId == metricId)
            .OrderByDescending(x => x.Id)
            .Select(x => new ImportBatchDto(
                x.Id, x.OriginalFileName, x.ReportingYear, x.PeriodType, x.PeriodNumber,
                x.StartedAtUtc, x.CompletedAtUtc, x.Status, x.RowCount,
                x.SchemaVersion.VersionNumber, x.ErrorMessage,
                completed.Any(o => o.ReportingYear == x.ReportingYear && o.PeriodType == x.PeriodType &&
                                   o.PeriodNumber == x.PeriodNumber && o.Id > x.Id)))
            .ToListAsync(ct);
    }

    private async Task<int> InsertRowsAsync(
        long batchId, IReadOnlyList<MetricField> fields, IEnumerable<object?[]> source, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            var rowCount = 0;
            var sourceRowNumber = 1; // row 1 = headers
            var chunk = new List<ImportedRow>(ChunkSize);

            foreach (var cells in source)
            {
                sourceRowNumber++;
                var row = CreateRow(batchId, sourceRowNumber, cells, fields);
                if (row is null)
                    continue;

                chunk.Add(row);
                if (chunk.Count == ChunkSize)
                    rowCount += await FlushAsync(chunk, ct);
            }
            rowCount += await FlushAsync(chunk, ct);

            var batch = await db.ImportBatches.SingleAsync(x => x.Id == batchId, ct);
            batch.MarkCompleted(rowCount);
            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return rowCount;
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    private ImportedRow? CreateRow(long batchId, int sourceRowNumber, object?[] cells, IReadOnlyList<MetricField> fields)
    {
        var row = new ImportedRow { ImportBatchId = batchId, SourceRowNumber = sourceRowNumber };
        for (var i = 0; i < fields.Count; i++)
        {
            var raw = cells[i];
            if (!FieldValueConverters.IsEmpty(raw))
                row.Values.Add(converters.Create(raw!, fields[i]));
        }
        return row.Values.Count == 0 ? null : row; // skip completely empty lines
    }

    private async Task<int> FlushAsync(List<ImportedRow> chunk, CancellationToken ct)
    {
        if (chunk.Count == 0)
            return 0;
        db.ImportedRows.AddRange(chunk);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        var count = chunk.Count;
        chunk.Clear();
        return count;
    }

    private static ITabularData OpenFile(ITabularFileReader reader, Stream stream)
    {
        try
        {
            return reader.Open(stream);
        }
        catch (Exception ex) when (ex is not AppException)
        {
            throw new BadInputException($"The file could not be read: {ex.Message}");
        }
    }

    private static IEnumerable<object?[]> Remaining(IEnumerator<object?[]> rows)
    {
        while (rows.MoveNext())
            yield return rows.Current;
    }
}
