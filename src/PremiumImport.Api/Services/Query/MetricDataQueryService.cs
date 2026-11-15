using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PremiumImport.Api.Contracts;
using PremiumImport.Api.Data;
using PremiumImport.Api.Domain;

namespace PremiumImport.Api.Services.Query;

public interface IMetricDataQueryService
{
    Task<PagedResult<ImportedRowDto>> GetRowsAsync(int metricId, MetricRowsQuery query, CancellationToken ct);
    Task<FieldFilterOptionsDto> GetFilterOptionsAsync(int metricId, int fieldId, CancellationToken ct);
}

/// <summary>
/// Reads EAV data back as rows. Everything (filter, sort, paging) is executed in the database:
///   1. choose batches (one batch, or the latest completed batch per reporting period)
///   2. every filter is a semi-join on ImportedValues using the (MetricFieldId, typed value) index
///   3. sorting uses a lookup by the (ImportedRowId, MetricFieldId) primary key
///   4. only the requested page's values are loaded, and only for relevant fields
/// </summary>
public sealed class MetricDataQueryService(AppDbContext db) : IMetricDataQueryService
{
    private const int MaxDistinctValues = 50;

    public async Task<PagedResult<ImportedRowDto>> GetRowsAsync(int metricId, MetricRowsQuery query, CancellationToken ct)
    {
        var fields = await LoadFieldsAsync(metricId, ct);
        var batchIds = SelectBatches(metricId, query);
        var rows = db.ImportedRows.AsNoTracking().Where(r => batchIds.Contains(r.ImportBatchId));

        foreach (var (key, expression) in query.Filters ?? [])
        {
            if (string.IsNullOrWhiteSpace(expression))
                continue;
            var field = FindField(fields, key);
            if (!field.IsFilterable)
                throw new BadInputException($"Field '{field.DisplayName}' is not filterable.");
            var matching = db.ImportedValues
                .Where(ValuePredicate(field, FilterParser.Parse(expression, field.DataType)))
                .Select(v => v.ImportedRowId);
            rows = rows.Where(r => matching.Contains(r.Id));
        }

        rows = string.IsNullOrWhiteSpace(query.SortBy)
            ? rows.OrderByDescending(r => r.ImportBatchId).ThenBy(r => r.SourceRowNumber)
            : Sort(rows, FindField(fields, query.SortBy), query.Descending);

        var totalCount = await rows.CountAsync(ct);
        var page = await rows
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new { r.Id, r.ImportBatchId, r.SourceRowNumber })
            .ToListAsync(ct);

        var rowIds = page.Select(r => r.Id).ToList();
        var relevant = fields.Where(f => f.IsRelevant).ToDictionary(f => f.Id, f => f.Key);
        var relevantIds = relevant.Keys.ToList();
        var values = await db.ImportedValues.AsNoTracking()
            .Where(v => rowIds.Contains(v.ImportedRowId) && relevantIds.Contains(v.MetricFieldId))
            .ToListAsync(ct);
        var valuesByRow = values.ToLookup(v => v.ImportedRowId);

        var items = page.Select(r => new ImportedRowDto(
                r.Id, r.ImportBatchId, r.SourceRowNumber,
                valuesByRow[r.Id].ToDictionary(v => relevant[v.MetricFieldId], v => v.TypedValue)))
            .ToList();
        return new PagedResult<ImportedRowDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<FieldFilterOptionsDto> GetFilterOptionsAsync(int metricId, int fieldId, CancellationToken ct)
    {
        var field = await db.MetricFields.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == fieldId && x.MetricId == metricId, ct)
            ?? throw new NotFoundException($"Field {fieldId} was not found for metric {metricId}.");
        var values = db.ImportedValues.AsNoTracking().Where(v => v.MetricFieldId == fieldId);
        var operators = FilterParser.OperatorsFor(field.DataType);

        switch (field.DataType)
        {
            case DataType.Number:
                return new FieldFilterOptionsDto(field.Key, field.DataType, operators, null,
                    await values.MinAsync(v => v.NumberValue, ct), await values.MaxAsync(v => v.NumberValue, ct));
            case DataType.Date:
                return new FieldFilterOptionsDto(field.Key, field.DataType, operators, null,
                    await values.MinAsync(v => v.DateValue, ct), await values.MaxAsync(v => v.DateValue, ct));
            case DataType.Boolean:
                var booleans = await values.Where(v => v.BooleanValue != null)
                    .GroupBy(v => v.BooleanValue)
                    .Select(g => new ValueCountDto(g.Key, g.Count()))
                    .ToListAsync(ct);
                return new FieldFilterOptionsDto(field.Key, field.DataType, operators, booleans, null, null);
            default:
                // Most frequent values - lets the UI offer a dropdown for low-cardinality text (status, department...).
                var texts = await values.Where(v => v.TextValue != null)
                    .GroupBy(v => v.TextValue)
                    .Select(g => new { Value = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count).ThenBy(g => g.Value)
                    .Take(MaxDistinctValues)
                    .ToListAsync(ct);
                return new FieldFilterOptionsDto(field.Key, field.DataType, operators,
                    texts.Select(t => new ValueCountDto(t.Value, t.Count)).ToList(), null, null);
        }
    }

    private async Task<List<MetricField>> LoadFieldsAsync(int metricId, CancellationToken ct)
    {
        if (!await db.Metrics.AnyAsync(x => x.Id == metricId, ct))
            throw new NotFoundException($"Metric {metricId} was not found.");
        return await db.MetricFields.AsNoTracking().Where(x => x.MetricId == metricId).ToListAsync(ct);
    }

    private static MetricField FindField(IEnumerable<MetricField> fields, string key) =>
        fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? throw new BadInputException($"Unknown field '{key}'.");

    private IQueryable<long> SelectBatches(int metricId, MetricRowsQuery query)
    {
        var completed = db.ImportBatches.Where(b => b.MetricId == metricId && b.Status == ImportStatus.Completed);
        if (query.BatchId.HasValue)
            return completed.Where(b => b.Id == query.BatchId.Value).Select(b => b.Id);

        var batches = completed;
        if (query.Year.HasValue)
            batches = batches.Where(b => b.ReportingYear == query.Year.Value);
        if (query.PeriodType.HasValue)
            batches = batches.Where(b => b.PeriodType == query.PeriodType.Value);
        if (query.PeriodNumber.HasValue)
            batches = batches.Where(b => b.PeriodNumber == query.PeriodNumber.Value);

        // A re-upload for the same period replaces the previous one (which stays in history).
        if (!query.IncludeSuperseded)
            batches = batches.Where(b => !completed.Any(o =>
                o.ReportingYear == b.ReportingYear && o.PeriodType == b.PeriodType &&
                o.PeriodNumber == b.PeriodNumber && o.Id > b.Id));

        return batches.Select(b => b.Id);
    }

    private static Expression<Func<ImportedValue, bool>> ValuePredicate(MetricField field, FieldFilter filter)
    {
        var id = field.Id;
        switch (field.DataType)
        {
            case DataType.Number:
            {
                var a = (decimal)filter.Value;
                var b = filter.To as decimal? ?? 0;
                return filter.Operator switch
                {
                    FilterOperator.GreaterThan => v => v.MetricFieldId == id && v.NumberValue > a,
                    FilterOperator.GreaterOrEqual => v => v.MetricFieldId == id && v.NumberValue >= a,
                    FilterOperator.LessThan => v => v.MetricFieldId == id && v.NumberValue < a,
                    FilterOperator.LessOrEqual => v => v.MetricFieldId == id && v.NumberValue <= a,
                    FilterOperator.Between => v => v.MetricFieldId == id && v.NumberValue >= a && v.NumberValue <= b,
                    _ => v => v.MetricFieldId == id && v.NumberValue == a
                };
            }
            case DataType.Date:
            {
                // Dates are compared by whole days: "=2026-01-05" means [2026-01-05, 2026-01-06).
                var from = (DateTime)filter.Value;
                var nextDay = from.AddDays(1);
                var toNextDay = (filter.To as DateTime? ?? from).AddDays(1);
                return filter.Operator switch
                {
                    FilterOperator.GreaterThan => v => v.MetricFieldId == id && v.DateValue >= nextDay,
                    FilterOperator.GreaterOrEqual => v => v.MetricFieldId == id && v.DateValue >= from,
                    FilterOperator.LessThan => v => v.MetricFieldId == id && v.DateValue < from,
                    FilterOperator.LessOrEqual => v => v.MetricFieldId == id && v.DateValue < nextDay,
                    FilterOperator.Between => v => v.MetricFieldId == id && v.DateValue >= from && v.DateValue < toNextDay,
                    _ => v => v.MetricFieldId == id && v.DateValue >= from && v.DateValue < nextDay
                };
            }
            case DataType.Boolean:
            {
                var flag = (bool)filter.Value;
                return v => v.MetricFieldId == id && v.BooleanValue == flag;
            }
            default:
            {
                var text = (string)filter.Value;
                return filter.Operator == FilterOperator.Equals
                    ? v => v.MetricFieldId == id && v.TextValue == text
                    : v => v.MetricFieldId == id && v.TextValue!.Contains(text);
            }
        }
    }

    private IQueryable<ImportedRow> Sort(IQueryable<ImportedRow> rows, MetricField field, bool descending)
    {
        if (!field.IsSortable)
            throw new BadInputException($"Field '{field.DisplayName}' is not sortable.");

        var id = field.Id;
        var values = db.ImportedValues;
        IOrderedQueryable<ImportedRow> ordered = field.DataType switch
        {
            DataType.Number => OrderBy(rows, r => values.Where(v => v.ImportedRowId == r.Id && v.MetricFieldId == id).Select(v => v.NumberValue).FirstOrDefault(), descending),
            DataType.Date => OrderBy(rows, r => values.Where(v => v.ImportedRowId == r.Id && v.MetricFieldId == id).Select(v => v.DateValue).FirstOrDefault(), descending),
            DataType.Boolean => OrderBy(rows, r => values.Where(v => v.ImportedRowId == r.Id && v.MetricFieldId == id).Select(v => v.BooleanValue).FirstOrDefault(), descending),
            _ => OrderBy(rows, r => values.Where(v => v.ImportedRowId == r.Id && v.MetricFieldId == id).Select(v => v.TextValue).FirstOrDefault(), descending)
        };
        return ordered.ThenBy(r => r.Id); // stable order for paging
    }

    private static IOrderedQueryable<ImportedRow> OrderBy<TKey>(
        IQueryable<ImportedRow> rows, Expression<Func<ImportedRow, TKey>> key, bool descending) =>
        descending ? rows.OrderByDescending(key) : rows.OrderBy(key);
}
