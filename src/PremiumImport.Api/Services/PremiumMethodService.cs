using Microsoft.EntityFrameworkCore;
using PremiumImport.Api.Contracts;
using PremiumImport.Api.Data;
using PremiumImport.Api.Domain;

namespace PremiumImport.Api.Services;

public interface IPremiumMethodService
{
    Task<IReadOnlyList<PremiumMethodDto>> GetAllAsync(CancellationToken ct);
    Task<PremiumMethodDto> GetAsync(int id, CancellationToken ct);
    Task<PremiumMethodDto> CreateAsync(PremiumMethodRequest request, CancellationToken ct);
    Task<PremiumMethodDto> UpdateAsync(int id, PremiumMethodRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public sealed class PremiumMethodService(AppDbContext db) : IPremiumMethodService
{
    public async Task<IReadOnlyList<PremiumMethodDto>> GetAllAsync(CancellationToken ct) =>
        await db.PremiumMethods.AsNoTracking()
            .OrderBy(x => x.Number)
            .Select(x => new PremiumMethodDto(
                x.Id, x.Number, x.Description, x.PremiumPercent, x.CalculationPeriod, x.Metrics.Count))
            .ToListAsync(ct);

    public async Task<PremiumMethodDto> GetAsync(int id, CancellationToken ct) =>
        await db.PremiumMethods.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PremiumMethodDto(
                x.Id, x.Number, x.Description, x.PremiumPercent, x.CalculationPeriod, x.Metrics.Count))
            .SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException($"Premium method {id} was not found.");

    public async Task<PremiumMethodDto> CreateAsync(PremiumMethodRequest request, CancellationToken ct)
    {
        var number = request.Number.Trim();
        await EnsureNumberIsFreeAsync(number, null, ct);

        var method = new PremiumMethod();
        Apply(method, request);
        db.PremiumMethods.Add(method);
        await db.SaveChangesAsync(ct);
        return await GetAsync(method.Id, ct);
    }

    public async Task<PremiumMethodDto> UpdateAsync(int id, PremiumMethodRequest request, CancellationToken ct)
    {
        var method = await db.PremiumMethods.FindAsync([id], ct)
            ?? throw new NotFoundException($"Premium method {id} was not found.");
        await EnsureNumberIsFreeAsync(request.Number.Trim(), id, ct);

        Apply(method, request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var method = await db.PremiumMethods.FindAsync([id], ct)
            ?? throw new NotFoundException($"Premium method {id} was not found.");
        if (await db.Metrics.AnyAsync(x => x.PremiumMethodId == id, ct))
            throw new ConflictException("Cannot delete a premium method that still has metrics.");

        db.PremiumMethods.Remove(method);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureNumberIsFreeAsync(string number, int? exceptId, CancellationToken ct)
    {
        if (await db.PremiumMethods.AnyAsync(x => x.Number == number && x.Id != exceptId, ct))
            throw new ConflictException($"Premium method '{number}' already exists.");
    }

    private static void Apply(PremiumMethod method, PremiumMethodRequest request)
    {
        method.Number = request.Number.Trim();
        method.Description = request.Description.Trim();
        method.PremiumPercent = request.PremiumPercent;
        method.CalculationPeriod = request.CalculationPeriod;
    }
}
