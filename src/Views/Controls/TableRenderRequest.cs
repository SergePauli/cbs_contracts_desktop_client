namespace CbsContractsDesktopClient.Views.Controls
{
    public enum TableRenderReason
    {
        Initial,
        QueryChanged,
        FilterChanged,
        SortChanged,
        PageLoaded,
        ScrollMoved,
        LayoutChanged,
        LoadingChanged,
        ValueStyleChanged
    }

    public sealed record TableRenderRequest(
        TableRenderReason Reason,
        bool ResetScroll = false);
}
