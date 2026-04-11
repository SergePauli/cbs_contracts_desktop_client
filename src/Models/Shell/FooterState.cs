namespace CbsContractsDesktopClient.Models.Shell
{
    public sealed class FooterState
    {
        public static FooterState Empty { get; } = new()
        {
            DepartmentOrRole = "Отдел не определен",
            UserName = "Пользователь не определен",
            VersionText = "v1.0.0"
        };

        public string DepartmentOrRole { get; init; } = string.Empty;

        public string UserName { get; init; } = string.Empty;

        public string VersionText { get; init; } = string.Empty;

        public string SummaryText =>
            string.IsNullOrWhiteSpace(UserName)
                ? DepartmentOrRole
                : $"{DepartmentOrRole}: {UserName}";
    }
}
