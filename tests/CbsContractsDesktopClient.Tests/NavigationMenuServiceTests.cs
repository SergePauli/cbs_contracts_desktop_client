using System.Linq;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Services.Navigation;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class NavigationMenuServiceTests
{
    [Fact]
    public void BuildMenu_AdminUser_SeesIsecurityToolReference()
    {
        var service = new NavigationMenuService();
        var user = new User
        {
            Role = "admin",
            DepartmentId = 99
        };

        var menu = service.BuildMenu(user);

        var references = Assert.Single(menu.Where(static section => section.Title == "Справочники"));
        Assert.Contains(references.Items, static item => item.Route == "/references/IsecurityTool");
    }

    [Fact]
    public void BuildMenu_OziUser_SeesIsecurityToolReference()
    {
        var service = new NavigationMenuService();
        var user = new User
        {
            Role = "user",
            DepartmentId = 1
        };

        var menu = service.BuildMenu(user);

        var references = Assert.Single(menu.Where(static section => section.Title == "Справочники"));
        Assert.Contains(references.Items, static item => item.Route == "/references/IsecurityTool");
    }

    [Fact]
    public void BuildMenu_RegularUser_DoesNotSeeIsecurityToolReference()
    {
        var service = new NavigationMenuService();
        var user = new User
        {
            Role = "user",
            DepartmentId = 2
        };

        var menu = service.BuildMenu(user);

        var references = Assert.Single(menu.Where(static section => section.Title == "Справочники"));
        Assert.DoesNotContain(references.Items, static item => item.Route == "/references/IsecurityTool");
    }
}
