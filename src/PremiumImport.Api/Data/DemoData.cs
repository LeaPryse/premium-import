using Microsoft.EntityFrameworkCore;

namespace PremiumImport.Api.Data;

public static class DemoData
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.PremiumMethods.AnyAsync())
            return;

        var method = new PremiumMethod
        {
            Number = "PR-2026",
            Description = "פרמיית ביצוע שנתית",
            PremiumPercent = 8.5m,
            CalculationPeriod = "רבעוני"
        };
        var metric = new Metric
        {
            Name = "עובדים",
            Description = "נתוני עובדים לדוגמה",
            SourceType = "Excel",
            SourceName = "HR",
            IngestionFrequency = "רבעונית"
        };
        var fields = new[]
        {
            new MetricField { Key = "employee-id", DisplayName = "ת״ז", DataType = "text", IsRelevant = true },
            new MetricField { Key = "employee-name", DisplayName = "שם עובד", DataType = "text", IsRelevant = true },
            new MetricField { Key = "department", DisplayName = "מחלקה", DataType = "text", IsRelevant = true },
            new MetricField { Key = "status", DisplayName = "סטטוס", DataType = "text", IsRelevant = true },
            new MetricField { Key = "manager", DisplayName = "מנהל ישיר", DataType = "text", IsRelevant = false },
            new MetricField { Key = "location", DisplayName = "מיקום", DataType = "text", IsRelevant = false }
        };
        metric.Fields = fields;
        method.Metrics.Add(metric);

        var schema = new MetricSchemaVersion
        {
            Metric = metric,
            Fingerprint = "demo-workers-v1",
            VersionNumber = 1,
            CreatedAtUtc = DateTime.UtcNow
        };
        foreach (var (field, ordinal) in fields.Take(4).Select((field, ordinal) => (field, ordinal)))
            schema.Columns.Add(new MetricSchemaColumn
            {
                MetricField = field,
                SourceHeader = field.DisplayName,
                Ordinal = ordinal
            });

        var batch = new ImportBatch
        {
            Metric = metric,
            SchemaVersion = schema,
            OriginalFileName = "employees-q1-2026.xlsx",
            ReportingYear = 2026,
            ReportingPeriod = "רבעון 1",
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            CompletedAtUtc = DateTime.UtcNow.AddMinutes(-4),
            Status = "Completed",
            RowCount = 3
        };
        var sampleData = new[]
        {
            new[] { "123456789", "נועה לוי", "מכירות", "פעיל" },
            new[] { "987654321", "דניאל כהן", "פיתוח", "פעיל" },
            new[] { "112233445", "מיה ישראלי", "משאבי אנוש", "בחופשה" }
        };
        foreach (var (values, rowIndex) in sampleData.Select((values, rowIndex) => (values, rowIndex)))
        {
            var row = new ImportedRow
            {
                ImportBatch = batch,
                SourceRowNumber = rowIndex + 2
            };
            for (var fieldIndex = 0; fieldIndex < 4; fieldIndex++)
                row.Values.Add(new ImportedValue
                {
                    MetricField = fields[fieldIndex],
                    TextValue = values[fieldIndex],
                    RawValue = values[fieldIndex]
                });
            batch.Rows.Add(row);
        }

        db.PremiumMethods.Add(method);
        await db.SaveChangesAsync();
    }
}
