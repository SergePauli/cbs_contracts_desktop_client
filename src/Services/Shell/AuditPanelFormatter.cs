using System;

namespace CbsContractsDesktopClient.Services.Shell
{
    public static class AuditPanelFormatter
    {
        public const string AddedAction = "added";
        public const string UpdatedAction = "updated";
        public const string RemovedAction = "removed";
        public const string ArchivedAction = "archived";
        public const string ImportedAction = "imported";

        public static string GetActionTitle(string? action)
        {
            return NormalizeAction(action) switch
            {
                AddedAction => "Добавлено:",
                UpdatedAction => "Изменено:",
                "deleted" => "Удалено:",
                RemovedAction => "Удалено:",
                ArchivedAction => "Архивировано:",
                ImportedAction => "Импорт:",
                _ => string.IsNullOrWhiteSpace(action) ? "Событие:" : $"{action}:"
            };
        }

        public static string GetActionBrushKey(string? action)
        {
            return NormalizeAction(action) switch
            {
                AddedAction => "ShellTableRowSelectedBackgroundBrush",
                UpdatedAction => "ShellAccentPanelBackgroundBrush",
                "deleted" => "ShellAuditRemovedBackgroundBrush",
                RemovedAction => "ShellAuditRemovedBackgroundBrush",
                _ => "ShellAccentPanelBackgroundBrush"
            };
        }

        public static int? GetActionFilterValue(string? action)
        {
            return NormalizeAction(action) switch
            {
                AddedAction => 0,
                UpdatedAction => 1,
                RemovedAction => 2,
                ArchivedAction => 3,
                ImportedAction => 4,
                _ => null
            };
        }

        public static string NormalizeAction(string? action)
        {
            return string.IsNullOrWhiteSpace(action)
                ? string.Empty
                : action.Trim().TrimStart(':').ToLowerInvariant();
        }
    }
}
