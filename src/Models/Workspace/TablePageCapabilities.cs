using System;

namespace CbsContractsDesktopClient.Models.Workspace
{
    [Flags]
    public enum TablePageCapabilities
    {
        None = 0,
        RowSelection = 1,
        Create = 2,
        Edit = 4,
        Delete = 8,
        ResetFilters = 16,
        PersistColumnWidths = 32,
        PersistSort = 64,
        Audit = 128,
        DetailFooter = 256,
        ConfigureColumns = 512
    }
}
