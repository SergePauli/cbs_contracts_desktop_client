using System.Text.Json;
using System.Text.Json.Serialization;

namespace CbsContractsDesktopClient.Models.Table
{
    public sealed class TableDataRow : IJsonOnDeserialized
    {
        private Dictionary<string, JsonElement> _values = [];
        private Dictionary<string, object?> _resolvedValues = [];

        public bool IsPlaceholder { get; init; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> Values
        {
            get => _values;
            set
            {
                _values = value ?? [];
                RebuildResolvedValues();
            }
        }

        public IReadOnlyDictionary<string, object?> ResolvedValues => _resolvedValues;

        public object? this[string fieldKey] => GetValue(fieldKey);

        public static TableDataRow CreatePlaceholder()
        {
            return new TableDataRow
            {
                IsPlaceholder = true
            };
        }

        public void RefreshResolvedValues()
        {
            RebuildResolvedValues();
        }

        public object? GetValue(string fieldKey)
        {
            if (IsPlaceholder)
            {
                return "...";
            }

            if (string.IsNullOrWhiteSpace(fieldKey))
            {
                return null;
            }

            EnsureResolvedValues();
            if (_resolvedValues.TryGetValue(fieldKey, out var resolvedValue))
            {
                return resolvedValue;
            }

            if (!fieldKey.Contains('.', StringComparison.Ordinal))
            {
                return null;
            }

            var pathSegments = fieldKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length == 0 || !Values.TryGetValue(pathSegments[0], out var rootValue))
            {
                return null;
            }

            var value = ResolveNestedValue(rootValue, pathSegments, 1);
            _resolvedValues[fieldKey] = value;
            return value;
        }

        void IJsonOnDeserialized.OnDeserialized()
        {
            RebuildResolvedValues();
        }

        private void EnsureResolvedValues()
        {
            if (_resolvedValues.Count == 0 && Values.Count > 0)
            {
                RebuildResolvedValues();
            }
        }

        private void RebuildResolvedValues()
        {
            _resolvedValues = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (key, value) in Values)
            {
                _resolvedValues[key] = ConvertValue(value);
                AddNestedResolvedValues(key, value);
            }
        }

        private void AddNestedResolvedValues(string prefix, JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in value.EnumerateObject())
                    {
                        var key = $"{prefix}.{property.Name}";
                        _resolvedValues.TryAdd(key, ConvertValue(property.Value));
                        AddNestedResolvedValues(key, property.Value);
                    }

                    break;
                case JsonValueKind.Array:
                    AddArrayNestedResolvedValues(prefix, value);
                    break;
            }
        }

        private void AddArrayNestedResolvedValues(string prefix, JsonElement array)
        {
            var groupedValues = new Dictionary<string, List<object?>>(StringComparer.Ordinal);
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var property in item.EnumerateObject())
                {
                    var key = $"{prefix}.{property.Name}";
                    if (!groupedValues.TryGetValue(key, out var values))
                    {
                        values = [];
                        groupedValues[key] = values;
                    }

                    values.Add(ConvertValue(property.Value));
                }
            }

            foreach (var (key, values) in groupedValues)
            {
                var displayValues = values
                    .Where(HasDisplayValue)
                    .Select(static value => value!.ToString())
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .ToList();

                _resolvedValues.TryAdd(key, displayValues.Count switch
                {
                    0 => null,
                    1 => displayValues[0],
                    _ => string.Join(", ", displayValues)
                });
            }
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
