using System.Collections.Generic;

namespace CbsContractsDesktopClient.Models.Settings
{
    public sealed class LocalUserSettings
    {
        public Dictionary<string, LocalTableSettings> Tables { get; init; } = [];
    }

    public sealed class LocalTableSettings
    {
        public Dictionary<string, LocalTableColumnSettings> Columns { get; init; } = [];

        public LocalTableSortSettings? Sort { get; set; }
    }

    public sealed class LocalTableColumnSettings
    {
        public string? Width { get; set; }
    }

    public sealed class LocalTableSortSettings
    {
        public string? FieldKey { get; set; }

        public string? Direction { get; set; }
    }
}
