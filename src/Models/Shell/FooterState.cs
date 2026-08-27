namespace CbsContractsDesktopClient.Models.Shell
{
    public sealed class FooterState
    {
        public static FooterState Empty { get; } = new()
        {
            DepartmentOrRole = "Отдел не определен",
            UserName = "Пользователь не определен",
            TotalCountValue = string.Empty,
            SelectedRecordText = string.Empty,
            SelectedRecordTasksText = string.Empty,
            SelectedRecordPerformersText = string.Empty,
            VersionText = "v1.0.8-beta"
        };

        public string DepartmentOrRole { get; init; } = string.Empty;

        public string UserName { get; init; } = string.Empty;

        public string TotalCountValue { get; init; } = string.Empty;

        public string SelectedRecordText { get; init; } = string.Empty;

        public string SelectedRecordFooterText { get; init; } = string.Empty;

        public string SelectedRecordTasksText { get; init; } = string.Empty;

        public string SelectedRecordPerformersText { get; init; } = string.Empty;

        public string VersionText { get; init; } = string.Empty;

        public bool HasSelectedRecordFooterText => !string.IsNullOrWhiteSpace(SelectedRecordFooterText);

        public bool HasSelectedRecordTasksText => !string.IsNullOrWhiteSpace(SelectedRecordTasksText);

        public bool HasSelectedRecordPerformersText => !string.IsNullOrWhiteSpace(SelectedRecordPerformersText);

        public string SelectedRecordSeparator =>
            string.IsNullOrWhiteSpace(SelectedRecordText)
                ? string.Empty
                : "|";

        public string SelectedRecordCaption =>
            string.IsNullOrWhiteSpace(SelectedRecordText)
                ? string.Empty
                : "Выбрана:";
    }
}
