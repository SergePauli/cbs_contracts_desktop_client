using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Models.References
{
    public sealed class ReferenceDefinition
    {
        public required string Route { get; init; }

        public required string Model { get; init; }

        public required string Title { get; init; }

        public string Preset { get; init; } = "item";

        public string? Summary { get; init; }

        public IReadOnlyList<CbsTableColumnDefinition> Columns { get; init; } = [];

        public string Description => $"model={Model}, preset={Preset}";

        public CbsTableDefinition Table => new()
        {
            Title = Title,
            Columns = Columns
        };
    }
}
