using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Models.Workspace
{
    public sealed class TablePageDefinition : EntityTableDefinition
    {
        public TablePageKind Kind { get; init; } = TablePageKind.Functional;

        public TablePageCapabilities Capabilities { get; init; } =
            TablePageCapabilities.RowSelection
            | TablePageCapabilities.ResetFilters
            | TablePageCapabilities.PersistColumnWidths
            | TablePageCapabilities.PersistSort
            | TablePageCapabilities.Audit;

        public CbsTableRowStyleKey RowStyleKey { get; init; } = CbsTableRowStyleKey.None;

        public IReadOnlyList<DataFilterCriterion> InitialFilters { get; init; } = [];

        public string AuditModel => Model;

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
                Columns = CloneColumns(Columns),
                RowStyleKey = RowStyleKey
            };
        }
    }
}
