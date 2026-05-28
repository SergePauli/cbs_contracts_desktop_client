// Contains the persisted width change for a workspace table column.
namespace CbsContractsDesktopClient.Services.Settings
{
    public sealed class TableColumnWidthSettings
    {
        public required string Route { get; init; }

        public required string FieldKey { get; init; }

        public string? Width { get; init; }
    }
}
