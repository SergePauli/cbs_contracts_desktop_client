using CbsContractsDesktopClient.Models.Data;

namespace CbsContractsDesktopClient.Models.Table
{
    public sealed class CbsTableColumnFilterDefinition
    {
        public bool IsEnabled { get; init; }

        public string PlaceholderText { get; init; } = string.Empty;

        public CbsTableFilterEditorKind EditorKind { get; init; } = CbsTableFilterEditorKind.Text;

        public DataFilterMode Mode { get; init; } = DataFilterMode.Text;

        public DataFilterMatchMode MatchMode { get; set; } = DataFilterMatchMode.Contains;

        public string? OptionsSourceKey { get; init; }

        public IReadOnlyList<CbsTableFilterOptionDefinition> StaticOptions { get; init; } = [];

        public string EmptySelectionText { get; init; } = "\u0412\u0441\u0435";
    }
}
