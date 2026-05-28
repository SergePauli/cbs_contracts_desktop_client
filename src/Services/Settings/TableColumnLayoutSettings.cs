// Contains the persisted column order and visibility for a workspace table.
namespace CbsContractsDesktopClient.Services.Settings
{
    public sealed class TableColumnLayoutSettings
    {
        public required string Route { get; init; }

        public IReadOnlyList<string> OrderedFieldKeys { get; init; } = [];

        public IReadOnlyList<string> VisibleFieldKeys { get; init; } = [];
    }
}
