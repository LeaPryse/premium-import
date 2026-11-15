using System.Globalization;
using PremiumImport.Api.Domain;

namespace PremiumImport.Api.Services.Import;

/// <summary>Strategy per data type: parses a raw cell and writes it to the matching typed column.</summary>
public interface IFieldValueConverter
{
    DataType DataType { get; }
    bool TryAssign(object raw, ImportedValue target);
}

public sealed class TextConverter : IFieldValueConverter
{
    public DataType DataType => DataType.Text;

    public bool TryAssign(object raw, ImportedValue target)
    {
        target.TextValue = FieldValueConverters.AsText(raw);
        return true;
    }
}

public sealed class NumberConverter : IFieldValueConverter
{
    public DataType DataType => DataType.Number;

    public bool TryAssign(object raw, ImportedValue target)
    {
        if (raw is byte or short or int or long or float or double or decimal)
        {
            target.NumberValue = Convert.ToDecimal(raw, CultureInfo.InvariantCulture);
            return true;
        }

        var text = FieldValueConverters.AsText(raw);
        // "012345678" is an identifier, not a number - converting it would lose the leading zero.
        if (text.Length > 1 && text[0] == '0' && text[1] != '.')
            return false;
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            return false;
        target.NumberValue = number;
        return true;
    }
}

public sealed class DateConverter : IFieldValueConverter
{
    private static readonly string[] Formats =
        ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-ddTHH:mm:ss", "dd/MM/yyyy HH:mm"];

    public DataType DataType => DataType.Date;

    public bool TryAssign(object raw, ImportedValue target)
    {
        if (raw is DateTime date)
        {
            target.DateValue = date;
            return true;
        }
        if (raw is string text &&
            DateTime.TryParseExact(text.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            target.DateValue = date;
            return true;
        }
        return false;
    }
}

public sealed class BooleanConverter : IFieldValueConverter
{
    private static readonly Dictionary<string, bool> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["true"] = true, ["false"] = false, ["yes"] = true, ["no"] = false, ["כן"] = true, ["לא"] = false
    };

    public DataType DataType => DataType.Boolean;

    public bool TryAssign(object raw, ImportedValue target)
    {
        if (raw is bool b)
        {
            target.BooleanValue = b;
            return true;
        }
        if (Words.TryGetValue(FieldValueConverters.AsText(raw), out b))
        {
            target.BooleanValue = b;
            return true;
        }
        return false;
    }
}

/// <summary>Picks the right converter per field and infers a data type for new columns.</summary>
public sealed class FieldValueConverters
{
    private readonly Dictionary<DataType, IFieldValueConverter> _byType;

    // Inference order: most specific first; Text always matches and is the fallback.
    private static readonly DataType[] InferenceOrder = [DataType.Date, DataType.Boolean, DataType.Number];

    public FieldValueConverters(IEnumerable<IFieldValueConverter> converters) =>
        _byType = converters.ToDictionary(c => c.DataType);

    public static string AsText(object raw) =>
        (Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "").Trim();

    public static bool IsEmpty(object? raw) => raw is null || AsText(raw) == "";

    /// <summary>
    /// Fills the typed column. A value that does not match the field type is kept as text (never lost),
    /// so a later type correction can be explained and the original is always in RawValue.
    /// </summary>
    public ImportedValue Create(object raw, MetricField field)
    {
        var value = new ImportedValue { MetricFieldId = field.Id, RawValue = Truncate(AsText(raw)) };
        if (!_byType[field.DataType].TryAssign(raw, value))
            value.TextValue = value.RawValue;
        value.TextValue = value.TextValue is null ? null : Truncate(value.TextValue);
        return value;
    }

    public DataType Infer(IEnumerable<object?> samples)
    {
        var values = samples.Where(v => !IsEmpty(v)).Cast<object>().ToList();
        if (values.Count == 0)
            return DataType.Text;

        var probe = new ImportedValue();
        foreach (var type in InferenceOrder)
            if (values.All(v => _byType[type].TryAssign(v, probe)))
                return type;
        return DataType.Text;
    }

    private static string Truncate(string text) => text.Length > 400 ? text[..400] : text;
}
