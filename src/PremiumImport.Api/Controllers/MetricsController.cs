using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremiumImport.Api.Contracts;
using PremiumImport.Api.Data;
using PremiumImport.Api.Services;

namespace PremiumImport.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class MetricsController(AppDbContext db, ExcelImportService importService) : ControllerBase
{
    [HttpGet("premium-methods/{premiumMethodId:int}/metrics")]
    public async Task<ActionResult<IReadOnlyList<MetricDto>>> GetMetrics(
        int premiumMethodId,
        CancellationToken cancellationToken)
    {
        var metrics = await db.Metrics.AsNoTracking()
            .Where(x => x.PremiumMethodId == premiumMethodId)
            .OrderBy(x => x.Name)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
        return Ok(metrics);
    }

    [HttpPost("premium-methods/{premiumMethodId:int}/metrics")]
    public async Task<ActionResult<MetricDto>> CreateMetric(
        int premiumMethodId,
        CreateMetricRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.SourceType) ||
            string.IsNullOrWhiteSpace(request.SourceName))
            return BadRequest("Metric name and source details are required.");
        if (!await db.PremiumMethods.AnyAsync(x => x.Id == premiumMethodId, cancellationToken))
            return NotFound("Premium method was not found.");
        if (await db.Metrics.AnyAsync(x => x.PremiumMethodId == premiumMethodId &&
            x.Name == request.Name.Trim(), cancellationToken))
            return Conflict("A metric with this name already exists for the premium method.");

        var metric = new Metric
        {
            PremiumMethodId = premiumMethodId,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            SourceType = request.SourceType.Trim(),
            SourceName = request.SourceName.Trim(),
            IngestionFrequency = request.IngestionFrequency.Trim()
        };
        db.Metrics.Add(metric);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetFields), new { metricId = metric.Id }, ToDto(metric));
    }

    [HttpGet("metrics/{metricId:int}/fields")]
    public async Task<ActionResult<IReadOnlyList<MetricFieldDto>>> GetFields(
        int metricId,
        CancellationToken cancellationToken)
    {
        if (!await db.Metrics.AnyAsync(x => x.Id == metricId, cancellationToken))
            return NotFound("Metric was not found.");

        var fields = await db.MetricFields.AsNoTracking()
            .Where(x => x.MetricId == metricId)
            .OrderBy(x => x.Id)
            .Select(x => new MetricFieldDto(
                x.Id, x.Key, x.DisplayName, x.DataType, x.IsRelevant,
                x.IsFilterable, x.IsSortable,
                x.SchemaColumns.OrderByDescending(c => c.SchemaVersion.VersionNumber)
                    .Select(c => c.SourceHeader).FirstOrDefault()))
            .ToListAsync(cancellationToken);
        return Ok(fields);
    }

    [HttpPut("metrics/{metricId:int}/fields/{fieldId:int}")]
    public async Task<IActionResult> UpdateField(
        int metricId,
        int fieldId,
        UpdateMetricFieldRequest request,
        CancellationToken cancellationToken)
    {
        var field = await db.MetricFields
            .SingleOrDefaultAsync(x => x.Id == fieldId && x.MetricId == metricId, cancellationToken);
        if (field is null)
            return NotFound("Field was not found for this metric.");
        if (string.IsNullOrWhiteSpace(request.DisplayName) ||
            request.DataType is not ("text" or "number" or "date" or "boolean"))
            return BadRequest("Field name and a supported data type (text, number, date, boolean) are required.");

        field.DisplayName = request.DisplayName.Trim();
        field.DataType = request.DataType;
        field.IsRelevant = request.IsRelevant;
        field.IsFilterable = request.IsFilterable;
        field.IsSortable = request.IsSortable;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("metrics/{metricId:int}/imports")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<ImportResultDto>> Import(
        int metricId,
        IFormFile file,
        [FromForm] int reportingYear,
        [FromForm] string reportingPeriod,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return BadRequest("The uploaded Excel file is empty.");
        if (reportingYear is < 1900 or > 2200)
            return BadRequest("Reporting year must be between 1900 and 2200.");
        if (string.IsNullOrWhiteSpace(reportingPeriod))
            return BadRequest("Reporting period is required.");
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) &&
            !Path.GetExtension(file.FileName).Equals(".xls", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .xlsx and .xls files are supported.");

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await importService.ImportAsync(
                metricId, stream, file.FileName, reportingYear, reportingPeriod, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (FormatException ex)
        {
            return BadRequest($"The workbook could not be read: {ex.Message}");
        }
    }

    [HttpGet("metrics/{metricId:int}/imports")]
    public async Task<ActionResult<IReadOnlyList<ImportBatchDto>>> GetImports(
        int metricId,
        CancellationToken cancellationToken)
    {
        if (!await db.Metrics.AnyAsync(x => x.Id == metricId, cancellationToken))
            return NotFound("Metric was not found.");

        var batches = await db.ImportBatches.AsNoTracking()
            .Where(x => x.MetricId == metricId)
            .OrderByDescending(x => x.StartedAtUtc)
            .Select(x => new ImportBatchDto(
                x.Id, x.OriginalFileName, x.ReportingYear, x.ReportingPeriod,
                x.StartedAtUtc, x.CompletedAtUtc, x.Status, x.RowCount,
                x.SchemaVersion.VersionNumber, x.ErrorMessage))
            .ToListAsync(cancellationToken);
        return Ok(batches);
    }

    [HttpGet("metrics/{metricId:int}/rows")]
    public async Task<ActionResult<PagedRowsDto>> GetRows(
        int metricId,
        [FromQuery] long? batchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool descending = false,
        [FromQuery] Dictionary<string, string>? filters = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 200)
            return BadRequest("Page must be positive and pageSize must be between 1 and 200.");

        var metricFields = await db.MetricFields.AsNoTracking()
            .Where(x => x.MetricId == metricId)
            .ToListAsync(cancellationToken);
        if (metricFields.Count == 0 && !await db.Metrics.AnyAsync(x => x.Id == metricId, cancellationToken))
            return NotFound("Metric was not found.");

        var batches = db.ImportBatches.AsNoTracking()
            .Where(x => x.MetricId == metricId && x.Status == "Completed");
        if (batchId.HasValue)
        {
            if (!await batches.AnyAsync(x => x.Id == batchId.Value, cancellationToken))
                return NotFound("Completed import batch was not found for this metric.");
            batches = batches.Where(x => x.Id == batchId.Value);
        }

        var rows = db.ImportedRows.AsNoTracking()
            .Where(row => batches.Any(batch => batch.Id == row.ImportBatchId));

        foreach (var filter in filters ?? new Dictionary<string, string>())
        {
            var field = metricFields.SingleOrDefault(x => x.Key == filter.Key);
            if (field is null || !field.IsFilterable)
                return BadRequest($"Field '{filter.Key}' cannot be filtered.");
            if (!ApplyFilter(ref rows, field, filter.Value))
                return BadRequest($"Invalid filter value for field '{field.DisplayName}'.");
        }

        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            var field = metricFields.SingleOrDefault(x => x.Key == sortBy);
            if (field is null || !field.IsSortable)
                return BadRequest($"Field '{sortBy}' cannot be sorted.");
            rows = SortRows(rows, field, descending);
        }
        else
        {
            rows = descending
                ? rows.OrderByDescending(x => x.Id)
                : rows.OrderBy(x => x.Id);
        }

        var totalCount = await rows.CountAsync(cancellationToken);
        var pageRows = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(x => x.Values)
                .ThenInclude(x => x.MetricField)
            .ToListAsync(cancellationToken);
        var items = pageRows.Select(row => new ImportedRowDto(
            row.Id,
            row.SourceRowNumber,
            row.Values.ToDictionary(
                value => value.MetricField.Key,
                GetDisplayValue,
                StringComparer.OrdinalIgnoreCase))).ToList();
        return Ok(new PagedRowsDto(items, page, pageSize, totalCount));
    }

    private static MetricDto ToDto(Metric metric) =>
        new(metric.Id, metric.PremiumMethodId, metric.Name, metric.Description,
            metric.SourceType, metric.SourceName, metric.IngestionFrequency);

    private static object? GetDisplayValue(ImportedValue value) =>
        value.NumberValue ?? (object?)value.DateValue ?? value.BooleanValue ?? value.TextValue ?? value.RawValue;

    private static bool ApplyFilter(
        ref IQueryable<ImportedRow> rows,
        MetricField field,
        string filter)
    {
        var operation = filter.StartsWith(">=", StringComparison.Ordinal) ||
            filter.StartsWith("<=", StringComparison.Ordinal)
            ? filter[..2]
            : filter.StartsWith('>') || filter.StartsWith('<') || filter.StartsWith('=')
                ? filter[..1]
                : field.DataType == "text" ? "~" : "=";
        var operand = filter[operation == "~" ? 0 : operation.Length..].Trim();
        switch (field.DataType)
        {
            case "number":
                if (!decimal.TryParse(operand, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    return false;
                rows = rows.Where(row => row.Values.Any(value =>
                    value.MetricFieldId == field.Id && value.NumberValue.HasValue &&
                    (operation switch
                    {
                        ">" => value.NumberValue > number,
                        ">=" => value.NumberValue >= number,
                        "<" => value.NumberValue < number,
                        "<=" => value.NumberValue <= number,
                        _ => value.NumberValue == number
                    })));
                return true;
            case "date":
                if (!DateTime.TryParse(operand, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    return false;
                rows = rows.Where(row => row.Values.Any(value =>
                    value.MetricFieldId == field.Id && value.DateValue.HasValue &&
                    (operation switch
                    {
                        ">" => value.DateValue > date,
                        ">=" => value.DateValue >= date,
                        "<" => value.DateValue < date,
                        "<=" => value.DateValue <= date,
                        _ => value.DateValue == date
                    })));
                return true;
            case "boolean":
                if (!bool.TryParse(operand, out var boolean))
                    return false;
                rows = rows.Where(row => row.Values.Any(value =>
                    value.MetricFieldId == field.Id && value.BooleanValue == boolean));
                return true;
            default:
                rows = rows.Where(row => row.Values.Any(value =>
                    value.MetricFieldId == field.Id && value.TextValue != null &&
                    (operation == "~"
                        ? value.TextValue.Contains(operand)
                        : value.TextValue == operand)));
                return true;
        }
    }

    private static IQueryable<ImportedRow> SortRows(
        IQueryable<ImportedRow> rows,
        MetricField field,
        bool descending) =>
        (field.DataType switch
        {
            "number" => descending
                ? rows.OrderByDescending(row => row.Values.Where(value => value.MetricFieldId == field.Id).Select(value => value.NumberValue).FirstOrDefault())
                : rows.OrderBy(row => row.Values.Where(value => value.MetricFieldId == field.Id).Select(value => value.NumberValue).FirstOrDefault()),
            "date" => descending
                ? rows.OrderByDescending(row => row.Values.Where(value => value.MetricFieldId == field.Id).Select(value => value.DateValue).FirstOrDefault())
                : rows.OrderBy(row => row.Values.Where(value => value.MetricFieldId == field.Id).Select(value => value.DateValue).FirstOrDefault()),
            "boolean" => descending
                ? rows.OrderByDescending(row => row.Values.Where(value => value.MetricFieldId == field.Id).Select(value => value.BooleanValue).FirstOrDefault())
                : rows.OrderBy(row => row.Values.Where(value => value.MetricFieldId == field.Id).Select(value => value.BooleanValue).FirstOrDefault()),
            _ => descending
                ? rows.OrderByDescending(row => row.Values.Where(value => value.MetricFieldId == field.Id).Select(value => value.TextValue).FirstOrDefault())
                : rows.OrderBy(row => row.Values.Where(value => value.MetricFieldId == field.Id).Select(value => value.TextValue).FirstOrDefault())
        });
}
