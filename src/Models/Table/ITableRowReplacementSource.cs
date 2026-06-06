namespace CbsContractsDesktopClient.Models.Table
{
    public interface ITableRowReplacementSource
    {
        event EventHandler<TableRowReplacedEventArgs>? RowReplaced;
    }
}
