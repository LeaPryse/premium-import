using Microsoft.EntityFrameworkCore;
using PremiumImport.Api.Contracts;
using PremiumImport.Api.Data;
using PremiumImport.Api.Domain;

namespace PremiumImport.Api.Services;

public interface IMetricService
{
    Task<IReadOnlyList<MetricDto>> GetByMethodAsync(int premiumMethodId, CancellationToken ct);
    Task<MetricDto> GetAsync(int id, CancellationToken ct);
    Task<MetricDto> CreateAsync(int premiumMethodId, MetricRequest request, CancellationToken ct);
    Task<MetricDto> UpdateAsync(int id, MetricRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<MetricFieldDto>> GetFieldsAsync(int metricId, CancellationToken ct);
    Task<MetricFieldDto> UpdateFieldAsync(int metricId, int fieldId, UpdateMetricFieldRequest request, CancellationToken ct);
    Task<MetricFieldDto> AddAliasAsync(int metricId, int fieldId, string alias, CancellationToken ct);
    Task<IReadOnlyList<SchemaVersionDto>> GetSchemaVersionsAsync(int metricId, CancellationToken ct);
}

public sealed class MetricService(AppDbContext db) : IMetricService
{
    public async Task<IReadOnlyList<MetricDto>> GetByMethodAsync(int premiumMethodId, CancellationToken ct)
    {
        if (!await db.PremiumMethods.AnyAsync(x => x.Id == premiumMethodId, ct))
            throw new NotFoundException($"Premium method {premiumMethodId} was not found.");

        return await Project(db.Metrics.Where(x => x.PremiumMethodId == premiumMethodId).OrderBy(x => x.Name))
            .ToListAsync(ct);
    }

    public async Task<MetricDto> GetAsync(int id, CancellationToken ct) =>
        await Project(db.Metrics.Where(x => x.Id == id)).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException($"Metric {id} was not found.");

    public async Task<MetricDto> CreateAsync(int premiumMethodId, MetricRequest request, CancellationToken ct)
    {
        if (!await db.PremiumMethods.AnyAsync(x => x.Id == premiumMethodId, ct))
            throw new NotFoundException($"Premium method {premiumMethodId} was not found.");
        await EnsureNameIsFreeAsync(premiumMethodId, request.Name.Trim(), null, ct);

        var metric = new Metric { PremiumMethodId = premiumMethodId };
        Apply(metric, request);
        db.Metrics.Add(metric);
        await db.SaveChangesAsync(ct);
        return await GetAsync(metric.Id, ct);
    }

    public async Task<MetricDto> UpdateAsync(int id, MetricRequest request, CancellationToken ct)
    {
        var metric = await db.Metrics.FindAsync([id], ct)
            ?? throw new NotFoundException($"Metric {id} was not found.");
        await EnsureNameIsFreeAsync(metric.PremiumMethodId, request.Name.Trim(), id, ct);

        Apply(metric, request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var metric = await db.Metrics.FindAsync([id], ct)
            ?? throw new NotFoundException($"Metric {id} was not found.");
        if (await db.ImportBatches.AnyAsync(x => x.MetricId == id, ct))
            throw new ConflictException("Cannot delete a metric that has import history.");

        db.Metrics.Remove(metric);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MetricFieldDto>> GetFieldsAsync(int metricId, CancellationToken ct)
    {
        await EnsureMetricExistsAsync(metricId, ct);
        return await ProjectFields(db.MetricFields.Where(x => x.MetricId == metricId))
            .ToListAsync(ct);
    }

    public async Task<MetricFieldDto> UpdateFieldAsync(
        int metricId, int fieldId, UpdateMetricFieldRequest request, CancellationToken ct)
    {
        var field = await FindFieldAsync(metricId, fieldId, ct);
        field.DisplayName = request.DisplayName.Trim();
        field.DataType = request.DataType;
        field.IsRelevant = request.IsRelevant;
        field.IsFilterable = request.IsFilterable;
        field.IsSortable = request.IsSortable;
        await db.SaveChangesAsync(ct);
        return await GetFieldAsync(fieldId, ct);
    }

    public async Task<MetricFieldDto> AddAliasAsync(int metricId, int fieldId, string alias, CancellationToken ct)
    {
        await FindFieldAsync(metricId, fieldId, ct);
        alias = alias.Trim();
        var takenBy = await db.MetricFields
            .Where(x => x.MetricId == metricId && x.Id != fieldId &&
                        (x.DisplayName == alias || x.Aliases.Any(a => a.Alias == alias)))
            .Select(x => x.DisplayName)
            .FirstOrDefaultAsync(ct);
        if (takenBy is not null)
            throw new ConflictException($"Header '{alias}' is already mapped to field '{takenBy}'.");

        if (!await db.MetricFieldAliases.AnyAsync(x => x.MetricFieldId == fieldId && x.Alias == alias, ct))
        {
            db.MetricFieldAliases.Add(new MetricFieldAlias { MetricFieldId = fieldId, Alias = alias });
            await db.SaveChangesAsync(ct);
        }
        return await GetFieldAsync(fieldId, ct);
    }

    public async Task<IReadOnlyList<SchemaVersionDto>> GetSchemaVersionsAsync(int metricId, CancellationToken ct)
    {
        await EnsureMetricExistsAsync(metricId, ct);
        return await db.MetricSchemaVersions.AsNoTracking()
            .Where(x => x.MetricId == metricId)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => new SchemaVersionDto(
                x.Id, x.VersionNumber, x.CreatedAtUtc, x.ImportBatches.Count,
                x.Columns.OrderBy(c => c.Ordinal)
                    .Select(c => new SchemaColumnDto(c.Ordinal, c.SourceHeader, c.MetricFieldId, c.MetricField.Key))
                    .ToList()))
            .ToListAsync(ct);
    }

    private async Task<MetricFieldDto> GetFieldAsync(int fieldId, CancellationToken ct) =>
        await ProjectFields(db.MetricFields.Where(x => x.Id == fieldId)).SingleAsync(ct);

    private async Task<MetricField> FindFieldAsync(int metricId, int fieldId, CancellationToken ct) =>
        await db.MetricFields.SingleOrDefaultAsync(x => x.Id == fieldId && x.MetricId == metricId, ct)
        ?? throw new NotFoundException($"Field {fieldId} was not found for metric {metricId}.");

    private async Task EnsureMetricExistsAsync(int metricId, CancellationToken ct)
    {
        if (!await db.Metrics.AnyAsync(x => x.Id == metricId, ct))
            throw new NotFoundException($"Metric {metricId} was not found.");
    }

    private async Task EnsureNameIsFreeAsync(int methodId, string name, int? exceptId, CancellationToken ct)
    {
        if (await db.Metrics.AnyAsync(x => x.PremiumMethodId == methodId && x.Name == name && x.Id != exceptId, ct))
            throw new ConflictException($"Metric '{name}' already exists for this premium method.");
    }

    private static void Apply(Metric metric, MetricRequest request)
    {
        metric.Name = request.Name.Trim();
        metric.Description = request.Description?.Trim() ?? "";
        metric.SourceType = request.SourceType;
        metric.SourceName = request.SourceName.Trim();
        metric.IngestionFrequency = request.IngestionFrequency;
    }

    private static IQueryable<MetricDto> Project(IQueryable<Metric> query) =>
        query.AsNoTracking().Select(x => new MetricDto(
            x.Id, x.PremiumMethodId, x.Name, x.Description, x.SourceType, x.SourceName, x.IngestionFrequency,
            x.Fields.Count,
            x.ImportBatches.Max(b => (DateTime?)b.StartedAtUtc)));

    private static IQueryable<MetricFieldDto> ProjectFields(IQueryable<MetricField> query) =>
        query.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new MetricFieldDto(
                x.Id, x.Key, x.DisplayName, x.DataType, x.IsRelevant, x.IsFilterable, x.IsSortable,
                x.SchemaColumns.OrderByDescending(c => c.SchemaVersion.VersionNumber)
                    .Select(c => c.SourceHeader).FirstOrDefault(),
                x.Aliases.Select(a => a.Alias).ToList()));
}
