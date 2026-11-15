using Microsoft.EntityFrameworkCore;
using PremiumImport.Api.Domain;

namespace PremiumImport.Api.Data;

/// <summary>Seeds one premium method with three metrics (definitions only - data arrives via Excel upload).</summary>
public static class DemoData
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.PremiumMethods.AnyAsync())
            return;

        db.PremiumMethods.Add(new PremiumMethod
        {
            Number = "PR-2026",
            Description = "פרמיית ביצוע שנתית",
            PremiumPercent = 8.5m,
            CalculationPeriod = PeriodType.Quarter,
            Metrics =
            {
                new Metric
                {
                    Name = "עובדים", Description = "נתוני עובדים",
                    SourceType = SourceType.Excel, SourceName = "HR", IngestionFrequency = Frequency.Quarterly
                },
                new Metric
                {
                    Name = "שעות עבודה", Description = "שעות רגילות ונוספות לעובד",
                    SourceType = SourceType.Excel, SourceName = "נוכחות", IngestionFrequency = Frequency.Monthly
                },
                new Metric
                {
                    Name = "מכירות", Description = "מכירות לפי סניף",
                    SourceType = SourceType.Excel, SourceName = "ERP", IngestionFrequency = Frequency.Monthly
                }
            }
        });
        await db.SaveChangesAsync();
    }
}
