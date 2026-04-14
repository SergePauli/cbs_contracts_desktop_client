namespace CbsContractsDesktopClient.Models.Table
{
    public sealed class CbsTableColumnDefinition
    {
        public required string FieldKey { get; init; }

        public required string Header { get; init; }

        public string? ApiField { get; init; }

        public string? Width { get; init; }

        public bool IsSortable { get; init; } = true;

        public CbsTableColumnFilterDefinition Filter { get; init; } = new();

        public CbsTableBodyMode BodyMode { get; init; } = CbsTableBodyMode.Text;

        public string? BodyTemplateKey { get; init; }
    }
}
