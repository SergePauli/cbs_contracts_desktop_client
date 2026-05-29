using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow;

public enum ContractRowDetailSelectionKind
{
    Revision,
    Stage,
    Contract
}

public abstract class ContractRowDetailStrategy
{
    private readonly string _contractIdFieldKey;
    private readonly string _contragentIdFieldKey;
    private readonly ContractRowDetailSelectionKind _selectionKind;

    protected ContractRowDetailStrategy(
        string route,
        ContractRowDetailSelectionKind selectionKind,
        string contractIdFieldKey = "contract.id",
        string contragentIdFieldKey = "contract.contragent.id")
    {
        Route = route;
        _selectionKind = selectionKind;
        _contractIdFieldKey = contractIdFieldKey;
        _contragentIdFieldKey = contragentIdFieldKey;
    }

    public string Route { get; }

    public long? ResolveContractId(TableDataRow row)
    {
        return TryGetLong(row.GetValue(_contractIdFieldKey));
    }

    public long? ResolveContragentId(TableDataRow row)
    {
        return TryGetLong(row.GetValue(_contragentIdFieldKey));
    }

    public bool IsSameSelection(TableDataRow row, long? contractId)
    {
        return contractId is null || ResolveContractId(row) == contractId;
    }

    public void ApplySelection(
        ContractWorkflowStore store,
        TableDataRow selectedRow,
        TableDataRow? contract,
        TableDataRow? contragent)
    {
        store.SetRowDetailSelection(
            _selectionKind,
            selectedRow,
            contract,
            contragent,
            BuildSelectedRowHeader(selectedRow));
    }

    protected abstract string BuildSelectedRowHeader(TableDataRow row);

    protected static string BuildTaskKindHeader(TableDataRow row)
    {
        return FirstText(
            row.GetValue("task_kind.name"),
            row.GetValue("contract.task_kind.name"));
    }

    protected static IReadOnlyList<string> ReadNameList(TableDataRow row, string fieldKey)
    {
        var array = TryGetArray(row, fieldKey);
        if (array is null)
        {
            return [];
        }

        return array.Value
            .EnumerateArray()
            .Select(ReadDisplayName)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToList();
    }

    private static string? ReadDisplayName(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            return item.GetString();
        }

        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return TryGetString(item, "name")
            ?? TryGetString(item, "full_name")
            ?? TryGetString(item, "head")
            ?? TryGetString(item, "description");
    }
}

public sealed class RevisionRowDetailStrategy : ContractRowDetailStrategy
{
    public RevisionRowDetailStrategy()
        : base("/revisions", ContractRowDetailSelectionKind.Revision)
    {
    }

    protected override string BuildSelectedRowHeader(TableDataRow row)
    {
        return FirstText(row.GetValue("description"));
    }
}

public sealed class StageRowDetailStrategy : ContractRowDetailStrategy
{
    public StageRowDetailStrategy()
        : base("/stages", ContractRowDetailSelectionKind.Stage)
    {
    }

    protected override string BuildSelectedRowHeader(TableDataRow row)
    {
        var parts = new List<string>();
        var taskKind = BuildTaskKindHeader(row);
        if (!string.IsNullOrWhiteSpace(taskKind))
        {
            parts.Add(taskKind);
        }

        var performers = ReadNameList(row, "performers");
        parts.Add($"Исполнители: {(performers.Count == 0 ? "нет" : string.Join(", ", performers))}");

        var tasks = ReadNameList(row, "tasks");
        if (tasks.Count > 0)
        {
            parts.Add($"Прочие задачи: {string.Join(", ", tasks)}");
        }

        return string.Join("; ", parts);
    }
}

public sealed class ContractTableRowDetailStrategy : ContractRowDetailStrategy
{
    public ContractTableRowDetailStrategy()
        : base(
            "/contracts",
            ContractRowDetailSelectionKind.Contract,
            contractIdFieldKey: "id",
            contragentIdFieldKey: "contragent.id")
    {
    }

    protected override string BuildSelectedRowHeader(TableDataRow row)
    {
        return BuildTaskKindHeader(row);
    }
}
