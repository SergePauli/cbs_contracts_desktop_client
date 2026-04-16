namespace CbsContractsDesktopClient.Models.Data
{
    public sealed class DataFilterCriterion
    {
        public required string FieldKey { get; init; }

        public DataFilterMode FilterMode { get; init; } = DataFilterMode.Text;

        public required DataFilterMatchMode MatchMode { get; init; }

        public object? Value { get; init; }
    }
}
