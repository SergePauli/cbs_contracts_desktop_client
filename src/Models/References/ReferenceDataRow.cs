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
                return "…";
            }

            return Values.TryGetValue(fieldKey, out var value)
                ? ConvertValue(value)
                : null;
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
    }
}
