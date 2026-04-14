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
                DataFilterMatchMode.Equals => BuildSingle($"{apiField}__eq", value),
                DataFilterMatchMode.NotEquals => BuildSingle($"{apiField}__not_eq", value),
                DataFilterMatchMode.LessThan => BuildSingle($"{apiField}__lt", value),
                DataFilterMatchMode.LessThanOrEqual => BuildSingle($"{apiField}__lteq", value),
                DataFilterMatchMode.GreaterThan => BuildSingle($"{apiField}__gt", value),
                DataFilterMatchMode.GreaterThanOrEqual => BuildSingle($"{apiField}__gteq", value),
                DataFilterMatchMode.StartsWith => BuildString($"{apiField}__start", value),
                DataFilterMatchMode.Contains => BuildString($"{apiField}__cont", value),
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

        private static Dictionary<string, object?> BuildString(string key, object value)
        {
            var text = value.ToString();
            return string.IsNullOrWhiteSpace(text)
                ? []
                : new Dictionary<string, object?> { [key] = text };
        }

        private static Dictionary<string, object?> BuildInFilter(string inKey, string nullKey, object value)
        {
            if (value is not IEnumerable<object?> values)
            {
                return [];
            }

            var list = values.ToList();
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
