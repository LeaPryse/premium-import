using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PremiumImport.Api.Contracts;
using PremiumImport.Api.Data;

namespace PremiumImport.Api.Controllers;

[ApiController]
[Route("api/premium-methods")]
public sealed class PremiumMethodsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PremiumMethodDto>>> GetAll(CancellationToken cancellationToken)
    {
        var methods = await db.PremiumMethods
            .AsNoTracking()
            .OrderBy(x => x.Number)
            .Select(x => new PremiumMethodDto(
                x.Id, x.Number, x.Description, x.PremiumPercent, x.CalculationPeriod, x.Metrics.Count))
            .ToListAsync(cancellationToken);
        return Ok(methods);
    }

    [HttpPost]
    public async Task<ActionResult<PremiumMethodDto>> Create(
        CreatePremiumMethodRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Number) ||
            string.IsNullOrWhiteSpace(request.Description) ||
            string.IsNullOrWhiteSpace(request.CalculationPeriod) ||
            request.PremiumPercent is < 0 or > 100)
            return BadRequest("Number, description, period and a premium percentage between 0 and 100 are required.");

        var number = request.Number.Trim();
        if (await db.PremiumMethods.AnyAsync(x => x.Number == number, cancellationToken))
            return Conflict($"Premium method '{number}' already exists.");

        var method = new PremiumMethod
        {
            Number = number,
            Description = request.Description.Trim(),
            PremiumPercent = request.PremiumPercent,
            CalculationPeriod = request.CalculationPeriod.Trim()
        };
        db.PremiumMethods.Add(method);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = method.Id },
            new PremiumMethodDto(method.Id, method.Number, method.Description,
                method.PremiumPercent, method.CalculationPeriod, 0));
    }
}
