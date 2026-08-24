using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Shared.Formatting;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

public sealed class ContractEditState : IEditState
{
    private ContractEditState(ContractEditStateSnapshot original)
    {
        Original = original;
        RestoreOriginal();
    }

    public ContractEditStateSnapshot Original { get; }

    public long Id { get; private set; }

    public string? ListKey { get; private set; }

    public string? Name { get; private set; }

    public decimal? Cost { get; private set; }

    public string? ExternalNumber { get; set; }

    public StatusEditState Status { get; private set; } = new(null, null);

    public DateTimeOffset? SignedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public TaskKindEditState TaskKind { get; private set; } = new(null, null, null);

    public string? ContragentName { get; private set; }

    public bool? Governmental { get; private set; }

    public bool IsMultiStage { get; private set; }

    public IReadOnlyList<StageSnapshotEditState> Stages { get; private set; } = [];

    public IReadOnlyList<ContractResponsibleEditState> ContractResponsibles { get; private set; } = [];

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name)
            ? Id.ToString(System.Globalization.CultureInfo.CurrentCulture)
            : Name;

    public string GetSectionTitle()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Contract edit state must contain name for contract section title.");
        }

        if (string.IsNullOrWhiteSpace(TaskKind.Name))
        {
            throw new InvalidOperationException("Contract edit state must contain task_kind.name for contract section title.");
        }

        return $"Контракт {Name} {TaskKind.Name}";
    }

    public bool HasExternalNumberChanges =>
        IsExternalNumberChanged(ExternalNumber);

    public bool HasChanges => HasExternalNumberChanges;

    public void ApplyClosedStatusPreview(DateTimeOffset? closedAt)
    {
        Status = new StatusEditState(WorkflowStatusIds.Closed, "Закрыт");
        ClosedAt = closedAt;
    }

    public void RestoreStatusPreview()
    {
        Status = Original.Status;
        ClosedAt = Original.ClosedAt;
    }

    public void RestoreOriginal()
    {
        Id = Original.Id;
        ListKey = Original.ListKey;
        Name = Original.Name;
        Cost = Original.Cost;
        ExternalNumber = Original.ExternalNumber;
        Status = Original.Status;
        SignedAt = Original.SignedAt;
        ClosedAt = Original.ClosedAt;
        TaskKind = Original.TaskKind;
        ContragentName = Original.ContragentName;
        Governmental = Original.Governmental;
        IsMultiStage = Original.IsMultiStage;
        Stages = Original.Stages;
        ContractResponsibles = Original.ContractResponsibles;
    }

    public void SetContractResponsibles(IReadOnlyList<ContractResponsibleEditState> contractResponsibles)
    {
        ArgumentNullException.ThrowIfNull(contractResponsibles);
        ContractResponsibles = contractResponsibles;
    }

    public void ClearContractResponsibles()
    {
        ContractResponsibles = [];
    }

    public void RestoreContractResponsibles()
    {
        ContractResponsibles = Original.ContractResponsibles;
    }

    public IReadOnlyDictionary<string, object?> BuildExternalNumberPayload()
    {
        return BuildExternalNumberPayload(ExternalNumber);
    }

    public bool IsExternalNumberChanged(string? externalNumber)
    {
        return !string.Equals(
            NormalizeText(externalNumber),
            NormalizeText(Original.ExternalNumber),
            StringComparison.Ordinal);
    }

    public IReadOnlyDictionary<string, object?> BuildExternalNumberPayload(string? externalNumber)
    {
        var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = Id,
            ["external_number"] = NormalizeText(externalNumber)
        };

        if (!string.IsNullOrWhiteSpace(ListKey))
        {
            request["list_key"] = ListKey;
        }

        return request;
    }

    public static ContractEditState? FromRow(TableDataRow? row)
    {
        if (row is null || row.IsPlaceholder)
        {
            return null;
        }

        var id = TryGetLong(row.GetValue("id"));
        if (id is null)
        {
            return null;
        }

        var stages = ReadStages(row);
        var isMultiStage =
            TryGetBool(row.GetValue("multyStage"))
            ?? TryGetBool(row.GetValue("multiStage"))
            ?? TryGetBool(row.GetValue("is_multistage"))
            ?? stages.Count > 1;

        var statusId = TryGetLong(row.GetValue("status.id")) ?? TryGetLong(row.GetValue("status_id"));
        if (statusId is null)
        {
            throw new InvalidOperationException("Contract edit row must contain status_id or status.id.");
        }

        var statusName = row.GetValue("status.name")?.ToString();
        if (string.IsNullOrWhiteSpace(statusName))
        {
            throw new InvalidOperationException("Contract edit row must contain status.name.");
        }

        return new ContractEditState(new ContractEditStateSnapshot(
            Id: id.Value,
            ListKey: row.GetValue("list_key")?.ToString(),
            Name: row.GetValue("name")?.ToString(),
            Cost: TryGetDecimal(row.GetValue("cost")),
            ExternalNumber: NormalizeText(row.GetValue("external_number")?.ToString()),
            Status: new StatusEditState(
                statusId,
                statusName),
            SignedAt: AppFormatters.ParseDate(row.GetValue("signed_at")),
            ClosedAt: AppFormatters.ParseDate(row.GetValue("closed_at")),
            TaskKind: new TaskKindEditState(
                TryGetLong(row.GetValue("task_kind.id")) ?? TryGetLong(row.GetValue("task_kind_id")),
                row.GetValue("task_kind.name")?.ToString(),
                row.GetValue("task_kind.code")?.ToString()),
            ContragentName: row.GetValue("contragent.name")?.ToString(),
            Governmental: TryGetBool(row.GetValue("governmental")),
            IsMultiStage: isMultiStage,
            Stages: stages,
            ContractResponsibles: ReadContractResponsibles(row)));
    }

    public static ContractEditState CreateNew()
    {
        return new ContractEditState(new ContractEditStateSnapshot(
            Id: 0,
            ListKey: null,
            Name: null,
            Cost: null,
            ExternalNumber: null,
            Status: new StatusEditState(null, null),
            SignedAt: null,
            ClosedAt: null,
            TaskKind: new TaskKindEditState(null, null, null),
            ContragentName: null,
            Governmental: null,
            IsMultiStage: false,
            Stages: [],
            ContractResponsibles: []));
    }

    private static IReadOnlyList<ContractResponsibleEditState> ReadContractResponsibles(TableDataRow row)
    {
        var contractResponsibles = TryGetArray(row, "contract_responsibles")
            ?? throw new InvalidOperationException("Contract edit row must contain contract_responsibles array.");
        return EnumerateObjectArray(contractResponsibles)
            .Select(static responsible =>
            {
                var id = TryGetLong(responsible, "id")
                    ?? throw new InvalidOperationException("Contract responsible edit row must contain id.");
                var employeeId = TryGetLong(responsible, "employee_id")
                    ?? throw new InvalidOperationException("Contract responsible edit row must contain employee_id.");
                var employee = TryGetObject(responsible, "employee")
                    ?? throw new InvalidOperationException("Contract responsible edit row must contain employee.");
                var fullName = TryGetString(employee, "full_name");
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    throw new InvalidOperationException("Contract responsible edit row must contain employee.full_name.");
                }

                return new ContractResponsibleEditState(
                    id,
                    TryGetString(responsible, "list_key"),
                    employeeId,
                    fullName);
            })
            .ToList();
    }

    private static IReadOnlyList<StageSnapshotEditState> ReadStages(TableDataRow row)
    {
        return EnumerateObjectArray(row, "stages")
            .Select(static stage => new StageSnapshotEditState(
                Id: TryGetLong(stage, "id"),
                StatusId: TryGetLong(TryGetValue(stage, "status_id"))
                    ?? (TryGetObject(stage, "status") is { } status ? TryGetLong(status, "id") : null),
                StatusName: TryGetObject(stage, "status") is { } statusName
                    ? TryGetString(statusName, "name")
                    : null))
            .ToList();
    }

    private static decimal? TryGetDecimal(object? value)
    {
        return value switch
        {
            decimal decimalValue => decimalValue,
            long int64Value => int64Value,
            int int32Value => int32Value,
            string text when decimal.TryParse(
                text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedValue) => parsedValue,
            _ => null
        };
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record ContractEditStateSnapshot(
    long Id,
    string? ListKey,
    string? Name,
    decimal? Cost,
    string? ExternalNumber,
    StatusEditState Status,
    DateTimeOffset? SignedAt,
    DateTimeOffset? ClosedAt,
    TaskKindEditState TaskKind,
    string? ContragentName,
    bool? Governmental,
    bool IsMultiStage,
    IReadOnlyList<StageSnapshotEditState> Stages,
    IReadOnlyList<ContractResponsibleEditState> ContractResponsibles);
