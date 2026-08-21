using CbsContractsDesktopClient.Models.Workspace;

namespace CbsContractsDesktopClient.Models.References
{
    public sealed class ReferenceDefinition : EntityTableDefinition
    {
        public ReferenceEditorKind EditorKind { get; init; } = ReferenceEditorKind.Generic;

        public bool IsAuditEnabled { get; init; }

        public bool IncludeIdOnCreate { get; init; }

        public IReadOnlyList<ReferenceFieldDefinition> Fields { get; init; } = [];

        public TablePageDefinition TablePage => ToTablePageDefinition();

        public TablePageDefinition ToTablePageDefinition()
        {
            return new TablePageDefinition
            {
                Route = Route,
                Model = Model,
                Title = Title,
                NavigationDescription = NavigationDescription,
                Preset = Preset,
                Summary = Summary,
                Kind = TablePageKind.Reference,
                Capabilities =
                    TablePageCapabilities.RowSelection
                    | TablePageCapabilities.Create
                    | TablePageCapabilities.Edit
                    | TablePageCapabilities.Delete
                    | TablePageCapabilities.ResetFilters
                    | TablePageCapabilities.PersistColumnWidths
                    | TablePageCapabilities.PersistSort
                    | TablePageCapabilities.PersistFilters
                    | TablePageCapabilities.Audit,
                Columns = Columns,
                InitialSortField = InitialSortField,
                InitialSortDirection = InitialSortDirection
            };
        }

        public ReferenceDefinition Clone()
        {
            return new ReferenceDefinition
            {
                Route = Route,
                Model = Model,
                Title = Title,
                NavigationDescription = NavigationDescription,
                Preset = Preset,
                Summary = Summary,
                EditorKind = EditorKind,
                IsAuditEnabled = IsAuditEnabled,
                IncludeIdOnCreate = IncludeIdOnCreate,
                InitialSortField = InitialSortField,
                InitialSortDirection = InitialSortDirection,
                Fields = Fields.Select(static field => new ReferenceFieldDefinition
                {
                    FieldKey = field.FieldKey,
                    Label = field.Label,
                    ApiField = field.ApiField,
                    EditorType = field.EditorType,
                    EnumOptions = field.EnumOptions.ToList(),
                    IsRequired = field.IsRequired,
                    IsReadOnlyOnCreate = field.IsReadOnlyOnCreate,
                    IsReadOnlyOnEdit = field.IsReadOnlyOnEdit
                }).ToList(),
                Columns = CloneColumns(Columns)
            };
        }
    }
}
