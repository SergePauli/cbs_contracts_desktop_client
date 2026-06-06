namespace CbsContractsDesktopClient.Models.Table
{
    public sealed class TableRowReplacedEventArgs(int index, object row) : EventArgs
    {
        public int Index { get; } = index;

        public object Row { get; } = row;
    }
}
