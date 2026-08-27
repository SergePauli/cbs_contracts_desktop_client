using System.Text.Json;
using System.Text.Json.Nodes;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow;

public sealed class ContractWorkflowFactory
{
    private readonly IDataQueryService _dataQueryService;
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _currentLoadCts;
    private ContractWorkflowSelectionKey? _currentLoadKey;
    private Task<ContractWorkflowContext>? _currentLoadTask;
    private long _latestLoadVersion;

    public ContractWorkflowFactory(IDataQueryService dataQueryService)
    {
        _dataQueryService = dataQueryService;
    }

    public Task<ContractWorkflowContext> CreateFromContractRowAsync(
        TableDataRow row,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        var key = ContractWorkflowSelectionKey.FromContractRow(row);
        return CreateLatestAsync(key, row, cancellationToken);
    }

    public Task<ContractWorkflowContext> CreateFromStageRowAsync(
        TableDataRow row,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        var key = ContractWorkflowSelectionKey.FromStageRow(row);
        return CreateLatestAsync(key, row, cancellationToken);
    }

    public Task<ContractWorkflowContext> CreateFromRevisionRowAsync(
        TableDataRow row,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);

        var key = ContractWorkflowSelectionKey.FromRevisionRow(row);
        return CreateLatestAsync(key, row, cancellationToken);
    }

    public void CancelCurrentLoad()
    {
        lock (_syncRoot)
        {
            if (_currentLoadCts is not null)
            {
                _ = _currentLoadCts.CancelAsync();
            }
            _currentLoadCts = null;
            _currentLoadKey = null;
            _currentLoadTask = null;
            _latestLoadVersion++;
        }
    }

    public bool IsLatest(ContractWorkflowContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (_syncRoot)
        {
            return context.LoadVersion == _latestLoadVersion;
        }
    }

    private Task<ContractWorkflowContext> CreateLatestAsync(
        ContractWorkflowSelectionKey key,
        TableDataRow row,
        CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (_currentLoadTask is { IsCompleted: false }
                && _currentLoadKey == key)
            {
                return _currentLoadTask.WaitAsync(cancellationToken);
            }

            if (_currentLoadCts is not null)
            {
                _ = _currentLoadCts.CancelAsync();
            }

            var currentLoad = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var completion = new TaskCompletionSource<ContractWorkflowContext>(TaskCreationOptions.RunContinuationsAsynchronously);
            _currentLoadCts = currentLoad;
            _currentLoadKey = key;
            _currentLoadTask = completion.Task;
            var loadVersion = ++_latestLoadVersion;
            _ = CompleteCurrentLoadAsync(key, row, loadVersion, currentLoad, completion);
            return completion.Task;
        }
    }

    private async Task CompleteCurrentLoadAsync(
        ContractWorkflowSelectionKey key,
        TableDataRow row,
        long loadVersion,
        CancellationTokenSource currentLoad,
        TaskCompletionSource<ContractWorkflowContext> completion)
    {
        try
        {
            completion.SetResult(await CreateCoreAsync(key, row, loadVersion, currentLoad.Token));
        }
        catch (OperationCanceledException exception)
        {
            completion.SetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            completion.SetException(exception);
        }
        finally
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_currentLoadCts, currentLoad))
                {
                    _currentLoadCts = null;
                    _currentLoadKey = null;
                    _currentLoadTask = null;
                }
            }

            currentLoad.Dispose();
        }
    }

    private async Task<ContractWorkflowContext> CreateCoreAsync(
        ContractWorkflowSelectionKey key,
        TableDataRow selectedRow,
        long loadVersion,
        CancellationToken cancellationToken)
    {
        var stage = "load-contract-edit";
        try
        {
            var contract = await LoadContractEditRowAsync(key.ContractId, cancellationToken)
                ?? throw new InvalidOperationException("Contract edit row was not loaded.");
            stage = "add-computed-stage-names";
            AddComputedStageNames(contract);
            stage = "resolve-contragent-id";
            var contragentId = key.ContragentId ?? TryGetLong(contract.GetValue("contragent.id"));
            stage = "load-contragent-card";
            var contragent = contragentId is long id
                ? await LoadContragentCardAsync(id, cancellationToken)
                : null;

            stage = "create-context";
            return new ContractWorkflowContext(key, selectedRow, contract, contragent, loadVersion);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            DiagnosticsFileLogger.AppendBlock(
                "CONTRACT CONTEXT LOAD FAILED",
                $"stage={stage}{Environment.NewLine}"
                + $"selectionKind={key.SelectionKind}{Environment.NewLine}"
                + $"contractId={key.ContractId}{Environment.NewLine}"
                + $"contragentId={key.ContragentId?.ToString() ?? "<null>"}{Environment.NewLine}"
                + $"exception={ex}");
            throw new InvalidOperationException(
                $"ContractWorkflowFactory.CreateCoreAsync failed at '{stage}': {ex.Message}",
                ex);
        }
    }

    private async Task<TableDataRow?> LoadContractEditRowAsync(
        long contractId,
        CancellationToken cancellationToken)
    {
        var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
            new DataQueryRequest
            {
                Model = "Contract",
                Preset = "edit",
                Filters = new Dictionary<string, object?>
                {
                    ["id__eq"] = contractId
                },
                Limit = 1
            },
            cancellationToken);

        return rows.FirstOrDefault(static row => !row.IsPlaceholder);
    }

    public async Task<TableDataRow> ReloadContractEditRowAsync(
        long contractId,
        CancellationToken cancellationToken = default)
    {
        var contract = await LoadContractEditRowAsync(contractId, cancellationToken)
            ?? throw new InvalidOperationException("Contract edit row was not loaded.");
        AddComputedStageNames(contract);
        return contract;
    }

    public async Task<TableDataRow> LoadContragentCardRowAsync(
        long contragentId,
        CancellationToken cancellationToken = default)
    {
        return await LoadContragentCardAsync(contragentId, cancellationToken)
            ?? throw new InvalidOperationException($"Contragent card row {contragentId} was not loaded.");
    }

    private static void AddComputedStageNames(TableDataRow contract)
    {
        var stages = TryGetArray(contract, "stages");
        if (stages is null)
        {
            return;
        }

        var stageItems = stages.Value
            .EnumerateArray()
            .Where(static stage => stage.ValueKind == JsonValueKind.Object)
            .ToList();
        var isSingleZeroStage = stageItems.Count == 1 && TryGetInt(stageItems[0], "priority") == 0;
        var enrichedStages = new JsonArray();

        foreach (var stage in stageItems)
        {
            var priority = TryGetInt(stage, "priority")
                ?? throw new InvalidOperationException("Contract edit stage row must contain priority for computed name.");
            var taskKindName = TryGetObject(stage, "task_kind") is { } taskKind
                ? TryGetString(taskKind, "name")
                : null;
            if (string.IsNullOrWhiteSpace(taskKindName))
            {
                throw new InvalidOperationException("Contract edit stage row must contain task_kind.name for computed name.");
            }

            var stageObject = JsonNode.Parse(stage.GetRawText())?.AsObject()
                ?? throw new InvalidOperationException("Contract edit stage row must be a JSON object.");
            stageObject["name"] = isSingleZeroStage
                ? taskKindName
                : $"Э{priority}_{taskKindName}";
            enrichedStages.Add(stageObject);
        }

        contract.Values["stages"] = JsonSerializer.SerializeToElement(enrichedStages);
        contract.RefreshResolvedValues();
    }

    private async Task<TableDataRow?> LoadContragentCardAsync(
        long contragentId,
        CancellationToken cancellationToken)
    {
        var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
            new DataQueryRequest
            {
                Model = "Contragent",
                Preset = "card",
                Filters = new Dictionary<string, object?>
                {
                    ["id__eq"] = contragentId
                },
                Limit = 1
            },
            cancellationToken);

        return rows.FirstOrDefault(static row => !row.IsPlaceholder);
    }
}

public sealed record ContractWorkflowContext(
    ContractWorkflowSelectionKey Key,
    TableDataRow SelectedRow,
    TableDataRow Contract,
    TableDataRow? Contragent,
    long LoadVersion)
{
    public bool Matches(TableDataRow? row)
    {
        return row is not null
            && !row.IsPlaceholder
            && Key == ContractWorkflowSelectionKey.FromRow(Key.SelectionKind, row);
    }

    public void ApplyTo(ContractWorkflowStore store, ContractRowDetailStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(strategy);

        strategy.ApplySelection(store, SelectedRow, Contract, Contragent);
    }
}

public sealed record ContractWorkflowSelectionKey(
    ContractRowDetailSelectionKind SelectionKind,
    long? SelectedRowId,
    string? SelectedRowListKey,
    long ContractId,
    long? ContragentId,
    long? StageId,
    long? RevisionPriority)
{
    public static ContractWorkflowSelectionKey FromContractRow(TableDataRow row)
    {
        return FromRow(ContractRowDetailSelectionKind.Contract, row);
    }

    public static ContractWorkflowSelectionKey FromStageRow(TableDataRow row)
    {
        return FromRow(ContractRowDetailSelectionKind.Stage, row);
    }

    public static ContractWorkflowSelectionKey FromRevisionRow(TableDataRow row)
    {
        return FromRow(ContractRowDetailSelectionKind.Revision, row);
    }

    public static ContractWorkflowSelectionKey FromRow(
        ContractRowDetailSelectionKind selectionKind,
        TableDataRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return selectionKind switch
        {
            ContractRowDetailSelectionKind.Contract => BuildContractKey(row),
            ContractRowDetailSelectionKind.Stage => BuildStageKey(row),
            ContractRowDetailSelectionKind.Revision => BuildRevisionKey(row),
            _ => throw new InvalidOperationException($"Unsupported contract workflow selection kind: {selectionKind}.")
        };
    }

    private static ContractWorkflowSelectionKey BuildContractKey(TableDataRow row)
    {
        var contractId = TryGetLong(row.GetValue("id"))
            ?? throw new InvalidOperationException("Contract workflow contract row must contain id.");
        return new ContractWorkflowSelectionKey(
            ContractRowDetailSelectionKind.Contract,
            TryGetLong(row.GetValue("id")),
            row.GetValue("list_key")?.ToString(),
            contractId,
            TryGetLong(row.GetValue("contragent.id")),
            StageId: null,
            RevisionPriority: null);
    }

    private static ContractWorkflowSelectionKey BuildStageKey(TableDataRow row)
    {
        var contractId =
            TryGetLong(row.GetValue("contract.id"))
            ?? TryGetLong(row.GetValue("contract_id"))
            ?? throw new InvalidOperationException("Contract workflow stage row must contain contract.id or contract_id.");
        return new ContractWorkflowSelectionKey(
            ContractRowDetailSelectionKind.Stage,
            TryGetLong(row.GetValue("id")),
            row.GetValue("list_key")?.ToString(),
            contractId,
            TryGetLong(row.GetValue("contract.contragent.id")) ?? TryGetLong(row.GetValue("contract.contragent_id")),
            TryGetLong(row.GetValue("id")),
            RevisionPriority: null);
    }

    private static ContractWorkflowSelectionKey BuildRevisionKey(TableDataRow row)
    {
        var contractId =
            TryGetLong(row.GetValue("contract.id"))
            ?? TryGetLong(row.GetValue("contract_id"))
            ?? throw new InvalidOperationException("Contract workflow revision row must contain contract.id or contract_id.");
        return new ContractWorkflowSelectionKey(
            ContractRowDetailSelectionKind.Revision,
            TryGetLong(row.GetValue("id")),
            row.GetValue("list_key")?.ToString(),
            contractId,
            TryGetLong(row.GetValue("contract.contragent.id")) ?? TryGetLong(row.GetValue("contract.contragent_id")),
            StageId: null,
            TryGetLong(row.GetValue("priority")));
    }
}
