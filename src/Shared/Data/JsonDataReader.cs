using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Shared.Data;

public static class JsonDataReader
{
    public static string? TryGetText(ReferenceDataRow? row, params string[] fieldKeys)
    {
        if (row is null)
        {
            return null;
        }

        foreach (var fieldKey in fieldKeys)
        {
            var text = row.GetValue(fieldKey)?.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    public static string GetText(ReferenceDataRow? row, params string[] fieldKeys)
    {
        return TryGetText(row, fieldKeys) ?? string.Empty;
    }

    public static string? TryGetSingleLineText(ReferenceDataRow? row, params string[] fieldKeys)
    {
        return NormalizeSingleLine(TryGetText(row, fieldKeys));
    }

    public static string GetSingleLineText(ReferenceDataRow? row, params string[] fieldKeys)
    {
        return TryGetSingleLineText(row, fieldKeys) ?? string.Empty;
    }

    public static string? TryGetDisplayText(
        ReferenceDataRow? displayRow,
        ReferenceDataRow? fallbackRow,
        params string[] fieldKeys)
    {
        return TryGetText(displayRow, fieldKeys)
            ?? TryGetText(fallbackRow, fieldKeys);
    }

    public static string GetDisplayText(
        ReferenceDataRow? displayRow,
        ReferenceDataRow? fallbackRow,
        params string[] fieldKeys)
    {
        return TryGetDisplayText(displayRow, fallbackRow, fieldKeys) ?? string.Empty;
    }

    public static string? TryGetRawText(ReferenceDataRow? row, string fieldKey)
    {
        return row?.GetValue(fieldKey)?.ToString();
    }

    public static string GetRawText(ReferenceDataRow? row, string fieldKey)
    {
        return TryGetRawText(row, fieldKey) ?? string.Empty;
    }

    public static string? TryFirstText(params object?[] values)
    {
        foreach (var value in values)
        {
            var text = value?.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    public static string FirstText(params object?[] values)
    {
        return TryFirstText(values) ?? string.Empty;
    }

    public static JsonElement? TryGetArray(ReferenceDataRow? row, string fieldKey)
    {
        if (row is null)
        {
            return null;
        }

        if (row.Values.TryGetValue(fieldKey, out var directValue)
            && directValue.ValueKind == JsonValueKind.Array)
        {
            return directValue;
        }

        var segments = fieldKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1 || !row.Values.TryGetValue(segments[0], out var current))
        {
            return null;
        }

        for (var index = 1; index < segments.Length; index++)
        {
            if (current.ValueKind != JsonValueKind.Object
                || !current.TryGetProperty(segments[index], out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.Array ? current : null;
    }

    public static JsonElement? TryGetFirstArray(ReferenceDataRow? row, params string[] fieldKeys)
    {
        foreach (var fieldKey in fieldKeys)
        {
            if (TryGetArray(row, fieldKey) is JsonElement array)
            {
                return array;
            }
        }

        return null;
    }

    public static JsonElement? TryGetNestedArray(ReferenceDataRow? row, string fieldKey, string nestedFieldKey)
    {
        var root = row is not null
            && row.Values.TryGetValue(fieldKey, out var value)
            && value.ValueKind == JsonValueKind.Object
            ? value
            : (JsonElement?)null;

        return root is null ? null : TryGetArray(root.Value, nestedFieldKey);
    }

    public static int? TryGetArrayCount(ReferenceDataRow? row, string fieldKey)
    {
        return TryGetArray(row, fieldKey)?.GetArrayLength();
    }

    public static IEnumerable<JsonElement> EnumerateObjectArray(ReferenceDataRow? row, string fieldKey)
    {
        return EnumerateObjectArray(TryGetArray(row, fieldKey));
    }

    public static IEnumerable<JsonElement> EnumerateObjectArray(JsonElement? array)
    {
        if (array is null || array.Value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.Value
            .EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.Object);
    }

    public static JsonElement? TryGetValue(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            ? value
            : null;
    }

    public static JsonElement? TryGetObject(JsonElement element, string propertyName)
    {
        var value = TryGetValue(element, propertyName);
        return value?.ValueKind == JsonValueKind.Object ? value : null;
    }

    public static JsonElement? TryGetArray(JsonElement element, string propertyName)
    {
        var value = TryGetValue(element, propertyName);
        return value?.ValueKind == JsonValueKind.Array ? value : null;
    }

    public static string? TryGetString(JsonElement element, string propertyName)
    {
        var value = TryGetValue(element, propertyName);
        return value is null ? null : ToStringValue(value.Value);
    }

    public static string? TryGetSingleLineString(JsonElement element, string propertyName)
    {
        return NormalizeSingleLine(TryGetString(element, propertyName));
    }

    public static long? TryGetLong(JsonElement element, string propertyName)
    {
        return TryGetLong(TryGetValue(element, propertyName));
    }

    public static long? TryGetLong(JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var value = element.Value;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => (long)decimalValue,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            _ => null
        };
    }

    public static long? TryGetLong(object? value)
    {
        return value switch
        {
            JsonElement element => TryGetLong(element),
            long int64Value => int64Value,
            int int32Value => int32Value,
            decimal decimalValue => (long)decimalValue,
            string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue) => parsedValue,
            _ => null
        };
    }

    public static int? TryGetInt(JsonElement element, string propertyName)
    {
        return TryGetInt(TryGetValue(element, propertyName));
    }

    public static int? TryGetInt(JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var value = element.Value;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.Number when value.TryGetInt64(out var number) => checked((int)number),
            JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            _ => null
        };
    }

    public static int? TryGetInt(object? value)
    {
        return value switch
        {
            JsonElement element => TryGetInt(element),
            int int32Value => int32Value,
            long int64Value => checked((int)int64Value),
            decimal decimalValue => checked((int)decimalValue),
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue) => parsedValue,
            _ => null
        };
    }

    public static bool? TryGetBool(JsonElement element, string propertyName)
    {
        return TryGetBool(TryGetValue(element, propertyName));
    }

    public static bool? TryGetBool(JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var value = element.Value;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsedValue) => parsedValue,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number != 0,
            JsonValueKind.Number when value.TryGetInt64(out var number) => number != 0,
            _ => null
        };
    }

    public static bool? TryGetBool(object? value)
    {
        return value switch
        {
            JsonElement element => TryGetBool(element),
            bool boolValue => boolValue,
            string text when bool.TryParse(text, out var parsedValue) => parsedValue,
            string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue) => numericValue != 0,
            long int64Value => int64Value != 0,
            int int32Value => int32Value != 0,
            decimal decimalValue => decimalValue != 0,
            _ => null
        };
    }

    public static ReferenceDataRow ToReferenceDataRow(JsonElement item)
    {
        return new ReferenceDataRow
        {
            Values = item.ValueKind == JsonValueKind.Object
                ? item.EnumerateObject().ToDictionary(
                    static property => property.Name,
                    static property => property.Value,
                    StringComparer.OrdinalIgnoreCase)
                : []
        };
    }

    private static string? ToStringValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            _ => value.ToString()
        };
    }

    private static string? NormalizeSingleLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(
            " ",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
