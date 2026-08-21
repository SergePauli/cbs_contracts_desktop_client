using System.Text.Json;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class ContractWorkflowStore
    {
        private ContractRowDetailSelectionKind _selectionKind = ContractRowDetailSelectionKind.Contract;

        public void SetRevisionSelection(
            TableDataRow revision,
            TableDataRow? contract,
            TableDataRow? contragent,
            string? selectedRowHeader = null)
        {
            SetRowDetailSelection(
                ContractRowDetailSelectionKind.Revision,
                revision,
                contract,
                contragent,
                selectedRowHeader);
        }

        public void SetStageSelection(
            TableDataRow stage,
            TableDataRow? contract,
            TableDataRow? contragent,
            string? selectedRowHeader = null)
        {
            SetRowDetailSelection(
                ContractRowDetailSelectionKind.Stage,
                stage,
                contract,
                contragent,
                selectedRowHeader);
        }

        public void SetContractSelection(
            TableDataRow selectedContract,
            TableDataRow? contract,
            TableDataRow? contragent,
            string? selectedRowHeader = null)
        {
            SetRowDetailSelection(
                ContractRowDetailSelectionKind.Contract,
                selectedContract,
                contract,
                contragent,
                selectedRowHeader);
        }

        public void SetRowDetailSelection(
            ContractRowDetailSelectionKind selectionKind,
            TableDataRow selectedRow,
            TableDataRow? contract,
            TableDataRow? contragent,
            string? selectedRowHeader = null)
        {
            var stage = "begin";
            BeginSelectionApplication();
            try
            {
                stage = "assign-selection";
                _selectionKind = selectionKind;
                SelectedRevision = null;
                SelectedStage = null;
                SelectedStageEditState = null;
                Contract = selectionKind == ContractRowDetailSelectionKind.Contract
                    ? contract ?? selectedRow
                    : contract;

                stage = "build-contract-edit-state";
                SelectedContractEditState = ContractEditState.FromRow(Contract);
                Contragent = contragent;
                FocusedRevisionPriority = null;

                stage = "resolve-selected-stage";
                SelectedStage = ResolveSelectedStage(selectionKind, selectedRow, Contract);

                stage = "reset-edit-graph";
                ResetEditGraph();

                if (selectionKind == ContractRowDetailSelectionKind.Revision)
                {
                    SelectedRevision = selectedRow;
                    FocusedRevisionPriority = TryGetInt(selectedRow.GetValue("priority"));
                }

                stage = "build-selection-presentation";
                SelectedRowHeader = selectedRowHeader ?? string.Empty;
                SelectedFooterText = BuildSelectedFooterText(selectedRow, SelectedStage);
                Comments = ReadSelectionComments(selectionKind, selectedRow, Contract);

                stage = "publish-selection";
                CompleteSelectionApplication();
            }
            catch (Exception ex)
            {
                CancelSelectionApplication();
                DiagnosticsFileLogger.AppendBlock(
                    "CONTRACT CONTEXT APPLY FAILED",
                    $"stage={stage}{Environment.NewLine}"
                    + $"selectionKind={selectionKind}{Environment.NewLine}"
                    + $"selectedRowId={TryGetLong(selectedRow.GetValue("id"))?.ToString() ?? "<null>"}{Environment.NewLine}"
                    + $"contractId={TryGetLong(contract?.GetValue("id"))?.ToString() ?? "<null>"}{Environment.NewLine}"
                    + $"contragentId={TryGetLong(contragent?.GetValue("id"))?.ToString() ?? "<null>"}{Environment.NewLine}"
                    + $"exception={ex}");
                throw new InvalidOperationException(
                    $"ContractWorkflowStore.SetRowDetailSelection failed at '{stage}': {ex.Message}",
                    ex);
            }
        }

        public IReadOnlyList<long> GetSelectedStageOrderIds()
        {
            var contract = Contract
                ?? throw new InvalidOperationException("ContractWorkflowStore.Contract не задан для построения аудита.");
            var stages = TryGetArray(contract, "stages")
                ?? throw new InvalidOperationException("Contract edit row не содержит обязательный массив stages для построения аудита.");

            var selectedStageId = _selectionKind == ContractRowDetailSelectionKind.Stage
                ? TryGetLong(SelectedStage?.GetValue("id"))
                    ?? throw new InvalidOperationException("Выбранный этап не содержит обязательный id для построения аудита.")
                : (long?)null;
            var selectedStageFound = selectedStageId is null;
            var stageOrderIds = new List<long>();

            foreach (var stage in stages.EnumerateArray())
            {
                if (stage.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException("Contract edit row содержит некорректный элемент stages для построения аудита.");
                }

                var stageId = TryGetLong(stage, "id")
                    ?? throw new InvalidOperationException("Contract edit stage row не содержит обязательный id для построения аудита.");
                if (selectedStageId is not null && stageId != selectedStageId.Value)
                {
                    continue;
                }

                selectedStageFound = true;
                var stageOrders = TryGetArray(stage, "stage_orders")
                    ?? throw new InvalidOperationException($"Contract edit stage row ID {stageId} не содержит обязательный массив stage_orders.");
                foreach (var stageOrder in stageOrders.EnumerateArray())
                {
                    if (stageOrder.ValueKind != JsonValueKind.Object)
                    {
                        throw new InvalidOperationException($"Stage ID {stageId} содержит некорректный элемент stage_orders.");
                    }

                    stageOrderIds.Add(
                        TryGetLong(stageOrder, "id")
                        ?? throw new InvalidOperationException($"StageOrder этапа ID {stageId} не содержит обязательный id."));
                }
            }

            if (!selectedStageFound)
            {
                throw new InvalidOperationException($"Выбранный этап ID {selectedStageId} отсутствует в Contract edit row.");
            }

            return stageOrderIds.Distinct().ToList();
        }

        public bool MatchesSelectedAuditRecord(string model, long selectedId)
        {
            return model switch
            {
                "Contract" => _selectionKind == ContractRowDetailSelectionKind.Contract
                    && SelectedContractEditState?.Id == selectedId,
                "Stage" => _selectionKind == ContractRowDetailSelectionKind.Stage
                    && SelectedStageEditState?.Id == selectedId,
                _ => false
            };
        }

        public void ClearRowDetailSelection()
        {
            BeginSelectionApplication();
            _selectionKind = ContractRowDetailSelectionKind.Contract;
            SelectedRevision = null;
            SelectedStage = null;
            SelectedStageEditState = null;
            Contract = null;
            SelectedContractEditState = null;
            Contragent = null;
            FocusedRevisionPriority = null;
            SelectedRowHeader = string.Empty;
            SelectedFooterText = string.Empty;
            Comments = [];
            CompleteSelectionApplication();
        }

        private static TableDataRow? ResolveSelectedStage(
            ContractRowDetailSelectionKind selectionKind,
            TableDataRow selectedRow,
            TableDataRow? contract)
        {
            if (selectionKind == ContractRowDetailSelectionKind.Stage)
            {
                return selectedRow;
            }

            if (contract is null || contract.IsPlaceholder)
            {
                return null;
            }

            var stages = TryGetArray(contract, "stages");
            if (stages is null)
            {
                return null;
            }

            JsonElement? firstStage = null;
            foreach (var stage in stages.Value.EnumerateArray())
            {
                if (stage.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                firstStage ??= stage;
                if (TryGetBool(stage, "used") == true)
                {
                    return ToTableDataRow(stage);
                }
            }

            return firstStage is JsonElement fallbackStage
                ? ToTableDataRow(fallbackStage)
                : null;
        }
    }
}
