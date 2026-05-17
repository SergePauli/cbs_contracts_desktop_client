using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Shared.Data;
using static CbsContractsDesktopClient.Shared.Formatting.AppFormatters;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public sealed record StageCommerEditPayloadInput(
        long Id,
        string? ListKey,
        long? StatusId,
        string? DeadlineKind,
        DateTimeOffset? DeadlineAt,
        DateTimeOffset? StartAt,
        string? PaymentDeadlineKind,
        DateTimeOffset? PaymentDeadlineAt,
        int? Duration,
        int? PaymentDuration,
        DateTimeOffset? ClosedAt,
        IReadOnlyCollection<long> SelectedTaskKindIds,
        string? Comment,
        int? ProfileId);

    public static class StageCommerEditPayloadBuilder
    {
        public static IReadOnlyDictionary<string, object?> BuildForUpdate(
            ReferenceDataRow sourceRow,
            StageCommerEditPayloadInput input)
        {
            ArgumentNullException.ThrowIfNull(sourceRow);
            ArgumentNullException.ThrowIfNull(input);

            var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = input.Id
            };

            if (!string.IsNullOrWhiteSpace(input.ListKey))
            {
                request["list_key"] = input.ListKey;
            }

            AppendChangedLong(request, sourceRow, "status_id", input.StatusId, "status.id");
            AppendChangedText(request, sourceRow, "deadline_kind", input.DeadlineKind);
            AppendChangedDate(request, sourceRow, "deadline_at", input.DeadlineAt);
            AppendChangedDate(request, sourceRow, "start_at", input.StartAt);
            AppendChangedText(request, sourceRow, "payment_deadline_kind", input.PaymentDeadlineKind);
            AppendChangedDate(request, sourceRow, "payment_deadline_at", input.PaymentDeadlineAt);
            AppendChangedInt(request, sourceRow, "duration", input.Duration);
            AppendChangedInt(request, sourceRow, "payment_duration", input.PaymentDuration);
            AppendChangedDate(request, sourceRow, "closed_at", input.ClosedAt);

            var tasksDelta = BuildTaskAttributesDelta(sourceRow, input.SelectedTaskKindIds);
            if (tasksDelta.Count > 0)
            {
                request["tasks_attributes"] = tasksDelta;
            }

            var comment = input.Comment?.Trim();
            if (!string.IsNullOrWhiteSpace(comment) && input.ProfileId is int profileId)
            {
                request["comments_attributes"] = new[]
                {
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["content"] = comment,
                        ["profile_id"] = profileId
                    }
                };
            }

            return request;
        }

        public static IReadOnlyDictionary<string, object?> BuildContractClosePayload(
            long contractId,
            DateTimeOffset? closedAt)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = contractId,
                ["status_id"] = 5L,
                ["closed_at"] = FormatDate(closedAt)
            };
        }

        private static void AppendChangedText(
            IDictionary<string, object?> request,
            ReferenceDataRow sourceRow,
            string key,
            string? value)
        {
            var normalizedValue = string.IsNullOrWhiteSpace(value) ? null : value;
            var originalValue = sourceRow.GetValue(key)?.ToString();
            originalValue = string.IsNullOrWhiteSpace(originalValue) ? null : originalValue;
            if (!string.Equals(originalValue, normalizedValue, StringComparison.Ordinal))
            {
                request[key] = normalizedValue;
            }
        }

        private static void AppendChangedInt(
            IDictionary<string, object?> request,
            ReferenceDataRow sourceRow,
            string key,
            int? value)
        {
            var originalValue = TryGetLong(sourceRow.GetValue(key)) is long number ? (int?)number : null;
            if (originalValue != value)
            {
                request[key] = value;
            }
        }

        private static void AppendChangedLong(
            IDictionary<string, object?> request,
            ReferenceDataRow sourceRow,
            string key,
            long? value,
            string fallbackKey)
        {
            var originalValue = TryGetLong(sourceRow.GetValue(key))
                ?? TryGetLong(sourceRow.GetValue(fallbackKey));
            if (originalValue != value)
            {
                request[key] = value;
            }
        }

        private static void AppendChangedDate(
            IDictionary<string, object?> request,
            ReferenceDataRow sourceRow,
            string key,
            DateTimeOffset? value)
        {
            var originalValue = ToDateOnly(ParseDate(sourceRow.GetValue(key)));
            var currentValue = ToDateOnly(value);
            if (originalValue != currentValue)
            {
                request[key] = FormatDate(value);
            }
        }

        private static DateOnly? ToDateOnly(DateTimeOffset? value)
        {
            return value is null
                ? null
                : DateOnly.FromDateTime(value.Value.Date);
        }

        private static IReadOnlyList<Dictionary<string, object?>> BuildTaskAttributesDelta(
            ReferenceDataRow sourceRow,
            IReadOnlyCollection<long> selectedTaskKindIds)
        {
            var selectedKinds = selectedTaskKindIds.ToHashSet();
            var originalTasks = ReadStageTasks(sourceRow);

            var originalKinds = originalTasks
                .Select(static item => item.TaskKindId)
                .Where(static id => id is not null)
                .Select(static id => id!.Value)
                .ToHashSet();

            var added = selectedKinds
                .Where(kind => !originalKinds.Contains(kind))
                .Select(kind => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["list_key"] = Guid.NewGuid().ToString(),
                    ["task_kind_id"] = kind
                });

            var removed = originalTasks
                .Where(task => task.TaskKindId is long kind && !selectedKinds.Contains(kind))
                .Select(task =>
                {
                    var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["_destroy"] = "1"
                    };
                    if (task.Id is not null)
                    {
                        payload["id"] = task.Id;
                    }

                    if (!string.IsNullOrWhiteSpace(task.ListKey))
                    {
                        payload["list_key"] = task.ListKey;
                    }

                    return payload;
                });

            return added.Concat(removed).ToList();
        }

        private static IReadOnlyList<StageTaskRecord> ReadStageTasks(ReferenceDataRow row)
        {
            return JsonDataReader.EnumerateObjectArray(row, "tasks")
                .Select(static item => new StageTaskRecord(
                    Id: JsonDataReader.TryGetLong(item, "id"),
                    ListKey: JsonDataReader.TryGetString(item, "list_key"),
                    TaskKindId: JsonDataReader.TryGetLong(item, "task_kind_id")))
                .ToList();
        }

        private sealed record StageTaskRecord(long? Id, string? ListKey, long? TaskKindId);
    }
}
