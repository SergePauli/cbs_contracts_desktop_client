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
