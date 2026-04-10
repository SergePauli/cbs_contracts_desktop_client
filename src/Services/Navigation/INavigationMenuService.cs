using System.Collections.Generic;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Models.Navigation;

namespace CbsContractsDesktopClient.Services.Navigation
{
    public interface INavigationMenuService
    {
        IReadOnlyList<NavigationMenuSection> BuildMenu(User? user, string? currentRoute = null);
    }
}
