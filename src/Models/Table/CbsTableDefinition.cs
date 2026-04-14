namespace CbsContractsDesktopClient.Models.Table
{
    public sealed class CbsTableDefinition
    {
        public required string Title { get; init; }

        public IReadOnlyList<CbsTableColumnDefinition> Columns { get; init; } = [];
    }
}
