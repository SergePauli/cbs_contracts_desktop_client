using System.Collections.Generic;

namespace CbsContractsDesktopClient.Models.Settings
{
    public sealed class LocalUserSettings
    {
        public Dictionary<string, LocalTableSettings> Tables { get; init; } = [];

        public bool ShowStageCostFraction { get; set; }

        public bool ShowContractCostFraction { get; set; }
    }

    public sealed class LocalTableSettings
    {
        public Dictionary<string, LocalTableColumnSettings> Columns { get; init; } = [];

        public List<string> ColumnOrder { get; set; } = [];

        public List<LocalTableFilterSettings> Filters { get; set; } = [];

        public LocalTableSortSettings? Sort { get; set; }
    }

    public sealed class LocalTableColumnSettings
    {
        public string? Width { get; set; }

        public bool? IsVisible { get; set; }
    }

    public sealed class LocalTableSortSettings
    {
        public string? FieldKey { get; set; }

        public string? Direction { get; set; }
    }

    public sealed class LocalTableFilterSettings
    {
        public string? FieldKey { get; set; }

        public string? FilterMode { get; set; }

        public string? MatchMode { get; set; }

        public object? Value { get; set; }
    }
}
