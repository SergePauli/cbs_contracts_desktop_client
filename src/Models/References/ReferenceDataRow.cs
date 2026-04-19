using System.Text.Json;
using System.Text.Json.Serialization;

namespace CbsContractsDesktopClient.Models.References
{
    public sealed class ReferenceDataRow
    {
        public bool IsPlaceholder { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> Values { get; set; } = [];

        public object? this[string fieldKey] => GetValue(fieldKey);

        public static ReferenceDataRow CreatePlaceholder()
        {
            return new ReferenceDataRow
            {
                IsPlaceholder = true
            };
        }

        public object? GetValue(string fieldKey)
        {
            if (IsPlaceholder)
            {
                return "...";
            }

            if (Values.TryGetValue(fieldKey, out var directValue))
            {
                return ConvertValue(directValue);
            }

            if (string.IsNullOrWhiteSpace(fieldKey) || !fieldKey.Contains('.', StringComparison.Ordinal))
            {
                return null;
            }

            var pathSegments = fieldKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length == 0 || !Values.TryGetValue(pathSegments[0], out var rootValue))
            {
                return null;
            }

            return ResolveNestedValue(rootValue, pathSegments, 1);
        }

        private static object? ConvertValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when value.TryGetInt64(out var number) => number,
                JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
                JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(ConvertArrayItem).Where(static item => !string.IsNullOrWhiteSpace(item))),
                JsonValueKind.Object => value.GetRawText(),
                _ => value.GetRawText()
            };
        }

        private static string? ConvertArrayItem(JsonElement item)
        {
            var value = ConvertValue(item);
            return value?.ToString();
        }

        private static object? ResolveNestedValue(JsonElement current, string[] pathSegments, int index)
        {
            if (index >= pathSegments.Length)
            {
                return ConvertValue(current);
            }

            return current.ValueKind switch
            {
                JsonValueKind.Object when current.TryGetProperty(pathSegments[index], out var childValue)
                    => ResolveNestedValue(childValue, pathSegments, index + 1),
                JsonValueKind.Array => ResolveArrayNestedValue(current, pathSegments, index),
                _ => null
            };
        }

        private static object? ResolveArrayNestedValue(JsonElement array, string[] pathSegments, int index)
        {
            var values = array.EnumerateArray()
                .Select(item => ResolveNestedValue(item, pathSegments, index))
                .Where(HasDisplayValue)
                .ToList();

            if (values.Count == 0)
            {
                return null;
            }

            if (values.Count == 1)
            {
                return values[0];
            }

            return string.Join(", ", values.Select(static value => value!.ToString()));
        }

        private static bool HasDisplayValue(object? value)
        {
            return value switch
            {
                null => false,
                string text => !string.IsNullOrWhiteSpace(text),
                _ => true
            };
        }
    }
}
