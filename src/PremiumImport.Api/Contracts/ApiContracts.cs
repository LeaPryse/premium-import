namespace PremiumImport.Api.Contracts;

public sealed record PremiumMethodDto(
    int Id,
    string Number,
    string Description,
    decimal PremiumPercent,
    string CalculationPeriod,
    int MetricCount);

public sealed record CreatePremiumMethodRequest(
    string Number,
    string Description,
    decimal PremiumPercent,
    string CalculationPeriod);

public sealed record MetricDto(
    int Id,
    int PremiumMethodId,
    string Name,
    string Description,
    string SourceType,
    string SourceName,
    string IngestionFrequency);

public sealed record CreateMetricRequest(
    string Name,
    string Description,
    string SourceType,
    string SourceName,
    string IngestionFrequency);

public sealed record MetricFieldDto(
    int Id,
    string Key,
    string DisplayName,
    string DataType,
    bool IsRelevant,
    bool IsFilterable,
    bool IsSortable,
    string? SourceHeader);

public sealed record UpdateMetricFieldRequest(
    string DisplayName,
    string DataType,
    bool IsRelevant,
    bool IsFilterable,
    bool IsSortable);

public sealed record ImportBatchDto(
    long Id,
    string OriginalFileName,
    int ReportingYear,
    string ReportingPeriod,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string Status,
    int RowCount,
    int SchemaVersion,
    string? ErrorMessage);

public sealed record ImportedRowDto(
    long Id,
    int SourceRowNumber,
    IReadOnlyDictionary<string, object?> Values);

public sealed record PagedRowsDto(
    IReadOnlyList<ImportedRowDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record ImportResultDto(long BatchId, int RowCount, int SchemaVersion, bool NewSchema);
