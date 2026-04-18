using CbsContractsDesktopClient.Models.Data;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class ReferenceTableSortSettings
    {
        public required string Route { get; init; }

        public string? FieldKey { get; init; }

        public DataSortDirection? Direction { get; init; }
    }
}
