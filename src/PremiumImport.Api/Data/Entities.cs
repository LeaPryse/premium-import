namespace PremiumImport.Api.Data;

public sealed class PremiumMethod
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal PremiumPercent { get; set; }
    public string CalculationPeriod { get; set; } = "";
    public ICollection<Metric> Metrics { get; set; } = new List<Metric>();
}

public sealed class Metric
{
    public int Id { get; set; }
    public int PremiumMethodId { get; set; }
    public PremiumMethod PremiumMethod { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SourceType { get; set; } = "Excel";
    public string SourceName { get; set; } = "";
    public string IngestionFrequency { get; set; } = "";
    public ICollection<MetricField> Fields { get; set; } = new List<MetricField>();
    public ICollection<MetricSchemaVersion> SchemaVersions { get; set; } = new List<MetricSchemaVersion>();
    public ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();
}

public sealed class MetricField
{
    public int Id { get; set; }
    public int MetricId { get; set; }
    public Metric Metric { get; set; } = null!;
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DataType { get; set; } = "text";
    public bool IsRelevant { get; set; }
    public bool IsFilterable { get; set; } = true;
    public bool IsSortable { get; set; } = true;
    public ICollection<MetricSchemaColumn> SchemaColumns { get; set; } = new List<MetricSchemaColumn>();
}

public sealed class MetricSchemaVersion
{
    public int Id { get; set; }
    public int MetricId { get; set; }
    public Metric Metric { get; set; } = null!;
    public string Fingerprint { get; set; } = "";
    public int VersionNumber { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<MetricSchemaColumn> Columns { get; set; } = new List<MetricSchemaColumn>();
    public ICollection<ImportBatch> ImportBatches { get; set; } = new List<ImportBatch>();
}

public sealed class MetricSchemaColumn
{
    public int Id { get; set; }
    public int SchemaVersionId { get; set; }
    public MetricSchemaVersion SchemaVersion { get; set; } = null!;
    public int MetricFieldId { get; set; }
    public MetricField MetricField { get; set; } = null!;
    public string SourceHeader { get; set; } = "";
    public int Ordinal { get; set; }
}

public sealed class ImportBatch
{
    public long Id { get; set; }
    public int MetricId { get; set; }
    public Metric Metric { get; set; } = null!;
    public int SchemaVersionId { get; set; }
    public MetricSchemaVersion SchemaVersion { get; set; } = null!;
    public string OriginalFileName { get; set; } = "";
    public int ReportingYear { get; set; }
    public string ReportingPeriod { get; set; } = "";
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Status { get; set; } = "Processing";
    public int RowCount { get; set; }
    public string? ErrorMessage { get; set; }
    public ICollection<ImportedRow> Rows { get; set; } = new List<ImportedRow>();
}

public sealed class ImportedRow
{
    public long Id { get; set; }
    public long ImportBatchId { get; set; }
    public ImportBatch ImportBatch { get; set; } = null!;
    public int SourceRowNumber { get; set; }
    public ICollection<ImportedValue> Values { get; set; } = new List<ImportedValue>();
}

public sealed class ImportedValue
{
    public long Id { get; set; }
    public long ImportedRowId { get; set; }
    public ImportedRow ImportedRow { get; set; } = null!;
    public int MetricFieldId { get; set; }
    public MetricField MetricField { get; set; } = null!;
    public string? TextValue { get; set; }
    public decimal? NumberValue { get; set; }
    public DateTime? DateValue { get; set; }
    public bool? BooleanValue { get; set; }
    public string? RawValue { get; set; }
}
