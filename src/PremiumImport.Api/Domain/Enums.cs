namespace PremiumImport.Api.Domain;

/// <summary>Logical data type of a metric field. Decides which typed column of ImportedValue holds the value.</summary>
public enum DataType
{
    Text,
    Number,
    Date,
    Boolean
}

public enum SourceType
{
    Excel,
    ExternalApi
}

public enum PeriodType
{
    Month,
    Quarter,
    HalfYear,
    Year
}

public enum Frequency
{
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    Yearly
}

public enum ImportStatus
{
    Processing,
    Completed,
    Failed
}
