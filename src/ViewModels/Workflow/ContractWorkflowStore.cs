using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.References;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class ContractWorkflowStore : ObservableObject
    {
        [ObservableProperty]
        public partial ReferenceDataRow? SelectedRevision { get; set; }

        [ObservableProperty]
        public partial ReferenceDataRow? SelectedStage { get; set; }

        [ObservableProperty]
        public partial ReferenceDataRow? Contract { get; set; }

        [ObservableProperty]
        public partial ReferenceDataRow? Contragent { get; set; }

        [ObservableProperty]
        public partial int? FocusedRevisionPriority { get; set; }

        [ObservableProperty]
        public partial string SelectedRowHeader { get; set; } = string.Empty;

        [ObservableProperty]
        public partial IReadOnlyList<ReferenceDataRow> Comments { get; set; } = [];

        public void SetRevisionSelection(
            ReferenceDataRow revision,
            ReferenceDataRow? contract,
            ReferenceDataRow? contragent,
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
            ReferenceDataRow stage,
            ReferenceDataRow? contract,
            ReferenceDataRow? contragent,
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
            ReferenceDataRow selectedContract,
            ReferenceDataRow? contract,
            ReferenceDataRow? contragent,
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
            ReferenceDataRow selectedRow,
            ReferenceDataRow? contract,
            ReferenceDataRow? contragent,
            string? selectedRowHeader = null)
        {
            SelectedRevision = null;
            SelectedStage = null;
            Contract = selectionKind == ContractRowDetailSelectionKind.Contract
                ? contract ?? selectedRow
                : contract;
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
            }

            SelectedRowHeader = selectedRowHeader ?? string.Empty;
            Comments = ReadContractComments(Contract);
        }

        public void ClearRowDetailSelection()
        {
            SelectedRevision = null;
            SelectedStage = null;
            Contract = null;
            Contragent = null;
            FocusedRevisionPriority = null;
            SelectedRowHeader = string.Empty;
            Comments = [];
        }

        private static IReadOnlyList<ReferenceDataRow> ReadContractComments(ReferenceDataRow? contract)
        {
            if (contract is null || contract.IsPlaceholder)
            {
                return [];
            }

            var comments = new List<ReferenceDataRow>();
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

        private static void AddComments(ICollection<ReferenceDataRow> target, JsonElement? comments)
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

                target.Add(ToReferenceDataRow(comment));
            }
        }
    }
}
