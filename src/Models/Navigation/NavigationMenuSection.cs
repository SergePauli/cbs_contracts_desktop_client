using System.Collections.Generic;

namespace CbsContractsDesktopClient.Models.Navigation
{
    public class NavigationMenuSection
    {
        public string Title { get; set; } = string.Empty;
        public List<NavigationMenuItem> Items { get; set; } = [];
        public bool IsSessionSection { get; set; }
        public bool IsCollapsible { get; set; }
        public bool IsExpanded { get; set; }
    }
}
