namespace CbsContractsDesktopClient.Models.Shell
{
    public sealed class BreadcrumbItemState
    {
        public BreadcrumbItemState(string title, string route)
        {
            Title = title;
            Route = route;
        }

        public string Title { get; }

        public string Route { get; }

        public bool IsNavigable => !string.IsNullOrWhiteSpace(Route);

        public override string ToString()
        {
            return Title;
        }
    }
}
