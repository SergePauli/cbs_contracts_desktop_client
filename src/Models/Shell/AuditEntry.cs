namespace CbsContractsDesktopClient.Models.Shell
{
    public sealed class AuditEntry
    {
        public string Timestamp { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string BackgroundBrushKey { get; init; } = "ShellAccentPanelBackgroundBrush";
    }
}
