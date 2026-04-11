using System.Collections.Generic;

namespace CbsContractsDesktopClient.Models.Shell
{
    public sealed class AuditPanelState
    {
        public static AuditPanelState Empty { get; } = new()
        {
            Title = "Аудит",
            Description = "Выберите объект в рабочей области, чтобы увидеть связанные изменения и быстрые ссылки.",
            Entries =
            [
                new AuditEntry
                {
                    Timestamp = "Ожидание контекста",
                    Title = "Панель готова",
                    Description = "Здесь появится история изменений выбранного договора или документа.",
                    BackgroundBrushKey = "ShellAccentPanelBackgroundBrush"
                }
            ]
        };

        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public IReadOnlyList<AuditEntry> Entries { get; init; } = [];
    }
}
