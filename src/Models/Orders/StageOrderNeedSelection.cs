namespace CbsContractsDesktopClient.Models.Orders
{
    public sealed record StageOrderNeedSelection(long Id, string ListKey);

    public sealed record CreateOrderFromNeedsInput(long ContragentId, string? Description);
}
