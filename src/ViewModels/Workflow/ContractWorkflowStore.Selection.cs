using System.Text.Json;
using CbsContractsDesktopClient.Models.Table;
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
            _selectionKind = selectionKind;
            SelectedRevision = null;
            SelectedStage = null;
            SelectedStageEditState = null;
            Contract = selectionKind == ContractRowDetailSelectionKind.Contract
                ? contract ?? selectedRow
                : contract;
            SelectedContractEditState = ContractEditState.FromRow(Contract);
            Contragent = contragent;
            FocusedRevisionPriority = null;
            SelectedStage = ResolveSelectedStage(selectionKind, selectedRow, Contract);
            ResetEditGraph();

            if (selectionKind == ContractRowDetailSelectionKind.Revision)
            {
                SelectedRevision = selectedRow;
                FocusedRevisionPriority = TryGetInt(selectedRow.GetValue("priority"));
            }

            SelectedRowHeader = selectedRowHeader ?? string.Empty;
            SelectedFooterText = BuildSelectedFooterText(selectedRow, SelectedStage);
            Comments = ReadSelectionComments(selectionKind, selectedRow, Contract);
            NotifySelectionApplied();
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
