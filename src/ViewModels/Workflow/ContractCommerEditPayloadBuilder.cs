using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Formatting;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow;

public sealed record ContractCommerEditPayloadInput(
    bool IsCreateMode,
    long? Id,
    string? ListKey,
    long? TaskKindId,
    string? Code,
    int? Year,
    long? Order,
    long? ContragentId,
    long? StatusId,
    DateTimeOffset? SignedAt,
    string? Comment,
    bool Governmental,
    string? ExternalNumber,
    DateTimeOffset? DeadlineAt,
    DateTimeOffset? ClosedAt,
    int? ProfileId);

public static class ContractCommerEditPayloadBuilder
{
    public static IReadOnlyDictionary<string, object?> BuildCommentUpdate(
        long contractId,
        string? listKey,
        string comment,
        int profileId)
    {
        var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = contractId
        };
        if (!string.IsNullOrWhiteSpace(listKey))
        {
            request["list_key"] = listKey;
        }

        AppendCommentAttributes(request, comment, profileId);
        return request;
    }

    public static IReadOnlyDictionary<string, object?> Build(
        TableDataRow sourceRow,
        ContractEditState contractState,
        ContractCommerEditPayloadInput input,
        IReadOnlyList<StageEditState> stages,
        IReadOnlyList<RevisionEditState> revisions)
    {
        ArgumentNullException.ThrowIfNull(sourceRow);
        ArgumentNullException.ThrowIfNull(contractState);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentNullException.ThrowIfNull(revisions);

        var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!input.IsCreateMode)
        {
            request["id"] = input.Id ?? throw new InvalidOperationException("Contract update payload must contain id.");
            if (!string.IsNullOrWhiteSpace(input.ListKey))
            {
                request["list_key"] = input.ListKey;
            }
        }

        AppendScalarFields(request, sourceRow, input);
        AppendStageAttributes(request, stages, input.IsCreateMode, input.ProfileId);
        AppendRevisionAttributes(request, revisions, input.IsCreateMode);
        AppendContractResponsibleAttributes(request, contractState);
        AppendCommentAttributes(request, input.Comment, input.ProfileId);

        return request;
    }

    private static void AppendContractResponsibleAttributes(
        IDictionary<string, object?> request,
        ContractEditState contractState)
    {
        var originalEmployeeIds = contractState.Original.ContractResponsibles
            .Select(static responsible => responsible.EmployeeId)
            .ToHashSet();
        var selectedEmployeeIds = contractState.ContractResponsibles
            .Select(static responsible => responsible.EmployeeId)
            .ToHashSet();
        if (originalEmployeeIds.SetEquals(selectedEmployeeIds))
        {
            return;
        }

        var originalByEmployeeId = contractState.Original.ContractResponsibles
            .ToDictionary(static responsible => responsible.EmployeeId);
        var attributes = new List<Dictionary<string, object?>>();
        foreach (var responsible in contractState.ContractResponsibles.DistinctBy(static item => item.EmployeeId))
        {
            if (originalByEmployeeId.ContainsKey(responsible.EmployeeId))
            {
                continue;
            }

            attributes.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["list_key"] = string.IsNullOrWhiteSpace(responsible.ListKey)
                    ? Guid.NewGuid().ToString()
                    : responsible.ListKey,
                ["employee_id"] = responsible.EmployeeId
            });
        }

        attributes.AddRange(contractState.Original.ContractResponsibles
            .Where(responsible => !selectedEmployeeIds.Contains(responsible.EmployeeId))
            .Select(static responsible =>
            {
                var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = responsible.Id,
                    ["_destroy"] = "1"
                };
                if (!string.IsNullOrWhiteSpace(responsible.ListKey))
                {
                    payload["list_key"] = responsible.ListKey;
                }

                return payload;
            }));

        request["contract_responsibles_attributes"] = attributes;
    }

    private static void AppendScalarFields(
        IDictionary<string, object?> request,
        TableDataRow sourceRow,
        ContractCommerEditPayloadInput input)
    {
        if (input.IsCreateMode)
        {
            AppendCreateValue(request, "task_kind_id", input.TaskKindId);
            AppendCreateValue(request, "code", NormalizeText(input.Code));
            AppendCreateValue(request, "year", input.Year);
            if (input.Order is null)
            {
                request["order"] = null;
            }

            AppendCreateValue(request, "contragent_id", input.ContragentId);
            AppendCreateValue(request, "status_id", input.StatusId);
            AppendCreateValue(request, "signed_at", FormatDate(input.SignedAt));
            AppendCreateValue(request, "governmental", input.Governmental);
            AppendCreateValue(request, "external_number", NormalizeText(input.ExternalNumber));
            AppendCreateValue(request, "deadline_at", FormatDate(input.DeadlineAt));
            AppendCreateValue(request, "closed_at", FormatDate(input.ClosedAt));
            return;
        }

        AppendChangedLong(request, sourceRow, "task_kind_id", input.TaskKindId, "task_kind.id");
        AppendChangedText(request, sourceRow, "code", input.Code);
        AppendChangedInt(request, sourceRow, "year", input.Year);
        AppendChangedLong(request, sourceRow, "contragent_id", input.ContragentId, "contragent.id");
        AppendChangedLong(request, sourceRow, "status_id", input.StatusId, "status.id");
        AppendChangedDate(request, sourceRow, "signed_at", input.SignedAt);
        AppendChangedBool(request, sourceRow, "governmental", input.Governmental);
        AppendChangedText(request, sourceRow, "external_number", input.ExternalNumber);
        AppendChangedDate(request, sourceRow, "deadline_at", input.DeadlineAt);
        AppendChangedDate(request, sourceRow, "closed_at", input.ClosedAt);
    }

    private static void AppendStageAttributes(
        IDictionary<string, object?> request,
        IReadOnlyList<StageEditState> stages,
        bool isCreateMode,
        int? profileId)
    {
        var attributes = stages
            .Where(stage => isCreateMode || IsNewStage(stage) || stage.HasChanges || HasTaskChanges(stage) || !string.IsNullOrWhiteSpace(stage.Comment))
            .Select(stage => BuildStageAttributes(stage, profileId))
            .Where(static item => item.Count > 0)
            .ToList();

        if (attributes.Count > 0)
        {
            request["stages_attributes"] = attributes;
        }
    }

    private static bool HasTaskChanges(StageEditState stage)
    {
        var originalTaskKindIds = stage.Original.Tasks
            .Select(static task => task.TaskKindId)
            .Where(static id => id is not null)
            .Select(static id => id!.Value)
            .ToHashSet();
        var currentTaskKindIds = stage.Tasks
            .Select(static task => task.TaskKindId)
            .Where(static id => id is not null)
            .Select(static id => id!.Value)
            .ToHashSet();
        return !originalTaskKindIds.SetEquals(currentTaskKindIds);
    }

    private static bool IsNewStage(StageEditState stage)
    {
        return stage.Id <= 0;
    }

    private static Dictionary<string, object?> BuildStageAttributes(StageEditState stage, int? inputProfileId)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var isNew = stage.Id <= 0;
        if (!isNew)
        {
            payload["id"] = stage.Id;
        }

        if (!string.IsNullOrWhiteSpace(stage.ListKey))
        {
            payload["list_key"] = stage.ListKey;
        }

        if (stage.IsDestroyed)
        {
            payload["_destroy"] = "1";
            return payload;
        }

        AppendStageValue(payload, isNew, "priority", stage.Original.Priority, stage.Priority);
        AppendStageValue(payload, isNew, "used", stage.Original.Used, stage.Used);
        AppendStageValue(payload, isNew, "cost", stage.Original.Cost, stage.Cost);
        AppendStageValue(payload, isNew, "status_id", stage.Original.Status.Id, stage.Status.Id);
        AppendStageValue(payload, isNew, "task_kind_id", stage.Original.TaskKind.Id, stage.TaskKind.Id);
        AppendStageText(payload, isNew, "deadline_kind", stage.Original.DeadlineKind, stage.DeadlineKind);
        AppendStageValue(payload, isNew, "duration", stage.Original.Duration, stage.Duration);
        AppendStageDate(payload, isNew, "start_at", stage.Original.StartAt, stage.StartAt);
        AppendStageDate(payload, isNew, "deadline_at", stage.Original.DeadlineAt, stage.DeadlineAt);
        AppendStageDate(payload, isNew, "closed_at", stage.Original.ClosedAt, stage.ClosedAt);
        AppendStageText(payload, isNew, "payment_deadline_kind", stage.Original.PaymentDeadlineKind, stage.PaymentDeadlineKind);
        AppendStageValue(payload, isNew, "payment_duration", stage.Original.PaymentDuration, stage.PaymentDuration);
        AppendStageDate(payload, isNew, "payment_deadline_at", stage.Original.PaymentDeadlineAt, stage.PaymentDeadlineAt);

        var tasksDelta = StageCommerEditPayloadBuilder.BuildTaskAttributesDelta(stage);
        if (tasksDelta.Count > 0)
        {
            payload["tasks_attributes"] = tasksDelta;
        }

        StageEditPayloadBuilderHelpers.AppendCommentAttributes(payload, stage.Comment, inputProfileId);
        return payload;
    }

    private static void AppendRevisionAttributes(
        IDictionary<string, object?> request,
        IReadOnlyList<RevisionEditState> revisions,
        bool isCreateMode)
    {
        var attributes = revisions
            .Where(revision => isCreateMode || IsNewRevision(revision) || revision.HasChanges)
            .Select(BuildRevisionAttributes)
            .Where(static item => item.Count > 0)
            .ToList();

        if (attributes.Count > 0)
        {
            request["revisions_attributes"] = attributes;
        }
    }

    private static Dictionary<string, object?> BuildRevisionAttributes(RevisionEditState revision)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var isNew = revision.Id is null or <= 0;
        if (!isNew && revision.Id is long id)
        {
            payload["id"] = id;
        }

        if (!string.IsNullOrWhiteSpace(revision.ListKey))
        {
            payload["list_key"] = revision.ListKey;
        }

        if (revision.IsDestroyed)
        {
            payload["_destroy"] = "1";
            return payload;
        }

        AppendStageValue(payload, isNew, "priority", revision.Original.Priority, revision.Priority);
        AppendStageValue(payload, isNew, "is_signed", revision.Original.IsSigned, revision.IsSigned);
        AppendStageValue(payload, isNew, "is_present", revision.Original.IsPresent, revision.IsPresent);
        AppendStageValue(payload, isNew, "used", revision.Original.Used, revision.Used);
        AppendStageText(payload, isNew, "description", revision.Original.Description, revision.Description);
        AppendStageText(payload, isNew, "doc_link", revision.Original.DocLink, revision.DocLink);
        AppendStageText(payload, isNew, "scan_link", revision.Original.ScanLink, revision.ScanLink);
        AppendStageText(payload, isNew, "protocol_link", revision.Original.ProtocolLink, revision.ProtocolLink);
        AppendStageText(payload, isNew, "zip_link", revision.Original.ZipLink, revision.ZipLink);
        return payload;
    }

    private static bool IsNewRevision(RevisionEditState revision)
    {
        return revision.Id is null or <= 0;
    }

    private static void AppendCommentAttributes(
        IDictionary<string, object?> request,
        string? comment,
        int? profileId)
    {
        var normalizedComment = NormalizeText(comment);
        if (string.IsNullOrWhiteSpace(normalizedComment))
        {
            return;
        }

        if (profileId is null)
        {
            throw new InvalidOperationException("Не удалось определить profile_id пользователя для комментария.");
        }

        request["comments_attributes"] = new[]
        {
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["content"] = normalizedComment,
                ["profile_id"] = profileId.Value
            }
        };
    }

    private static void AppendCreateValue(IDictionary<string, object?> request, string key, object? value)
    {
        if (value is not null)
        {
            request[key] = value;
        }
    }

    private static void AppendStageValue(
        IDictionary<string, object?> request,
        bool isNew,
        string key,
        object? originalValue,
        object? value)
    {
        if (isNew)
        {
            if (value is not null)
            {
                request[key] = value;
            }

            return;
        }

        if (!Equals(originalValue, value))
        {
            request[key] = value;
        }
    }

    private static void AppendStageText(
        IDictionary<string, object?> request,
        bool isNew,
        string key,
        string? originalValue,
        string? value)
    {
        var normalizedOriginal = NormalizeText(originalValue);
        var normalizedValue = NormalizeText(value);
        if (isNew)
        {
            if (normalizedValue is not null)
            {
                request[key] = normalizedValue;
            }

            return;
        }

        if (!string.Equals(normalizedOriginal, normalizedValue, StringComparison.Ordinal))
        {
            request[key] = normalizedValue;
        }
    }

    private static void AppendStageDate(
        IDictionary<string, object?> request,
        bool isNew,
        string key,
        DateTimeOffset? originalValue,
        DateTimeOffset? value)
    {
        if (isNew)
        {
            var formattedValue = FormatDate(value);
            if (formattedValue is not null)
            {
                request[key] = formattedValue;
            }

            return;
        }

        if (ToDateOnly(originalValue) != ToDateOnly(value))
        {
            request[key] = FormatDate(value);
        }
    }

    private static void AppendChangedText(
        IDictionary<string, object?> request,
        TableDataRow sourceRow,
        string key,
        string? value)
    {
        var originalValue = NormalizeText(sourceRow.GetValue(key)?.ToString());
        var currentValue = NormalizeText(value);
        if (!string.Equals(originalValue, currentValue, StringComparison.Ordinal))
        {
            request[key] = currentValue;
        }
    }

    private static void AppendChangedLong(
        IDictionary<string, object?> request,
        TableDataRow sourceRow,
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

    private static void AppendChangedInt(
        IDictionary<string, object?> request,
        TableDataRow sourceRow,
        string key,
        int? value)
    {
        var originalValue = TryGetLong(sourceRow.GetValue(key)) is long number ? (int?)number : null;
        if (originalValue != value)
        {
            request[key] = value;
        }
    }

    private static void AppendChangedBool(
        IDictionary<string, object?> request,
        TableDataRow sourceRow,
        string key,
        bool value)
    {
        var originalValue = TryGetBool(sourceRow.GetValue(key)) == true;
        if (originalValue != value)
        {
            request[key] = value;
        }
    }

    private static void AppendChangedDate(
        IDictionary<string, object?> request,
        TableDataRow sourceRow,
        string key,
        DateTimeOffset? value)
    {
        var originalDate = ToDateOnly(AppFormatters.ParseDate(sourceRow.GetValue(key)));
        var currentDate = ToDateOnly(value);
        if (originalDate != currentDate)
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

    private static string? FormatDate(DateTimeOffset? value)
    {
        return value is null ? null : AppFormatters.FormatDate(value);
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

}
