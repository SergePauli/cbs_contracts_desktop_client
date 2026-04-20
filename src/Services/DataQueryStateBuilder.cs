using System.Collections;
using System.Linq;
using System.Globalization;
using CbsContractsDesktopClient.Models.Data;

namespace CbsContractsDesktopClient.Services
{
    public static class DataQueryStateBuilder
    {
        public static object? BuildFilters(
            IEnumerable<DataFilterCriterion> filters,
            IReadOnlyDictionary<string, string> fieldMap)
        {
            Dictionary<string, object?>? result = null;

            foreach (var filter in filters)
            {
                if (!fieldMap.TryGetValue(filter.FieldKey, out var apiField))
                {
                    continue;
                }

                var fragment = BuildFilterFragment(apiField, filter);
                if (fragment.Count == 0)
                {
                    continue;
                }

                result ??= [];
                foreach (var pair in fragment)
                {
                    result[pair.Key] = pair.Value;
                }
            }

            return result;
        }

        public static IReadOnlyList<string>? BuildSorts(
            IEnumerable<DataSortCriterion> sorts,
            IReadOnlyDictionary<string, string> fieldMap)
        {
            var result = new List<string>();

            foreach (var sort in sorts)
            {
                if (!fieldMap.TryGetValue(sort.FieldKey, out var apiField))
                {
                    continue;
                }

                var direction = sort.Direction == DataSortDirection.Ascending ? "asc" : "desc";
                result.Add($"{apiField} {direction}");
            }

            return result.Count == 0 ? null : result;
        }

        private static Dictionary<string, object?> BuildFilterFragment(string apiField, DataFilterCriterion filter)
        {
            var value = filter.Value;
            if (value is null)
            {
                return [];
            }

            return filter.MatchMode switch
            {
                DataFilterMatchMode.Equals => BuildFilterValue(filter, $"{apiField}__eq", value),
                DataFilterMatchMode.NotEquals => BuildSingle($"{apiField}__not_eq", value),
                DataFilterMatchMode.LessThan => BuildFilterValue(filter, $"{apiField}__lt", value),
                DataFilterMatchMode.LessThanOrEqual => BuildFilterValue(filter, $"{apiField}__lte", value),
                DataFilterMatchMode.GreaterThan => BuildFilterValue(filter, $"{apiField}__gt", value),
                DataFilterMatchMode.GreaterThanOrEqual => BuildFilterValue(filter, $"{apiField}__gte", value),
                DataFilterMatchMode.StartsWith => BuildString($"{apiField}__start", value),
                DataFilterMatchMode.Contains => BuildString($"{apiField}__cnt", value),
                DataFilterMatchMode.EndsWith => BuildString($"{apiField}__end", value),
                DataFilterMatchMode.NotContains => BuildString($"{apiField}__not_cont", value),
                DataFilterMatchMode.In => BuildInFilter($"{apiField}__in", $"{apiField}__null", value),
                _ => []
            };
        }

        private static Dictionary<string, object?> BuildSingle(string key, object value)
        {
            return new Dictionary<string, object?> { [key] = value };
        }

        private static Dictionary<string, object?> BuildFilterValue(DataFilterCriterion filter, string key, object value)
        {
            if (filter.FilterMode == DataFilterMode.Numeric)
            {
                return BuildNumeric(key, value);
            }

            if (filter.FilterMode == DataFilterMode.DateTime)
            {
                return BuildDateTime(key, value);
            }

            return BuildSingle(key, value);
        }

        private static Dictionary<string, object?> BuildString(string key, object value)
        {
            var text = value.ToString();
            return string.IsNullOrWhiteSpace(text)
                ? []
                : new Dictionary<string, object?> { [key] = text };
        }

        private static Dictionary<string, object?> BuildNumeric(string key, object value)
        {
            if (TryConvertNumeric(value, out var numericValue))
            {
                return new Dictionary<string, object?> { [key] = numericValue };
            }

            return [];
        }

        private static Dictionary<string, object?> BuildDateTime(string key, object value)
        {
            if (TryConvertDateTime(value, out var dateTimeValue))
            {
                return new Dictionary<string, object?> { [key] = dateTimeValue };
            }

            return [];
        }

        private static bool TryConvertNumeric(object value, out object numericValue)
        {
            switch (value)
            {
                case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    numericValue = value;
                    return true;
                case string text when !string.IsNullOrWhiteSpace(text):
                    if (decimal.TryParse(text, out var decimalValue))
                    {
                        numericValue = decimalValue;
                        return true;
                    }

                    if (decimal.TryParse(
                        text,
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out decimalValue))
                    {
                        numericValue = decimalValue;
                        return true;
                    }

                    break;
            }

            numericValue = default!;
            return false;
        }

        private static bool TryConvertDateTime(object value, out string dateTimeValue)
        {
            switch (value)
            {
                case DateTimeOffset dateTimeOffset:
                    dateTimeValue = dateTimeOffset.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
                    return true;
                case DateTime dateTime:
                    dateTimeValue = dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
                    return true;
                case string text when !string.IsNullOrWhiteSpace(text):
                    if (DateTimeOffset.TryParse(
                        text,
                        CultureInfo.CurrentCulture,
                        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                        out var parsedOffset))
                    {
                        dateTimeValue = parsedOffset.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
                        return true;
                    }

                    if (DateTime.TryParse(
                        text,
                        CultureInfo.CurrentCulture,
                        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                        out var parsedDateTime))
                    {
                        dateTimeValue = parsedDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
                        return true;
                    }

                    if (DateTimeOffset.TryParse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                        out parsedOffset))
                    {
                        dateTimeValue = parsedOffset.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
                        return true;
                    }

                    if (DateTime.TryParse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                        out parsedDateTime))
                    {
                        dateTimeValue = parsedDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
                        return true;
                    }

                    break;
            }

            dateTimeValue = string.Empty;
            return false;
        }

        private static Dictionary<string, object?> BuildInFilter(string inKey, string nullKey, object value)
        {
            if (value is string || value is not IEnumerable values)
            {
                return [];
            }

            var list = values.Cast<object?>().ToList();
            if (list.Count == 0)
            {
                return [];
            }

            if (list.Contains(null))
            {
                var nonNull = list.Where(static item => item is not null).ToArray();
                var group = new Dictionary<string, object?> { ["m"] = "or", [nullKey] = true };
                if (nonNull.Length > 0)
                {
                    group[inKey] = nonNull;
                }

                return new Dictionary<string, object?> { ["g"] = new[] { group } };
            }

            return new Dictionary<string, object?> { [inKey] = list };
        }
    }
}
