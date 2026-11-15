using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PremiumImport.Api.Data;
using PremiumImport.Api.Domain;

namespace PremiumImport.Api.Services.Import;

public sealed record ResolvedSchema(
    MetricSchemaVersion Version,
    IReadOnlyList<MetricField> FieldsByOrdinal,
    bool IsNew,
    IReadOnlyList<string> NewFields);

/// <summary>
/// Identifies the structure of an incoming file.
/// Same ordered headers as before  -> existing version (fingerprint match).
/// Different headers               -> new version; known headers map to existing fields
///                                    (by display name, alias or a previous header), unknown headers become new fields.
/// No DB schema change and no code change is needed for a new or changed file structure.
/// </summary>
public sealed class SchemaResolver(AppDbContext db, FieldValueConverters converters)
{
    public async Task<ResolvedSchema> ResolveAsync(
        int metricId,
        IReadOnlyList<string> headers,
        IReadOnlyList<object?[]> sampleRows,
        CancellationToken ct)
    {
        var fingerprint = Fingerprint(headers);
        var existing = await db.MetricSchemaVersions
            .Include(x => x.Columns).ThenInclude(x => x.MetricField)
            .SingleOrDefaultAsync(x => x.MetricId == metricId && x.Fingerprint == fingerprint, ct);
        if (existing is not null)
            return new ResolvedSchema(
                existing, existing.Columns.OrderBy(c => c.Ordinal).Select(c => c.MetricField).ToList(), false, []);

        var fields = await db.MetricFields
            .Include(x => x.Aliases)
            .Include(x => x.SchemaColumns)
            .Where(x => x.MetricId == metricId)
            .ToListAsync(ct);
        var nextVersion = (await db.MetricSchemaVersions
            .Where(x => x.MetricId == metricId)
            .MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0) + 1;

        var version = new MetricSchemaVersion
        {
            MetricId = metricId,
            VersionNumber = nextVersion,
            Fingerprint = fingerprint,
            CreatedAtUtc = DateTime.UtcNow
        };
        var mapped = new List<MetricField>();
        var newFields = new List<string>();

        for (var ordinal = 0; ordinal < headers.Count; ordinal++)
        {
            var header = headers[ordinal];
            var field = fields.FirstOrDefault(f => f.MatchesHeader(header));
            if (field is not null && mapped.Contains(field))
                throw new BadInputException($"Columns '{header}' and another column map to the same field '{field.DisplayName}'.");

            if (field is null)
            {
                field = new MetricField
                {
                    MetricId = metricId,
                    Key = UniqueKey(header, fields),
                    DisplayName = header,
                    DataType = converters.Infer(sampleRows.Select(r => r[ordinal]))
                };
                fields.Add(field);
                db.MetricFields.Add(field);
                newFields.Add(header);
            }

            mapped.Add(field);
            version.Columns.Add(new MetricSchemaColumn { MetricField = field, SourceHeader = header, Ordinal = ordinal });
        }

        db.MetricSchemaVersions.Add(version);
        await db.SaveChangesAsync(ct);
        return new ResolvedSchema(version, mapped, true, newFields);
    }

    /// <summary>SHA-256 of the normalized, ordered header list.</summary>
    public static string Fingerprint(IEnumerable<string> headers) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join('\u001F', headers.Select(h => h.Trim().ToLowerInvariant())))));

    private static string UniqueKey(string header, IReadOnlyCollection<MetricField> fields)
    {
        var chars = header.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var key = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (key == "")
            key = "field";

        var candidate = key;
        for (var i = 2; fields.Any(f => f.Key == candidate); i++)
            candidate = $"{key}-{i}";
        return candidate;
    }
}
