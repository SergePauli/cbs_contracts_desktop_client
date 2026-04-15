namespace CbsContractsDesktopClient.Services.References
{
    public sealed class ReferenceTableColumnWidthSettings
    {
        public required string Route { get; init; }

        public required string FieldKey { get; init; }

        public string? Width { get; init; }
    }
}
