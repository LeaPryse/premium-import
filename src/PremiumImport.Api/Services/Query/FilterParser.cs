using System.Globalization;
using PremiumImport.Api.Domain;

namespace PremiumImport.Api.Services.Query;

public enum FilterOperator
{
    Contains,
    Equals,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    Between
}

public sealed record FieldFilter(FilterOperator Operator, object Value, object? To = null);

/// <summary>
/// Parses a filter expression sent by the UI into a typed filter.
///   text:         "abc" (contains) | "=abc" (equals)
///   number/date:  "10" | "=10" | "&gt;10" | "&gt;=10" | "&lt;10" | "&lt;=10" | "10..20" (between, inclusive)
///   boolean:      "true" | "false"
/// Dates use ISO format (yyyy-MM-dd), which is what an HTML date input sends.
/// </summary>
public static class FilterParser
{
    public static IReadOnlyList<string> OperatorsFor(DataType type) => type switch
    {
        DataType.Text => ["contains", "="],
        DataType.Boolean => ["="],
        _ => ["=", ">", ">=", "<", "<=", "between"]
    };

    public static FieldFilter Parse(string expression, DataType type)
    {
        var text = expression.Trim();
        if (text == "")
            throw new BadInputException("Empty filter.");

        if (type is DataType.Number or DataType.Date && text.Contains(".."))
        {
            var parts = text.Split("..", 2);
            return new FieldFilter(FilterOperator.Between, ParseValue(parts[0], type), ParseValue(parts[1], type));
        }

        var (op, operand) = text switch
        {
            _ when text.StartsWith(">=") => (FilterOperator.GreaterOrEqual, text[2..]),
            _ when text.StartsWith("<=") => (FilterOperator.LessOrEqual, text[2..]),
            _ when text.StartsWith('>') => (FilterOperator.GreaterThan, text[1..]),
            _ when text.StartsWith('<') => (FilterOperator.LessThan, text[1..]),
            _ when text.StartsWith('=') => (FilterOperator.Equals, text[1..]),
            _ => (type == DataType.Text ? FilterOperator.Contains : FilterOperator.Equals, text)
        };

        if (type is DataType.Text or DataType.Boolean && op is not (FilterOperator.Contains or FilterOperator.Equals))
            throw new BadInputException($"Operator is not supported for {type} fields.");

        return new FieldFilter(op, ParseValue(operand, type));
    }

    private static object ParseValue(string raw, DataType type)
    {
        var text = raw.Trim();
        return type switch
        {
            DataType.Number when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) => n,
            DataType.Date when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) => d.Date,
            DataType.Boolean when bool.TryParse(text, out var b) => b,
            DataType.Text => text,
            _ => throw new BadInputException($"'{text}' is not a valid {type.ToString().ToLowerInvariant()} value.")
        };
    }
}
