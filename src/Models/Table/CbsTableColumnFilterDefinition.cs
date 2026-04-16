using CbsContractsDesktopClient.Models.Data;

namespace CbsContractsDesktopClient.Models.Table
{
    public sealed class CbsTableColumnFilterDefinition
    {
        public bool IsEnabled { get; init; }

        public string PlaceholderText { get; init; } = string.Empty;

        public DataFilterMode Mode { get; init; } = DataFilterMode.Text;

        public DataFilterMatchMode MatchMode { get; set; } = DataFilterMatchMode.Contains;
    }
}
