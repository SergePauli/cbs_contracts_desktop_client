using System.Text.Json;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Models.Data;

namespace CbsContractsDesktopClient.Services.Workspace
{
    public sealed record StageTableFilterDefaults(
        IReadOnlyList<long?>? StatusIds,
        object? IsFunded,
        IReadOnlyList<long>? TaskKindIds)
    {
        public bool HasValues =>
            StatusIds is { Count: > 0 }
            || IsFunded is not null
            || TaskKindIds is { Count: > 0 };

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

            if (TaskKindIds is { Count: > 0 })
            {
                filters.Add(new DataFilterCriterion
                {
                    FieldKey = "task",
                    FilterMode = DataFilterMode.Numeric,
                    MatchMode = DataFilterMatchMode.In,
                    Value = TaskKindIds
                });
            }

            return filters;
        }
    }

    public static class StageTableFilterDefaultsReader
    {
        public static StageTableFilterDefaults FromUser(User? user)
        {
            if (user is null)
            {
                return new StageTableFilterDefaults(null, null, null);
            }

            return new StageTableFilterDefaults(
                StatusIds: TryReadStatusIds(user.Statuses),
                IsFunded: TryReadFundedValue(user.Statuses),
                TaskKindIds: TryReadTaskKindIds(user.ContractsTypes));
        }

        public static string BuildTrace(User? user, StageTableFilterDefaults defaults)
        {
            return "STAGE FILTER DEFAULTS "
                + $"user.statuses={FormatRaw(user?.Statuses)} "
                + $"user.contracts_types={FormatRaw(user?.ContractsTypes)} "
                + $"parsed.s_statuses={FormatList(defaults.StatusIds)} "
                + $"parsed.s_funded={FormatValue(defaults.IsFunded)} "
                + $"parsed.tasks={FormatList(defaults.TaskKindIds)}";
        }

        private static IReadOnlyList<long?>? TryReadStatusIds(string? statuses)
        {
            if (!TryReadSettingsString(statuses, "s_statuses", out var rawStatuses)
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
            if (!TryReadSettingsString(statuses, "s_funded", out var rawFunded)
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

        private static IReadOnlyList<long>? TryReadTaskKindIds(string? contractsTypes)
        {
            if (string.IsNullOrWhiteSpace(contractsTypes))
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(contractsTypes);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var values = document.RootElement
                    .EnumerateArray()
                    .Select(ReadLong)
                    .Where(static value => value.HasValue)
                    .Select(static value => value!.Value)
                    .Distinct()
                    .Order()
                    .ToList();

                return values.Count == 0 ? null : values;
            }
            catch (JsonException)
            {
                return null;
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

        private static long? ReadLong(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.String when long.TryParse(element.GetString(), out var longValue) => longValue,
                _ => null
            };
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
