namespace CbsContractsDesktopClient.Models.References
{
    public sealed class ReferenceColumnDefinition
    {
        public required string Key { get; init; }

        public required string Header { get; init; }

        public string? ApiField { get; init; }

        public ReferenceColumnKind Kind { get; init; } = ReferenceColumnKind.Text;

        public bool IsTextFilterEnabled => Kind == ReferenceColumnKind.Text;
    }
}
