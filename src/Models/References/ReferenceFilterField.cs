using CommunityToolkit.Mvvm.ComponentModel;

namespace CbsContractsDesktopClient.Models.References
{
    public partial class ReferenceFilterField : ObservableObject
    {
        public required string FieldKey { get; init; }

        public required string Header { get; init; }

        [ObservableProperty]
        public partial string Value { get; set; } = string.Empty;
    }
}
