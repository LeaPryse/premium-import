using Microsoft.AspNetCore.Mvc;
using PremiumImport.Api.Contracts;
using PremiumImport.Api.Services;

namespace PremiumImport.Api.Controllers;

[ApiController]
[Route("api/premium-methods")]
public sealed class PremiumMethodsController(IPremiumMethodService service) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PremiumMethodDto>> GetAll(CancellationToken ct) => service.GetAllAsync(ct);

    [HttpGet("{id:int}")]
    public Task<PremiumMethodDto> Get(int id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<PremiumMethodDto>> Create(PremiumMethodRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public Task<PremiumMethodDto> Update(int id, PremiumMethodRequest request, CancellationToken ct) =>
        service.UpdateAsync(id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
