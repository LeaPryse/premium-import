// Generates sample Excel files for the demo (different structures per metric + a structure change).
// Usage: dotnet run --project tools/SampleDataGenerator [outputDir]   (default: ./samples)
using ClosedXML.Excel;

var outputDir = args.Length > 0 ? args[0] : "samples";
Directory.CreateDirectory(outputDir);
var random = new Random(2026);

string[] firstNames = ["נועה", "דניאל", "מיה", "יוסי", "שרה", "אורי", "תמר", "איתי", "רוני", "עדי"];
string[] lastNames = ["לוי", "כהן", "ישראלי", "מזרחי", "פרץ", "ביטון", "אברהם", "פרידמן"];
string[] departments = ["מכירות", "פיתוח", "משאבי אנוש", "כספים", "תפעול"];
string[] statuses = ["פעיל", "פעיל", "פעיל", "בחופשה", "לא פעיל"];
string[] managers = ["רחל גולן", "משה דיין", "אבי כץ", "דנה רון"];
string[] locations = ["תל אביב", "ירושלים", "חיפה", "באר שבע"];
string[] regions = ["מרכז", "צפון", "דרום", "ירושלים"];

var employeeIds = Enumerable.Range(0, 200).Select(_ => random.Next(10_000_000, 399_999_999).ToString("D9")).ToArray();
string Name() => $"{Pick(firstNames)} {Pick(lastNames)}";
T Pick<T>(T[] items) => items[random.Next(items.Length)];

// Metric "employees" - version 1 (Q1) and version 2 with two extra columns (Q2).
Write("employees_2026_Q1_v1.xlsx", ["ת\"ז", "שם עובד", "מחלקה", "סטטוס"],
    employeeIds.Select(id => new object[] { id, Name(), Pick(departments), Pick(statuses) }));

Write("employees_2026_Q2_v2.xlsx", ["ת\"ז", "שם עובד", "מחלקה", "סטטוס", "מנהל ישיר", "מיקום"],
    employeeIds.Select(id => new object[] { id, Name(), Pick(departments), Pick(statuses), Pick(managers), Pick(locations) }));

// Metric "work hours" - monthly.
Write("work_hours_2026_04.xlsx", ["ת\"ז", "חודש", "שעות רגילות", "שעות נוספות"],
    employeeIds.Select(id => new object[] { id, new DateTime(2026, 4, 1), random.Next(120, 186), Math.Round(random.NextDouble() * 30, 1) }));

// Metric "sales" - a large file to demonstrate paging / filtering performance.
Write("sales_2026_Q1_50k.xlsx", ["קוד סניף", "תאריך", "סכום מכירות", "כמות עסקאות", "אזור"],
    Enumerable.Range(0, 50_000).Select(_ => new object[]
    {
        $"BR-{random.Next(1, 120):D3}",
        new DateTime(2026, 1, 1).AddDays(random.Next(0, 90)),
        Math.Round(random.NextDouble() * 20_000, 2),
        random.Next(1, 300),
        Pick(regions)
    }));

Console.WriteLine($"Sample files written to {Path.GetFullPath(outputDir)}");

void Write(string fileName, string[] headers, IEnumerable<object[]> rows)
{
    using var workbook = new XLWorkbook();
    var sheet = workbook.AddWorksheet("Data");
    sheet.RightToLeft = true;
    for (var c = 0; c < headers.Length; c++)
        sheet.Cell(1, c + 1).Value = headers[c];

    var r = 2;
    foreach (var row in rows)
    {
        for (var c = 0; c < row.Length; c++)
        {
            var cell = sheet.Cell(r, c + 1);
            switch (row[c])
            {
                case DateTime d: cell.Value = d; cell.Style.DateFormat.Format = "dd/MM/yyyy"; break;
                case int i: cell.Value = i; break;
                case double x: cell.Value = x; break;
                default: cell.Value = row[c].ToString(); break; // IDs stay text (leading zeros)
            }
        }
        r++;
    }
    sheet.Row(1).Style.Font.Bold = true;
    workbook.SaveAs(Path.Combine(outputDir, fileName));
    Console.WriteLine($"  {fileName}: {r - 2:N0} rows");
}
