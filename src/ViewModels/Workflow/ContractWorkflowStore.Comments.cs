using System.Text.Json;
using CbsContractsDesktopClient.Models.Table;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class ContractWorkflowStore
    {
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
    }
}
