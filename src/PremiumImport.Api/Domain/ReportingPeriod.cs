namespace PremiumImport.Api.Domain;

/// <summary>Rules for a reporting period such as "2026, Quarter 1" or "2026, Month 4".</summary>
public static class ReportingPeriod
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    public static int MaxNumber(PeriodType type) => type switch
    {
        PeriodType.Month => 12,
        PeriodType.Quarter => 4,
        PeriodType.HalfYear => 2,
        _ => 1
    };

    public static bool IsValid(int year, PeriodType type, int number) =>
        year is >= MinYear and <= MaxYear && number >= 1 && number <= MaxNumber(type);
}
