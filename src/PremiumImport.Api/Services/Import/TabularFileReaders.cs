using System.Globalization;
using System.Text;
using ExcelDataReader;

namespace PremiumImport.Api.Services.Import;

/// <summary>
/// Abstraction over a source file. The import pipeline depends only on this interface,
/// so a new format (or an external-system source) is added as a new class without touching the pipeline.
/// </summary>
public interface ITabularFileReader
{
    bool CanRead(string fileName);
    ITabularData Open(Stream stream);
}

/// <summary>A header row followed by data rows, read in a streaming fashion (the whole file is never in memory).</summary>
public interface ITabularData : IDisposable
{
    IReadOnlyList<string> Headers { get; }
    IEnumerable<object?[]> ReadRows();
}

public abstract class ExcelDataReaderFileReader : ITabularFileReader
{
    static ExcelDataReaderFileReader() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    protected abstract IReadOnlyCollection<string> Extensions { get; }

    public bool CanRead(string fileName) =>
        Extensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase);

    public ITabularData Open(Stream stream) => new Data(CreateReader(stream));

    protected abstract IExcelDataReader CreateReader(Stream stream);

    private sealed class Data : ITabularData
    {
        private readonly IExcelDataReader _reader;

        public Data(IExcelDataReader reader)
        {
            _reader = reader;
            if (!reader.Read())
                throw new BadInputException("The file is empty.");

            var headers = Enumerable.Range(0, reader.FieldCount)
                .Select(i => Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)?.Trim() ?? "")
                .ToList();
            // Excel often reports trailing formatted-but-empty columns.
            while (headers.Count > 0 && headers[^1] == "")
                headers.RemoveAt(headers.Count - 1);

            if (headers.Count == 0)
                throw new BadInputException("The first row must contain column headers.");
            if (headers.Any(h => h == ""))
                throw new BadInputException("Every column must have a header.");
            var duplicate = headers.GroupBy(h => h, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is not null)
                throw new BadInputException($"Duplicate column header '{duplicate.Key}'.");
            Headers = headers;
        }

        public IReadOnlyList<string> Headers { get; }

        public IEnumerable<object?[]> ReadRows()
        {
            while (_reader.Read())
            {
                var row = new object?[Headers.Count];
                for (var i = 0; i < row.Length && i < _reader.FieldCount; i++)
                    row[i] = _reader.GetValue(i);
                yield return row;
            }
        }

        public void Dispose() => _reader.Dispose();
    }
}

public sealed class ExcelFileReader : ExcelDataReaderFileReader
{
    protected override IReadOnlyCollection<string> Extensions { get; } = [".xlsx", ".xls"];
    protected override IExcelDataReader CreateReader(Stream stream) => ExcelReaderFactory.CreateReader(stream);
}

public sealed class CsvFileReader : ExcelDataReaderFileReader
{
    protected override IReadOnlyCollection<string> Extensions { get; } = [".csv"];
    protected override IExcelDataReader CreateReader(Stream stream) => ExcelReaderFactory.CreateCsvReader(stream);
}
