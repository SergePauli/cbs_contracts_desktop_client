using System.Text.Json;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Models.Data;

namespace CbsContractsDesktopClient.Services.Workspace
{
    public sealed record ContractTableFilterDefaults(
        IReadOnlyList<long?>? StatusIds,
        object? IsFunded)
    {
        public bool HasValues =>
            StatusIds is { Count: > 0 }
            || IsFunded is not null;

        public IReadOnlyList<DataFilterCriterion> ToCriteria()
        {
            var filters = new List<DataFilterCriterion>();

            if (StatusIds is { Count: > 0 })
            {
                filters.Add(new DataFilterCriterion
                {
                    FieldKey = "status",
                    FilterMode = DataFilterMode.Numeric,
                    MatchMode = DataFilterMatchMode.In,
                    Value = StatusIds
                });
            }

            if (IsFunded is not null)
            {
                filters.Add(new DataFilterCriterion
                {
                    FieldKey = "is_funded",
                    FilterMode = DataFilterMode.Text,
                    MatchMode = DataFilterMatchMode.Equals,
                    Value = IsFunded
                });
            }

            return filters;
        }
    }

    public static class ContractTableFilterDefaultsReader
    {
        public static ContractTableFilterDefaults FromUser(User? user)
        {
            if (user is null)
            {
                return new ContractTableFilterDefaults(null, null);
            }

            return new ContractTableFilterDefaults(
                StatusIds: TryReadStatusIds(user.Statuses),
                IsFunded: TryReadFundedValue(user.Statuses));
        }

        public static string BuildTrace(User? user, ContractTableFilterDefaults defaults)
        {
            return "CONTRACT FILTER DEFAULTS "
                + $"user.statuses={FormatRaw(user?.Statuses)} "
                + $"parsed.c_statuses={FormatList(defaults.StatusIds)} "
                + $"parsed.c_funded={FormatValue(defaults.IsFunded)}";
        }

        private static IReadOnlyList<long?>? TryReadStatusIds(string? statuses)
        {
            if (!TryReadSettingsString(statuses, "c_statuses", out var rawStatuses)
                || string.IsNullOrWhiteSpace(rawStatuses))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(rawStatuses);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var values = new List<long?>();
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    if (TryReadStatusId(element, out var statusId))
                    {
                        values.Add(statusId);
                    }
                }

                return values.Count == 0 ? null : values;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static object? TryReadFundedValue(string? statuses)
        {
            if (!TryReadSettingsString(statuses, "c_funded", out var rawFunded)
                || string.IsNullOrWhiteSpace(rawFunded)
                || string.Equals(rawFunded, "null", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.Equals(rawFunded, "'null'", StringComparison.Ordinal))
            {
                return "null";
            }

            try
            {
                using var document = JsonDocument.Parse(rawFunded);
                return document.RootElement.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => document.RootElement.GetString(),
                    JsonValueKind.Number when document.RootElement.TryGetInt64(out var longValue) => longValue,
                    _ => rawFunded
                };
            }
            catch (JsonException)
            {
                return rawFunded;
            }
        }

        private static bool TryReadSettingsString(string? settingsJson, string propertyName, out string? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(settingsJson))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(settingsJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object
                    || !document.RootElement.TryGetProperty(propertyName, out var property)
                    || property.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                value = property.GetString();
                return !string.IsNullOrWhiteSpace(value);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryReadStatusId(JsonElement element, out long? statusId)
        {
            statusId = null;
            if (element.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (element.ValueKind == JsonValueKind.Object)
            {
                if (!element.TryGetProperty("id", out var idElement))
                {
                    return false;
                }

                if (idElement.ValueKind == JsonValueKind.Null)
                {
                    return true;
                }

                if (ReadLong(idElement) is long id)
                {
                    statusId = id;
                    return true;
                }

                return false;
            }

            if (ReadLong(element) is long directId)
            {
                statusId = directId;
                return true;
            }

            return false;
        }

        private static long? ReadLong(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.String when long.TryParse(element.GetString(), out var longValue) => longValue,
                _ => null
            };
        }

        private static string FormatRaw(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<null>" : value;
        }

        private static string FormatList<T>(IReadOnlyList<T>? values)
        {
            return values is null
                ? "<null>"
                : "[" + string.Join(",", values.Select(static value => value?.ToString() ?? "null")) + "]";
        }

        private static string FormatValue(object? value)
        {
            return value?.ToString() ?? "<null>";
        }
    }
}
