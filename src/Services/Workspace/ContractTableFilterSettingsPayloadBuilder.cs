using System.Text.Json;
using System.Text.Json.Nodes;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.Workspace
{
    public sealed record ContractTableFilterSettingsPayload(
        IReadOnlyDictionary<string, object?> Payload,
        string StatusesJson,
        bool HasChanges);

    public static class ContractTableFilterSettingsPayloadBuilder
    {
        public static ContractTableFilterSettingsPayload Build(
            int? profileId,
            string? currentStatusesJson,
            IReadOnlyList<DataFilterCriterion> filters,
            IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>> optionsSources)
        {
            if (profileId is null)
            {
                throw new InvalidOperationException("Profile id is missing for contract filter settings save.");
            }

            var statusesNode = ParseObjectOrEmpty(currentStatusesJson);
            statusesNode["c_statuses"] = SerializeContractStatuses(filters, optionsSources);
            statusesNode["c_funded"] = SerializeFundedFilter(filters);

            var statusesJson = statusesNode.ToJsonString();
            var hasChanges = !string.Equals(statusesJson, currentStatusesJson, StringComparison.Ordinal);

            return new ContractTableFilterSettingsPayload(
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = profileId.Value,
                    ["settings"] = statusesJson
                },
                statusesJson,
                hasChanges);
        }

        private static JsonObject ParseObjectOrEmpty(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            try
            {
                return JsonNode.Parse(json) as JsonObject ?? [];
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("User statuses settings contain invalid JSON.");
            }
        }

        private static string? SerializeContractStatuses(
            IReadOnlyList<DataFilterCriterion> filters,
            IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>> optionsSources)
        {
            var values = FindSelectedValues(filters, "status");
            if (values.Count == 0)
            {
                return null;
            }

            optionsSources.TryGetValue("ContractStatus", out var options);
            var array = new JsonArray();
            foreach (var value in values)
            {
                var option = options?.FirstOrDefault(option => AreFilterValuesEqual(option.Value, value));
                array.Add(new JsonObject
                {
                    ["id"] = value is null ? null : JsonValue.Create(Convert.ToInt64(value)),
                    ["name"] = option?.Label ?? (value is null ? "Пустой" : value.ToString())
                });
            }

            return array.ToJsonString();
        }

        private static string? SerializeFundedFilter(IReadOnlyList<DataFilterCriterion> filters)
        {
            var filter = filters.FirstOrDefault(static filter =>
                string.Equals(filter.FieldKey, "is_funded", StringComparison.OrdinalIgnoreCase));
            if (filter?.Value is null)
            {
                return null;
            }

            return filter.Value switch
            {
                string text when string.Equals(text, "null", StringComparison.OrdinalIgnoreCase) => "'null'",
                bool boolValue => boolValue ? "true" : "false",
                _ => filter.Value.ToString()
            };
        }

        private static IReadOnlyList<object?> FindSelectedValues(
            IReadOnlyList<DataFilterCriterion> filters,
            string fieldKey)
        {
            var filter = filters.FirstOrDefault(filter =>
                string.Equals(filter.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));
            if (filter?.Value is null)
            {
                return [];
            }

            if (filter.Value is string)
            {
                return [filter.Value];
            }

            return filter.Value is System.Collections.IEnumerable values
                ? values.Cast<object?>().ToList()
                : [filter.Value];
        }

        private static bool AreFilterValuesEqual(object? left, object? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            return TryConvertLong(left, out var leftId) && TryConvertLong(right, out var rightId)
                ? leftId == rightId
                : Equals(left, right);
        }

        private static bool TryConvertLong(object value, out long result)
        {
            result = default;
            return value switch
            {
                long longValue => SetResult(longValue, out result),
                int intValue => SetResult(intValue, out result),
                decimal decimalValue => SetResult((long)decimalValue, out result),
                string text when long.TryParse(text, out var longValue) => SetResult(longValue, out result),
                _ => false
            };
        }

        private static bool SetResult(long value, out long result)
        {
            result = value;
            return true;
        }
    }
}
