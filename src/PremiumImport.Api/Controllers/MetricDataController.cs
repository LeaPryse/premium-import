using Microsoft.AspNetCore.Mvc;
using PremiumImport.Api.Contracts;
using PremiumImport.Api.Services.Query;

namespace PremiumImport.Api.Controllers;

/// <summary>Dynamic read side: imported rows and the filter options of each field.</summary>
[ApiController]
[Route("api/metrics/{metricId:int}")]
public sealed class MetricDataController(IMetricDataQueryService service) : ControllerBase
{
    /// <example>GET /api/metrics/1/rows?page=1&amp;pageSize=50&amp;sortBy=salary&amp;descending=true&amp;filters[department]=sales&amp;filters[salary]=1000..5000</example>
    [HttpGet("rows")]
    public Task<PagedResult<ImportedRowDto>> GetRows(int metricId, [FromQuery] MetricRowsQuery query, CancellationToken ct) =>
        service.GetRowsAsync(metricId, query, ct);

    [HttpGet("fields/{fieldId:int}/filter-options")]
    public Task<FieldFilterOptionsDto> GetFilterOptions(int metricId, int fieldId, CancellationToken ct) =>
        service.GetFilterOptionsAsync(metricId, fieldId, ct);
}
