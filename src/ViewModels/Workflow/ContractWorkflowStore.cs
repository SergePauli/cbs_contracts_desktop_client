using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class ContractWorkflowStore : ObservableObject
    {
        [ObservableProperty]
        public partial TableDataRow? SelectedRevision { get; set; }

        [ObservableProperty]
        public partial TableDataRow? SelectedStage { get; set; }

        [ObservableProperty]
        public partial TableDataRow? Contract { get; set; }

        [ObservableProperty]
        public partial TableDataRow? Contragent { get; set; }

        [ObservableProperty]
        public partial StageEditState? SelectedStageEditState { get; set; }

        [ObservableProperty]
        public partial ContractEditState? SelectedContractEditState { get; set; }

        [ObservableProperty]
        public partial int? FocusedRevisionPriority { get; set; }

        [ObservableProperty]
        public partial string SelectedRowHeader { get; set; } = string.Empty;

        [ObservableProperty]
        public partial IReadOnlyList<TableDataRow> Comments { get; set; } = [];

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
            SelectedRevision = null;
            SelectedStage = null;
            SelectedStageEditState = null;
            Contract = selectionKind == ContractRowDetailSelectionKind.Contract
                ? contract ?? selectedRow
                : contract;
            SelectedContractEditState = ContractEditState.FromRow(Contract);
            Contragent = contragent;
            FocusedRevisionPriority = null;

            if (selectionKind == ContractRowDetailSelectionKind.Revision)
            {
                SelectedRevision = selectedRow;
                FocusedRevisionPriority = TryGetInt(selectedRow.GetValue("priority"));
            }
            else if (selectionKind == ContractRowDetailSelectionKind.Stage)
            {
                SelectedStage = selectedRow;
                SelectedStageEditState = StageEditState.FromRow(selectedRow);
            }

            SelectedRowHeader = selectedRowHeader ?? string.Empty;
            Comments = ReadContractComments(Contract);
        }

        public void ClearRowDetailSelection()
        {
            SelectedRevision = null;
            SelectedStage = null;
            SelectedStageEditState = null;
            Contract = null;
            SelectedContractEditState = null;
            Contragent = null;
            FocusedRevisionPriority = null;
            SelectedRowHeader = string.Empty;
            Comments = [];
        }

        private static IReadOnlyList<TableDataRow> ReadContractComments(TableDataRow? contract)
        {
            if (contract is null || contract.IsPlaceholder)
            {
                return [];
            }

            var comments = new List<TableDataRow>();
            AddComments(comments, TryGetArray(contract, "comments"));

            var stages = TryGetArray(contract, "stages");
            if (stages is not null)
            {
                foreach (var stage in stages.Value.EnumerateArray())
                {
                    if (TryGetArray(stage, "comments") is JsonElement stageComments)
                    {
                        AddComments(comments, stageComments);
                    }
                }
            }

            return comments
                .OrderBy(static comment => TryGetLong(comment.GetValue("id")) ?? long.MaxValue)
                .ToList();
        }

        private static void AddComments(ICollection<TableDataRow> target, JsonElement? comments)
        {
            if (comments is null || comments.Value.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var comment in comments.Value.EnumerateArray())
            {
                if (comment.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                target.Add(ToTableDataRow(comment));
            }
        }
    }
}
