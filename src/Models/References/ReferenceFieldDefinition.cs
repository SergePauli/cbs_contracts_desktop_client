namespace CbsContractsDesktopClient.Models.References
{
    public sealed class ReferenceFieldDefinition
    {
        public required string FieldKey { get; init; }

        public required string Label { get; init; }

        public string? ApiField { get; init; }

        public ReferenceFieldEditorType EditorType { get; init; } = ReferenceFieldEditorType.Text;

        public bool IsRequired { get; init; }

        public bool IsReadOnlyOnCreate { get; init; }

        public bool IsReadOnlyOnEdit { get; init; }
    }
}
