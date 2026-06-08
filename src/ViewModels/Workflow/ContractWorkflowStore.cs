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
        public partial string SelectedFooterText { get; set; } = string.Empty;

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
            SelectedStage = ResolveSelectedStage(selectionKind, selectedRow, Contract);
            SelectedStageEditState = SelectedStage is null ? null : StageEditState.FromRow(SelectedStage);

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

        public string BuildSelectedFooterText(TableDataRow selectedRow)
        {
            return BuildSelectedFooterText(selectedRow, SelectedStage);
        }

        private static string BuildSelectedFooterText(TableDataRow selectedRow, TableDataRow? stage)
        {
            var id = TryGetLong(selectedRow.GetValue("id"));
            var taskKindName = FirstText(
                stage?.GetValue("task_kind.name"),
                stage?.GetValue("contract.task_kind.name"),
                selectedRow.GetValue("stage.task_kind.name"),
                selectedRow.GetValue("task_kind.name"),
                selectedRow.GetValue("contract.task_kind.name"));
            var tasks = ReadNameList(stage, "tasks");
            var performers = ReadNameList(stage, "performers");
            var stageTaskKindText = FormatStageTaskKindText(stage, taskKindName);

            return $"(ID: {FormatId(id)}) {stageTaskKindText}|{FormatNameList(tasks)}|{FormatNameList(performers)}";
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

        private static IReadOnlyList<TableDataRow> ReadSelectionComments(
            ContractRowDetailSelectionKind selectionKind,
            TableDataRow selectedRow,
            TableDataRow? contract)
        {
            if (contract is null || contract.IsPlaceholder)
            {
                return [];
            }

            var comments = new List<TableDataRow>();
            AddComments(comments, TryGetArray(contract, "comments"));

            if (selectionKind == ContractRowDetailSelectionKind.Stage)
            {
                AddComments(comments, ReadSelectedStageComments(selectedRow, contract));
                return SortComments(comments);
            }

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

            return SortComments(comments);
        }

        private static IReadOnlyList<TableDataRow> SortComments(IReadOnlyList<TableDataRow> comments)
        {
            return comments
                .Select(static (comment, index) => new CommentSortItem(comment, index))
                .OrderBy(static item => item, CommentSortComparer.Instance)
                .Select(static item => item.Comment)
                .ToList();
        }

        private static JsonElement? ReadSelectedStageComments(TableDataRow selectedRow, TableDataRow contract)
        {
            var selectedComments = TryGetArray(selectedRow, "comments");
            if (selectedComments is not null)
            {
                return selectedComments;
            }

            var stageId = TryGetLong(selectedRow.GetValue("id"));
            var stages = TryGetArray(contract, "stages");
            if (stageId is null || stages is null)
            {
                return null;
            }

            foreach (var stage in stages.Value.EnumerateArray())
            {
                if (stage.ValueKind == JsonValueKind.Object
                    && TryGetLong(stage.GetProperty("id")) == stageId
                    && TryGetArray(stage, "comments") is JsonElement comments)
                {
                    return comments;
                }
            }

            return null;
        }

        private static IReadOnlyList<string> ReadNameList(TableDataRow? row, string fieldKey)
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

        private static string FormatId(long? id)
        {
            return id?.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
        }

        private static string FormatNameList(IReadOnlyList<string> values)
        {
            return values.Count == 0 ? "нет" : string.Join(", ", values);
        }

        private static string FormatStageTaskKindText(TableDataRow? stage, string taskKindName)
        {
            var priority = TryGetInt(stage?.GetValue("priority"));
            return priority > 0
                ? $"Э{priority} - {taskKindName}"
                : taskKindName;
        }

        private sealed record CommentSortItem(TableDataRow Comment, int Index)
        {
            public long? Id { get; } = TryGetLong(Comment.GetValue("id"));
        }

        private sealed class CommentSortComparer : IComparer<CommentSortItem>
        {
            public static CommentSortComparer Instance { get; } = new();

            public int Compare(CommentSortItem? x, CommentSortItem? y)
            {
                if (x is null || y is null)
                {
                    return 0;
                }

                if (!x.Id.HasValue || !y.Id.HasValue)
                {
                    return 0;
                }

                return x.Id.Value.CompareTo(y.Id.Value);
            }
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
