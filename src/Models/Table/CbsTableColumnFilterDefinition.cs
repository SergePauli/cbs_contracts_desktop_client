using CbsContractsDesktopClient.Models.Data;

namespace CbsContractsDesktopClient.Models.Table
{
    public sealed class CbsTableColumnFilterDefinition
    {
        public bool IsEnabled { get; init; }

        public string PlaceholderText { get; init; } = string.Empty;

        public DataFilterMatchMode MatchMode { get; init; } = DataFilterMatchMode.Contains;
    }
}
