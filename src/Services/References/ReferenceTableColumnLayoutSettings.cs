namespace CbsContractsDesktopClient.Services.References
{
    public sealed class ReferenceTableColumnLayoutSettings
    {
        public required string Route { get; init; }

        public IReadOnlyList<string> OrderedFieldKeys { get; init; } = [];

        public IReadOnlyList<string> VisibleFieldKeys { get; init; } = [];
    }
}
