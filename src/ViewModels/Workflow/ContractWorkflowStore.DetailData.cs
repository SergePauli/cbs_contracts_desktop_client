using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class ContractWorkflowStore
    {
        public void ApplyRowDetailData(
            ContractRowDetailSelectionKind selectionKind,
            TableDataRow selectedRow,
            TableDataRow? contract,
            TableDataRow? contragent,
            string? selectedRowHeader = null)
        {
            var stage = "begin";
            BeginDetailDataApplication();
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

                stage = "publish-detail-data";
                CompleteDetailDataApplication();
            }
            catch (Exception ex)
            {
                CancelDetailDataApplication();
                DiagnosticsFileLogger.AppendBlock(
                    "CONTRACT CONTEXT APPLY FAILED",
                    $"stage={stage}{Environment.NewLine}"
                    + $"selectionKind={selectionKind}{Environment.NewLine}"
                    + $"selectedRowId={TryGetLong(selectedRow.GetValue("id"))?.ToString() ?? "<null>"}{Environment.NewLine}"
                    + $"contractId={TryGetLong(contract?.GetValue("id"))?.ToString() ?? "<null>"}{Environment.NewLine}"
                    + $"contragentId={TryGetLong(contragent?.GetValue("id"))?.ToString() ?? "<null>"}{Environment.NewLine}"
                    + $"exception={ex}");
                throw new InvalidOperationException(
                    $"ContractWorkflowStore.ApplyRowDetailData failed at '{stage}': {ex.Message}",
                    ex);
            }
        }
    }
}
