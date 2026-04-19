using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CbsContractsDesktopClient.Models.Table
{
    public sealed class CbsTableMultiSelectFilterValue
    {
        public required IReadOnlyList<CbsTableFilterOptionDefinition> Options { get; init; }

        public required IReadOnlyList<object?> SelectedValues { get; init; }

        public IReadOnlyList<CbsTableFilterOptionDefinition> SelectedOptions =>
            Options.Where(static option => option.Value is not null)
                .Join(
                    SelectedValues,
                    static option => option.Value,
                    static value => value,
                    static (option, _) => option)
                .ToList();

        public static CbsTableMultiSelectFilterValue Create(
            IEnumerable? options,
            IEnumerable? selectedValues)
        {
            var normalizedOptions = new List<CbsTableFilterOptionDefinition>();

            if (options is not null)
            {
                foreach (var option in options)
                {
                    if (TryNormalizeOption(option, out var normalizedOption))
                    {
                        normalizedOptions.Add(normalizedOption);
                    }
                }
            }

            return new CbsTableMultiSelectFilterValue
            {
                Options = normalizedOptions,
                SelectedValues = selectedValues?.Cast<object?>().ToArray() ?? []
            };
        }

        private static bool TryNormalizeOption(object? option, out CbsTableFilterOptionDefinition normalizedOption)
        {
            switch (option)
            {
                case null:
                    normalizedOption = default!;
                    return false;
                case CbsTableFilterOptionDefinition definition:
                    normalizedOption = new CbsTableFilterOptionDefinition
                    {
                        Value = definition.Value,
                        Label = definition.Label
                    };
                    return true;
                case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                    return TryNormalizeDictionary(readOnlyDictionary, out normalizedOption);
                case IDictionary<string, object?> dictionary:
                    return TryNormalizeDictionary(dictionary, out normalizedOption);
                case IDictionary nonGenericDictionary:
                    return TryNormalizeDictionary(nonGenericDictionary, out normalizedOption);
                default:
                    return TryNormalizeObject(option, out normalizedOption);
            }
        }

        private static bool TryNormalizeDictionary(
            IEnumerable<KeyValuePair<string, object?>> dictionary,
            out CbsTableFilterOptionDefinition normalizedOption)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var map = dictionary.ToDictionary(static pair => pair.Key, static pair => pair.Value, comparer);

            if (!map.TryGetValue("id", out var value) && !map.TryGetValue("value", out value))
            {
                normalizedOption = default!;
                return false;
            }

            if (!map.TryGetValue("name", out var label) && !map.TryGetValue("label", out label))
            {
                normalizedOption = default!;
                return false;
            }

            normalizedOption = new CbsTableFilterOptionDefinition
            {
                Value = value,
                Label = label?.ToString() ?? string.Empty
            };
            return true;
        }

        private static bool TryNormalizeDictionary(
            IDictionary dictionary,
            out CbsTableFilterOptionDefinition normalizedOption)
        {
            var pairs = new List<KeyValuePair<string, object?>>();

            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key is string key)
                {
                    pairs.Add(new KeyValuePair<string, object?>(key, entry.Value));
                }
            }

            return TryNormalizeDictionary(pairs, out normalizedOption);
        }

        private static bool TryNormalizeObject(object option, out CbsTableFilterOptionDefinition normalizedOption)
        {
            var type = option.GetType();
            var idProperty = FindReadableProperty(type, "Id") ?? FindReadableProperty(type, "Value");
            var nameProperty = FindReadableProperty(type, "Name") ?? FindReadableProperty(type, "Label");

            if (idProperty is null || nameProperty is null)
            {
                normalizedOption = default!;
                return false;
            }

            normalizedOption = new CbsTableFilterOptionDefinition
            {
                Value = idProperty.GetValue(option),
                Label = nameProperty.GetValue(option)?.ToString() ?? string.Empty
            };
            return true;
        }

        private static PropertyInfo? FindReadableProperty(Type type, string name)
        {
            return type.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        }
    }
}
