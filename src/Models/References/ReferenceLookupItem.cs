using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Models.References
{
    public sealed class ReferenceLookupItem
    {
        public required string Model { get; init; }

        public required string Preset { get; init; }

        public object? Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string FullName { get; init; } = string.Empty;

        public string Code { get; init; } = string.Empty;

        public required ReferenceDataRow Row { get; init; }

        public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name : FullName;

        public CbsTableFilterOptionDefinition ToOption()
        {
            return new CbsTableFilterOptionDefinition
            {
                Value = Id,
                Label = DisplayName
            };
        }
    }
}
