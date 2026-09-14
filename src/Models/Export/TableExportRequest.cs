using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Models.Export;

public sealed record TableExportRequest(string Title, DataQueryRequest Query,
    IReadOnlyList<CbsTableColumnDefinition> Columns, CbsTableRowStyleKey RowStyleKey);

public sealed record TableExportProgress(int Written, int Total);
