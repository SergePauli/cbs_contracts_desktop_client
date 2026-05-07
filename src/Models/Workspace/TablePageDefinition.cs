using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Models.Workspace
{
    public sealed class TablePageDefinition
    {
        public required string Route { get; init; }

        public required string Model { get; init; }

        public required string Title { get; init; }

        public string? NavigationDescription { get; init; }

        public string Preset { get; init; } = "item";

        public string? Summary { get; init; }

        public TablePageKind Kind { get; init; } = TablePageKind.Functional;

        public TablePageCapabilities Capabilities { get; init; } =
            TablePageCapabilities.RowSelection
            | TablePageCapabilities.ResetFilters
            | TablePageCapabilities.PersistColumnWidths
            | TablePageCapabilities.PersistSort
            | TablePageCapabilities.Audit;

        public IReadOnlyList<CbsTableColumnDefinition> Columns { get; init; } = [];

        public string? InitialSortField { get; init; }

        public DataSortDirection? InitialSortDirection { get; init; }

        public IReadOnlyList<DataFilterCriterion> InitialFilters { get; init; } = [];

        public string AuditModel => Model;

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

        public TablePageDefinition Clone()
        {
            return new TablePageDefinition
            {
                Route = Route,
                Model = Model,
                Title = Title,
                NavigationDescription = NavigationDescription,
                Preset = Preset,
                Summary = Summary,
                Kind = Kind,
                Capabilities = Capabilities,
                InitialSortField = InitialSortField,
                InitialSortDirection = InitialSortDirection,
                InitialFilters = InitialFilters.Select(static filter => new DataFilterCriterion
                {
                    FieldKey = filter.FieldKey,
                    FilterMode = filter.FilterMode,
                    MatchMode = filter.MatchMode,
                    Value = filter.Value
                }).ToList(),
                Columns = Columns.Select(CloneColumn).ToList()
            };
        }

        private static CbsTableColumnDefinition CloneColumn(CbsTableColumnDefinition column)
        {
            return new CbsTableColumnDefinition
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
            };
        }
    }
}
