using System.Text.Json;
using CbsContractsDesktopClient.Models.Table;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class ContractWorkflowStore
    {
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
    }
}
