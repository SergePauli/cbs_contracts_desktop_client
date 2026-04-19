using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Models.References
{
    public partial class ReferenceFilterField : ObservableObject
    {
        public required string FieldKey { get; init; }

        public required string Header { get; init; }

        public CbsTableFilterEditorKind EditorKind { get; init; } = CbsTableFilterEditorKind.Text;

        public string? OptionsSourceKey { get; init; }

        public IReadOnlyList<CbsTableFilterOptionDefinition> Options { get; init; } = [];

        public string EmptySelectionText { get; init; } = "\u0412\u0441\u0435";

        [ObservableProperty]
        public partial object? Value { get; set; }
    }
}
