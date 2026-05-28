// Contains the persisted sort state for a workspace table.
using CbsContractsDesktopClient.Models.Data;

namespace CbsContractsDesktopClient.Services.Settings
{
    public sealed class TableSortSettings
    {
        public required string Route { get; init; }

        public string? FieldKey { get; init; }

        public DataSortDirection? Direction { get; init; }
    }
}
