using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Models.References
{
    public sealed class ReferenceDefinition
    {
        public required string Route { get; init; }

        public required string Model { get; init; }

        public required string Title { get; init; }

        public string? NavigationDescription { get; init; }

        public string Preset { get; init; } = "item";

        public string? Summary { get; init; }

        public ReferenceEditorKind EditorKind { get; init; } = ReferenceEditorKind.Generic;

        public bool IsAuditEnabled { get; init; }

        public IReadOnlyList<CbsTableColumnDefinition> Columns { get; init; } = [];

        public IReadOnlyList<ReferenceFieldDefinition> Fields { get; init; } = [];

        public string? InitialSortField { get; init; }

        public CbsContractsDesktopClient.Models.Data.DataSortDirection? InitialSortDirection { get; init; }

        public string Description => $"model={Model}, preset={Preset}";

        public string EffectiveNavigationDescription =>
            string.IsNullOrWhiteSpace(NavigationDescription)
                ? Title
                : NavigationDescription;

        public CbsTableDefinition Table => new()
        {
            Title = Title,
            Columns = Columns
        };

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
                InitialSortField = InitialSortField,
                InitialSortDirection = InitialSortDirection,
                Fields = Fields.Select(static field => new ReferenceFieldDefinition
                {
                    FieldKey = field.FieldKey,
                    Label = field.Label,
                    ApiField = field.ApiField,
                    EditorType = field.EditorType,
                    IsRequired = field.IsRequired,
                    IsReadOnlyOnCreate = field.IsReadOnlyOnCreate,
                    IsReadOnlyOnEdit = field.IsReadOnlyOnEdit
                }).ToList(),
                Columns = Columns.Select(static column => new CbsTableColumnDefinition
                {
                    FieldKey = column.FieldKey,
                    Header = column.Header,
                    ApiField = column.ApiField,
                    DisplayField = column.DisplayField,
                    FilterField = column.FilterField,
                    SortField = column.SortField,
                    DefaultWidth = column.DefaultWidth,
                    Width = column.Width,
                    IsSortable = column.IsSortable,
                    IsFilterable = column.IsFilterable,
                    Alignment = column.Alignment,
                    Filter = new CbsTableColumnFilterDefinition
                    {
                        IsEnabled = column.Filter.IsEnabled,
                        PlaceholderText = column.Filter.PlaceholderText,
                        EditorKind = column.Filter.EditorKind,
                        Mode = column.Filter.Mode,
                        MatchMode = column.Filter.MatchMode,
                        OptionsSourceKey = column.Filter.OptionsSourceKey,
                        StaticOptions = column.Filter.StaticOptions
                            .Select(static option => new CbsTableFilterOptionDefinition
                            {
                                Value = option.Value,
                                Label = option.Label
                            })
                            .ToList(),
                        EmptySelectionText = column.Filter.EmptySelectionText
                    },
                    BodyMode = column.BodyMode,
                    BodyTemplateKey = column.BodyTemplateKey
                }).ToList()
            };
        }
    }
}
