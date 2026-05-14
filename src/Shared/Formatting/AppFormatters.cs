using System.Globalization;

namespace CbsContractsDesktopClient.Shared.Formatting;

public static class AppFormatters
{
    public static string FormatMoney(object? value)
    {
        return value switch
        {
            long longValue => longValue.ToString("N0", CultureInfo.CurrentCulture) + " руб.",
            int intValue => intValue.ToString("N0", CultureInfo.CurrentCulture) + " руб.",
            decimal decimalValue => decimalValue.ToString("N2", CultureInfo.CurrentCulture) + " руб.",
            string text when decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue)
                => parsedValue.ToString("N2", CultureInfo.CurrentCulture) + " руб.",
            _ => value?.ToString() ?? string.Empty
        };
    }

    public static string FormatDisplayDate(object? value)
    {
        var date = ParseDate(value);
        return date?.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture) ?? "-";
    }

    public static string FormatBoolean(object? value)
    {
        return TryGetBool(value) == true ? "Да" : "Нет";
    }

    public static string FormatFlagDate(object? flagValue, object? dateValue)
    {
        var hasFlag = TryGetBool(flagValue) == true;
        var date = FormatDisplayDate(dateValue);
        if (!hasFlag)
        {
            return date == "-" ? "Нет" : date;
        }

        return date == "-" ? "Да" : date;
    }

    public static string? FormatDate(DateTimeOffset? value)
    {
        return value is null
            ? null
            : value.Value.Date.ToString("ddd MMM dd yyyy", CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset? ParseDate(object? value)
    {
        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var offset))
        {
            return offset;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
        {
            return new DateTimeOffset(date);
        }

        return null;
    }

    public static long? TryGetLong(object? value)
    {
        return value switch
        {
            long int64Value => int64Value,
            int int32Value => int32Value,
            decimal decimalValue => (long)decimalValue,
            string text when long.TryParse(text, out var parsedValue) => parsedValue,
            _ => null
        };
    }

    public static int? TryGetInt(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsedValue)
                ? parsedValue
                : null;
    }

    public static bool? TryGetBool(object? value)
    {
        return value switch
        {
            bool boolValue => boolValue,
            string text when bool.TryParse(text, out var parsedValue) => parsedValue,
            string text when long.TryParse(text, out var numericValue) => numericValue != 0,
            long int64Value => int64Value != 0,
            int int32Value => int32Value != 0,
            decimal decimalValue => decimalValue != 0,
            _ => null
        };
    }
}
