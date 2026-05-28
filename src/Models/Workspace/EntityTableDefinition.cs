// Contains shared entity/table metadata used by reference screens and functional table pages.
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Models.Workspace
{
    public abstract class EntityTableDefinition
    {
        public required string Route { get; init; }

        public required string Model { get; init; }

        public required string Title { get; init; }

        public string? NavigationDescription { get; init; }

        public string Preset { get; init; } = "item";

        public string? Summary { get; init; }

        public IReadOnlyList<CbsTableColumnDefinition> Columns { get; init; } = [];

        public string? InitialSortField { get; init; }

        public DataSortDirection? InitialSortDirection { get; init; }

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

        protected static IReadOnlyList<CbsTableColumnDefinition> CloneColumns(
            IReadOnlyList<CbsTableColumnDefinition> columns)
        {
            return columns.Select(CloneColumn).ToList();
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
                IsVisible = column.IsVisible,
                IsImmutable = column.IsImmutable,
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
                    Value = column.Filter.Value,
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
