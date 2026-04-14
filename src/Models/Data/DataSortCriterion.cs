namespace CbsContractsDesktopClient.Models.Data
{
    public sealed class DataSortCriterion
    {
        public required string FieldKey { get; init; }

        public required DataSortDirection Direction { get; init; }
    }
}
