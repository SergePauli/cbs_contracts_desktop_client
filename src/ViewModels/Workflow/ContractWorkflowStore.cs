using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class ContractWorkflowStore : ObservableObject
    {
        [ObservableProperty]
        public partial ReferenceDataRow? SelectedRevision { get; set; }

        [ObservableProperty]
        public partial ReferenceDataRow? Contract { get; set; }

        [ObservableProperty]
        public partial ReferenceDataRow? Contragent { get; set; }

        [ObservableProperty]
        public partial int? FocusedRevisionPriority { get; set; }

        [ObservableProperty]
        public partial IReadOnlyList<ReferenceDataRow> Comments { get; set; } = [];

        public void SetRevisionSelection(
            ReferenceDataRow revision,
            ReferenceDataRow? contract,
            ReferenceDataRow? contragent)
        {
            SelectedRevision = revision;
            Contract = contract;
            Contragent = contragent;
            FocusedRevisionPriority = TryGetInt(revision.GetValue("priority"));
            Comments = ReadContractComments(contract);
        }

        public void ClearRevisionSelection()
        {
            SelectedRevision = null;
            Contract = null;
            Contragent = null;
            FocusedRevisionPriority = null;
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
                    if (stage.ValueKind == JsonValueKind.Object
                        && stage.TryGetProperty("comments", out var stageComments))
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

                target.Add(new ReferenceDataRow
                {
                    Values = comment
                        .EnumerateObject()
                        .ToDictionary(
                            static property => property.Name,
                            static property => property.Value,
                            StringComparer.OrdinalIgnoreCase)
                });
            }
        }

        private static JsonElement? TryGetArray(ReferenceDataRow row, string fieldKey)
        {
            return row.Values.TryGetValue(fieldKey, out var value)
                && value.ValueKind == JsonValueKind.Array
                ? value
                : null;
        }

        private static int? TryGetInt(object? value)
        {
            return value switch
            {
                int intValue => intValue,
                long longValue => checked((int)longValue),
                decimal decimalValue => checked((int)decimalValue),
                string text when int.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static long? TryGetLong(object? value)
        {
            return value switch
            {
                long longValue => longValue,
                int intValue => intValue,
                decimal decimalValue => (long)decimalValue,
                string text when long.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }
    }
}
