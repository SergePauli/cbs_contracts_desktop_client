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

        public ReferenceDefinition Clone()
        {
            return new ReferenceDefinition
            {
                Route = Route,
                Model = Model,
                Title = Title,
                Preset = Preset,
                Summary = Summary,
                Columns = Columns.Select(static column => new CbsTableColumnDefinition
                {
                    FieldKey = column.FieldKey,
                    Header = column.Header,
                    ApiField = column.ApiField,
                    DefaultWidth = column.DefaultWidth,
                    Width = column.Width,
                    IsSortable = column.IsSortable,
                    IsFilterable = column.IsFilterable,
                    Alignment = column.Alignment,
                    Filter = new CbsTableColumnFilterDefinition
                    {
                        IsEnabled = column.Filter.IsEnabled,
                        PlaceholderText = column.Filter.PlaceholderText,
                        MatchMode = column.Filter.MatchMode
                    },
                    BodyMode = column.BodyMode,
                    BodyTemplateKey = column.BodyTemplateKey
                }).ToList()
            };
        }
    }
}
