namespace CbsContractsDesktopClient.Models.Orders
{
    public sealed record StageOrderStageOption(long Id, string Label);

    public sealed record IsecurityToolCatalogItem(long Id, string Name, decimal? DefaultCost)
    {
        public string Label => Name;
    }
}
