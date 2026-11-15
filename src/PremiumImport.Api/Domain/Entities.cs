namespace PremiumImport.Api.Domain;

public class PremiumMethod
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal PremiumPercent { get; set; }
    public PeriodType CalculationPeriod { get; set; }
    public ICollection<Metric> Metrics { get; set; } = new List<Metric>();
}

public class Metric
{
    public int Id { get; set; }
    public int PremiumMethodId { get; set; }
    public PremiumMethod PremiumMethod { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public SourceType SourceType { get; set; } = SourceType.Excel;
    public string SourceName { get; set; } = "";
    public Frequency IngestionFrequency { get; set; }
    public ICollection<MetricField> Fields { get; set; } = new List<MetricField>();
    public ICollection<MetricSchemaVersion> SchemaVersions { get; set; } = new List<MetricSchemaVersion>();
    public ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();
}

/// <summary>
/// A logical field of a metric (the metric's "dictionary"). Stable across file-structure versions:
/// the same field keeps its Id even when the column moves or its header is renamed (via aliases).
/// </summary>
public class MetricField
{
    public int Id { get; set; }
    public int MetricId { get; set; }
    public Metric Metric { get; set; } = null!;
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DataType DataType { get; set; } = DataType.Text;
    public bool IsRelevant { get; set; } = true;
    public bool IsFilterable { get; set; } = true;
    public bool IsSortable { get; set; } = true;
    public ICollection<MetricFieldAlias> Aliases { get; set; } = new List<MetricFieldAlias>();
    public ICollection<MetricSchemaColumn> SchemaColumns { get; set; } = new List<MetricSchemaColumn>();

    public bool MatchesHeader(string header) =>
        Equals(DisplayName, header) ||
        Aliases.Any(a => Equals(a.Alias, header)) ||
        SchemaColumns.Any(c => Equals(c.SourceHeader, header));

    private static bool Equals(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>An alternative header name that maps to an existing field (e.g. after a column is renamed).</summary>
public class MetricFieldAlias
{
    public int Id { get; set; }
    public int MetricFieldId { get; set; }
    public MetricField MetricField { get; set; } = null!;
    public string Alias { get; set; } = "";
}

/// <summary>One distinct file structure (ordered list of headers) received for a metric.</summary>
public class MetricSchemaVersion
{
    public int Id { get; set; }
    public int MetricId { get; set; }
    public Metric Metric { get; set; } = null!;
    public int VersionNumber { get; set; }
    public string Fingerprint { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<MetricSchemaColumn> Columns { get; set; } = new List<MetricSchemaColumn>();
    public ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();
}

/// <summary>Maps a column position in a specific file structure to a logical field.</summary>
public class MetricSchemaColumn
{
    public int Id { get; set; }
    public int SchemaVersionId { get; set; }
    public MetricSchemaVersion SchemaVersion { get; set; } = null!;
    public int MetricFieldId { get; set; }
    public MetricField MetricField { get; set; } = null!;
    public string SourceHeader { get; set; } = "";
    public int Ordinal { get; set; }
}

/// <summary>One upload of a file for a metric and reporting period. Never deleted - this is the import history.</summary>
public class ImportBatch
{
    private const int MaxErrorLength = 1000;

    public long Id { get; set; }
    public int MetricId { get; set; }
    public Metric Metric { get; set; } = null!;
    public int SchemaVersionId { get; set; }
    public MetricSchemaVersion SchemaVersion { get; set; } = null!;
    public string OriginalFileName { get; set; } = "";
    public int ReportingYear { get; set; }
    public PeriodType PeriodType { get; set; }
    public int PeriodNumber { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public ImportStatus Status { get; set; } = ImportStatus.Processing;
    public int RowCount { get; set; }
    public string? ErrorMessage { get; set; }
    public ICollection<ImportedRow> Rows { get; set; } = new List<ImportedRow>();

    public void MarkCompleted(int rowCount)
    {
        Status = ImportStatus.Completed;
        RowCount = rowCount;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        Status = ImportStatus.Failed;
        CompletedAtUtc = DateTime.UtcNow;
        ErrorMessage = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
    }
}

public class ImportedRow
{
    public long Id { get; set; }
    public long ImportBatchId { get; set; }
    public ImportBatch ImportBatch { get; set; } = null!;
    public int SourceRowNumber { get; set; }
    public ICollection<ImportedValue> Values { get; set; } = new List<ImportedValue>();
}

/// <summary>
/// A single cell (EAV). Exactly one typed column is filled according to the field's DataType,
/// so filtering/sorting compare real numbers and dates. RawValue keeps the original text.
/// Primary key: (ImportedRowId, MetricFieldId).
/// </summary>
public class ImportedValue
{
    public long ImportedRowId { get; set; }
    public ImportedRow ImportedRow { get; set; } = null!;
    public int MetricFieldId { get; set; }
    public MetricField MetricField { get; set; } = null!;
    public string? TextValue { get; set; }
    public decimal? NumberValue { get; set; }
    public DateTime? DateValue { get; set; }
    public bool? BooleanValue { get; set; }
    public string? RawValue { get; set; }

    public object? TypedValue =>
        (object?)NumberValue ?? (object?)DateValue ?? (object?)BooleanValue ?? TextValue ?? RawValue;
}
