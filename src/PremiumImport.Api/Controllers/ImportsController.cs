using Microsoft.AspNetCore.Mvc;
using PremiumImport.Api.Contracts;
using PremiumImport.Api.Services.Import;

namespace PremiumImport.Api.Controllers;

[ApiController]
[Route("api/metrics/{metricId:int}/imports")]
public sealed class ImportsController(IImportService service) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    public Task<ImportResultDto> Upload(int metricId, [FromForm] ImportFileRequest request, CancellationToken ct) =>
        service.ImportAsync(metricId, request, ct);

    [HttpGet]
    public Task<IReadOnlyList<ImportBatchDto>> GetHistory(int metricId, CancellationToken ct) =>
        service.GetHistoryAsync(metricId, ct);
}
