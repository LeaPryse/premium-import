using System.ComponentModel.DataAnnotations;
using PremiumImport.Api.Domain;
using DataType = PremiumImport.Api.Domain.DataType;

namespace PremiumImport.Api.Contracts;

// ---------- Premium methods ----------

public sealed record PremiumMethodDto(
    int Id,
    string Number,
    string Description,
    decimal PremiumPercent,
    PeriodType CalculationPeriod,
    int MetricCount);

public sealed record PremiumMethodRequest(
    [Required, StringLength(20)] string Number,
    [Required, StringLength(200)] string Description,
    [Range(0, 100)] decimal PremiumPercent,
    PeriodType CalculationPeriod);

// ---------- Metrics ----------

public sealed record MetricDto(
    int Id,
    int PremiumMethodId,
    string Name,
    string Description,
    SourceType SourceType,
    string SourceName,
    Frequency IngestionFrequency,
    int FieldCount,
    DateTime? LastImportAtUtc);

public sealed record MetricRequest(
    [Required, StringLength(100)] string Name,
    [StringLength(500)] string? Description,
    SourceType SourceType,
    [Required, StringLength(100)] string SourceName,
    Frequency IngestionFrequency);

// ---------- Fields & file structures ----------

public sealed record MetricFieldDto(
    int Id,
    string Key,
    string DisplayName,
    DataType DataType,
    bool IsRelevant,
    bool IsFilterable,
    bool IsSortable,
    string? SourceHeader,
    IReadOnlyList<string> Aliases);

public sealed record UpdateMetricFieldRequest(
    [Required, StringLength(100)] string DisplayName,
    DataType DataType,
    bool IsRelevant,
    bool IsFilterable,
    bool IsSortable);

public sealed record AddAliasRequest([Required, StringLength(100)] string Alias);

public sealed record SchemaColumnDto(int Ordinal, string SourceHeader, int FieldId, string FieldKey);

public sealed record SchemaVersionDto(
    int Id,
    int VersionNumber,
    DateTime CreatedAtUtc,
    int ImportCount,
    IReadOnlyList<SchemaColumnDto> Columns);

// ---------- Imports ----------

public sealed class ImportFileRequest
{
    [Required] public IFormFile File { get; set; } = null!;
    [Range(ReportingPeriod.MinYear, ReportingPeriod.MaxYear)] public int ReportingYear { get; set; }
    public PeriodType PeriodType { get; set; }
    [Range(1, 12)] public int PeriodNumber { get; set; }
}

public sealed record ImportResultDto(
    long BatchId,
    int RowCount,
    int SchemaVersion,
    bool NewSchema,
    IReadOnlyList<string> NewFields);

public sealed record ImportBatchDto(
    long Id,
    string OriginalFileName,
    int ReportingYear,
    PeriodType PeriodType,
    int PeriodNumber,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    ImportStatus Status,
    int RowCount,
    int SchemaVersion,
    string? ErrorMessage,
    bool IsSuperseded);

// ---------- Data query ----------

public sealed class MetricRowsQuery
{
    /// <summary>Show a single batch. When empty - the latest completed batch of every period is used.</summary>
    public long? BatchId { get; set; }
    public int? Year { get; set; }
    public PeriodType? PeriodType { get; set; }
    public int? PeriodNumber { get; set; }
    /// <summary>Include rows of batches that were replaced by a newer upload for the same period.</summary>
    public bool IncludeSuperseded { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 500)] public int PageSize { get; set; } = 50;
    public string? SortBy { get; set; }
    public bool Descending { get; set; }
    /// <summary>filters[fieldKey]=expression. Expressions: "abc" (contains), "=abc", "&gt;10", "&lt;=10", "10..20".</summary>
    public Dictionary<string, string>? Filters { get; set; }
}

public sealed record ImportedRowDto(
    long Id,
    long BatchId,
    int SourceRowNumber,
    IReadOnlyDictionary<string, object?> Values);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record ValueCountDto(object? Value, int Count);

public sealed record FieldFilterOptionsDto(
    string FieldKey,
    DataType DataType,
    IReadOnlyList<string> Operators,
    IReadOnlyList<ValueCountDto>? Values,
    object? Min,
    object? Max);
