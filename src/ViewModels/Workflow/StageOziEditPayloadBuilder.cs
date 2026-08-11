using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
namespace CbsContractsDesktopClient.ViewModels.Workflow;

public sealed record StageOziEditPayloadInput(
    long Id,
    string? ListKey,
    long? StatusId,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? RideOutAt,
    DateTimeOffset? SendedAt,
    DateTimeOffset? ClosedAt,
    bool? IsRideOut,
    bool? IsSended,
    int? RegistryQuarter,
    int? RegistryYear,
    IReadOnlyList<StagePerformerEditState> SelectedPerformers,
    string? Comment,
    int? ProfileId);

public static class StageOziEditPayloadBuilder
{
    public static IReadOnlyDictionary<string, object?> BuildForUpdate(
        StageEditState state,
        IReadOnlyList<StagePerformerEditState> selectedPerformers,
        string? comment,
        int? profileId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(selectedPerformers);

        var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = state.Id
        };

        if (!string.IsNullOrWhiteSpace(state.ListKey))
        {
            request["list_key"] = state.ListKey;
        }

        AppendChangedLong(request, "status_id", state.Original.Status.Id, state.Status.Id);
        AppendChangedDate(request, "completed_at", state.Original.CompletedAt, state.CompletedAt);
        AppendChangedDate(request, "ride_out_at", state.Original.RideOutAt, state.RideOutAt);
        AppendChangedDate(request, "sended_at", state.Original.SendedAt, state.SendedAt);
        AppendChangedDate(request, "closed_at", state.Original.ClosedAt, state.ClosedAt);
        AppendChangedBool(request, "is_ride_out", state.Original.IsRideOut, state.IsRideOut);
        AppendChangedBool(request, "is_sended", state.Original.IsSended, state.IsSended);
        AppendChangedInt(request, "registry_quarter", state.Original.RegistryQuarter, state.RegistryQuarter);
        AppendChangedInt(request, "registry_year", state.Original.RegistryYear, state.RegistryYear);

        var performersDelta = BuildPerformerAttributesDelta(state.Original.Performers, selectedPerformers);
        if (performersDelta.Count > 0)
        {
            request["performers_attributes"] = performersDelta;
        }

        StageEditPayloadBuilderHelpers.AppendCommentAttributes(request, comment, profileId);
        return request;
    }

    private static IReadOnlyList<Dictionary<string, object?>> BuildPerformerAttributesDelta(
        IReadOnlyList<StagePerformerEditState> originalPerformers,
        IReadOnlyList<StagePerformerEditState> selectedPerformers)
    {
        var originalEmployeeIds = originalPerformers
            .Where(static performer => performer.EmployeeId is not null)
            .Select(static performer => performer.EmployeeId!.Value)
            .ToHashSet();
        var selectedEmployeeIds = selectedPerformers
            .Where(static performer => performer.EmployeeId is not null)
            .Select(static performer => performer.EmployeeId!.Value)
            .ToHashSet();
        if (originalEmployeeIds.SetEquals(selectedEmployeeIds))
        {
            return [];
        }

        var originalByEmployeeId = originalPerformers
            .Where(static performer => performer.EmployeeId is not null)
            .ToDictionary(static performer => performer.EmployeeId!.Value);
        var selectedByEmployeeId = selectedPerformers
            .Where(static performer => performer.EmployeeId is not null)
            .DistinctBy(static performer => performer.EmployeeId!.Value)
            .ToList();
        var result = new List<Dictionary<string, object?>>();
        var priority = 0;
        foreach (var performer in selectedByEmployeeId)
        {
            var employeeId = performer.EmployeeId!.Value;
            if (originalByEmployeeId.TryGetValue(employeeId, out var original)
                && original.Priority == priority
                && string.Equals(original.Name, performer.Name, StringComparison.Ordinal))
            {
                priority++;
                continue;
            }

            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["list_key"] = string.IsNullOrWhiteSpace(performer.ListKey)
                    ? Guid.NewGuid().ToString()
                    : performer.ListKey,
                ["employee_id"] = employeeId,
                ["name"] = performer.Name,
                ["priority"] = priority
            };

            if (performer.Id is not null)
            {
                payload["id"] = performer.Id;
            }

            result.Add(payload);
            priority++;
        }

        result.AddRange(originalPerformers
            .Where(performer => performer.EmployeeId is long employeeId && !selectedEmployeeIds.Contains(employeeId))
            .Select(static performer =>
            {
                var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["_destroy"] = "1",
                    ["priority"] = performer.Priority ?? 0
                };
                if (performer.Id is not null)
                {
                    payload["id"] = performer.Id;
                }

                if (!string.IsNullOrWhiteSpace(performer.ListKey))
                {
                    payload["list_key"] = performer.ListKey;
                }

                if (performer.EmployeeId is not null)
                {
                    payload["employee_id"] = performer.EmployeeId;
                }

                return payload;
            }));

        return result;
    }

    private static void AppendChangedLong(
        IDictionary<string, object?> request,
        string key,
        long? originalValue,
        long? value)
    {
        if (originalValue != value)
        {
            request[key] = value;
        }
    }

    private static void AppendChangedInt(
        IDictionary<string, object?> request,
        string key,
        int? originalValue,
        int? value)
    {
        if (originalValue != value)
        {
            request[key] = value;
        }
    }

    private static void AppendChangedBool(
        IDictionary<string, object?> request,
        string key,
        bool? originalValue,
        bool? value)
    {
        if (originalValue != value)
        {
            request[key] = value;
        }
    }

    private static void AppendChangedDate(
        IDictionary<string, object?> request,
        string key,
        DateTimeOffset? originalValue,
        DateTimeOffset? value)
    {
        var originalDate = ToDateOnly(originalValue);
        var currentDate = ToDateOnly(value);
        if (originalDate != currentDate)
        {
            request[key] = Shared.Formatting.AppFormatters.FormatDate(value);
        }
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
    {
        return value is null
            ? null
            : DateOnly.FromDateTime(value.Value.Date);
    }
}
