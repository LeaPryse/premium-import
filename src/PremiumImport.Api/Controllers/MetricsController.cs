using Microsoft.AspNetCore.Mvc;
using PremiumImport.Api.Contracts;
using PremiumImport.Api.Services;

namespace PremiumImport.Api.Controllers;

/// <summary>Metric definitions, their field dictionary and the history of file structures.</summary>
[ApiController]
[Route("api")]
public sealed class MetricsController(IMetricService service) : ControllerBase
{
    [HttpGet("premium-methods/{premiumMethodId:int}/metrics")]
    public Task<IReadOnlyList<MetricDto>> GetByMethod(int premiumMethodId, CancellationToken ct) =>
        service.GetByMethodAsync(premiumMethodId, ct);

    [HttpPost("premium-methods/{premiumMethodId:int}/metrics")]
    public async Task<ActionResult<MetricDto>> Create(int premiumMethodId, MetricRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(premiumMethodId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("metrics/{id:int}")]
    public Task<MetricDto> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPut("metrics/{id:int}")]
    public Task<MetricDto> Update(int id, MetricRequest request, CancellationToken ct) =>
        service.UpdateAsync(id, request, ct);

    [HttpDelete("metrics/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("metrics/{id:int}/fields")]
    public Task<IReadOnlyList<MetricFieldDto>> GetFields(int id, CancellationToken ct) =>
        service.GetFieldsAsync(id, ct);

    [HttpPut("metrics/{id:int}/fields/{fieldId:int}")]
    public Task<MetricFieldDto> UpdateField(int id, int fieldId, UpdateMetricFieldRequest request, CancellationToken ct) =>
        service.UpdateFieldAsync(id, fieldId, request, ct);

    /// <summary>Map an additional header name to an existing field (e.g. the column was renamed in the source file).</summary>
    [HttpPost("metrics/{id:int}/fields/{fieldId:int}/aliases")]
    public Task<MetricFieldDto> AddAlias(int id, int fieldId, AddAliasRequest request, CancellationToken ct) =>
        service.AddAliasAsync(id, fieldId, request.Alias, ct);

    [HttpGet("metrics/{id:int}/schema-versions")]
    public Task<IReadOnlyList<SchemaVersionDto>> GetSchemaVersions(int id, CancellationToken ct) =>
        service.GetSchemaVersionsAsync(id, ct);
}
