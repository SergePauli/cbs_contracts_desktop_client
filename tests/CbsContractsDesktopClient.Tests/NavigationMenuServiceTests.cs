using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services.Navigation;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;
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

        Assert.Contains(
            menu.SelectMany(static section => section.Items),
            static item => item.Route == "/references/IsecurityTool");
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

        Assert.Contains(
            menu.SelectMany(static section => section.Items),
            static item => item.Route == "/references/IsecurityTool");
    }

    [Fact]
    public void BuildMenu_CommerUser_SeesIsecurityToolReference()
    {
        var service = new NavigationMenuService();
        var user = new User
        {
            Role = "user",
            DepartmentId = 2
        };

        var menu = service.BuildMenu(user);

        Assert.Contains(
            menu.SelectMany(static section => section.Items),
            static item => item.Route == "/references/IsecurityTool");
    }

    [Fact]
    public void BuildMenu_RegularUser_DoesNotSeeIsecurityToolReference()
    {
        var service = new NavigationMenuService();
        var user = new User
        {
            Role = "user",
            DepartmentId = 99
        };

        var menu = service.BuildMenu(user);

        Assert.DoesNotContain(
            menu.SelectMany(static section => section.Items),
            static item => item.Route == "/references/IsecurityTool");
    }

    [Fact]
    public void BuildMenu_AdminUser_SeesUsersReferenceRoute()
    {
        var service = new NavigationMenuService();
        var user = new User
        {
            Role = "admin",
            DepartmentId = 99
        };

        var menu = service.BuildMenu(user);

        Assert.Contains(
            menu.SelectMany(static section => section.Items),
            static item => item.Route == "/users");
    }

    [Fact]
    public void BuildMenu_UsesReferenceTitle_ForUsersItem()
    {
        var service = new NavigationMenuService(new FakeReferenceDefinitionService());
        var user = new User
        {
            Role = "admin",
            DepartmentId = 99
        };

        var menu = service.BuildMenu(user);
        var usersItem = menu
            .SelectMany(static section => section.Items)
            .Single(static item => item.Route == "/users");

        Assert.Equal("\u041f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b\u0438", usersItem.Title);
    }

    [Fact]
    public void BuildMenu_UsesFunctionalTableTitles_AsMenuFallbacks()
    {
        var service = new NavigationMenuService(
            tablePageDefinitionService: new FakeTablePageDefinitionService(
                ("/contracts", "Контракты"),
                ("/stages", "Этапы"),
                ("/revisions", "ДСоглашения")));
        var user = new User
        {
            Role = "admin",
            DepartmentId = 99
        };

        var menu = service.BuildMenu(user);
        var baseItems = menu.Single(static section => section.Title == "База").Items;

        Assert.Equal("Контракты", baseItems.Single(static item => item.Route == "/contracts").Title);
        Assert.Equal("Этапы", baseItems.Single(static item => item.Route == "/stages").Title);
        Assert.Equal("ДСоглашения", baseItems.Single(static item => item.Route == "/revisions").Title);
    }

    [Fact]
    public void BuildMenu_ShowsUnregisteredRoute_ForMissingFunctionalTableDefinition()
    {
        var service = new NavigationMenuService(tablePageDefinitionService: new FakeTablePageDefinitionService());
        var user = new User
        {
            Role = "admin",
            DepartmentId = 99
        };

        var menu = service.BuildMenu(user);
        var contractsItem = menu
            .Single(static section => section.Title == "База")
            .Items
            .Single(static item => item.Route == "/contracts");

        Assert.Equal("/contracts (маршрут не зарегистрирован)", contractsItem.Title);
    }

    [Fact]
    public void BuildMenu_MovesDiagnosticsToSessionSection()
    {
        var service = new NavigationMenuService();
        var user = new User
        {
            Role = "admin",
            DepartmentId = 99
        };

        var menu = service.BuildMenu(user);
        var baseSection = menu.Single(static section => section.Title == "База");
        var sessionSection = menu.Single(static section => section.IsSessionSection);

        Assert.DoesNotContain(baseSection.Items, static item => item.Route == "/diagnostics");
        Assert.Equal(["/diagnostics", "/logout"], sessionSection.Items.Select(static item => item.Route));
    }

    [Fact]
    public void BuildMenu_ReferencesSection_IsExpandedByDefault()
    {
        var service = new NavigationMenuService();
        var user = new User
        {
            Role = "admin",
            DepartmentId = 99
        };

        var menu = service.BuildMenu(user);
        var referencesSection = menu.Single(static section => section.Title == "Справочники");

        Assert.True(referencesSection.IsCollapsible);
        Assert.True(referencesSection.IsExpanded);
    }

    [Fact]
    public void BuildMenu_InternUser_MovesDiagnosticsToSessionSection()
    {
        var service = new NavigationMenuService();
        var user = new User
        {
            Role = "intern",
            DepartmentId = 2
        };

        var menu = service.BuildMenu(user);
        var baseSection = menu.Single(static section => section.Title == "База");
        var sessionSection = menu.Single(static section => section.IsSessionSection);

        Assert.DoesNotContain(baseSection.Items, static item => item.Route == "/diagnostics");
        Assert.Equal(["/diagnostics", "/logout"], sessionSection.Items.Select(static item => item.Route));
    }

    private sealed class FakeReferenceDefinitionService : IReferenceDefinitionService
    {
        public bool TryGetByRoute(string? route, out ReferenceDefinition definition)
        {
            if (string.Equals(route, "/users", StringComparison.OrdinalIgnoreCase))
            {
                definition = new ReferenceDefinition
                {
                    Route = "/users",
                    Model = "Profile",
                    Title = "\u041f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b\u0438",
                    NavigationDescription = "РџСЂРѕС„РёР»Рё РїРѕР»СЊР·РѕРІР°С‚РµР»РµР№"
                };
                return true;
            }

            definition = null!;
            return false;
        }
    }

    private sealed class FakeTablePageDefinitionService : ITablePageDefinitionService
    {
        private readonly Dictionary<string, string> _titlesByRoute;

        public FakeTablePageDefinitionService(params (string Route, string Title)[] definitions)
        {
            _titlesByRoute = definitions.ToDictionary(
                static definition => definition.Route,
                static definition => definition.Title,
                StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetByRoute(string? route, out TablePageDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(route)
                && _titlesByRoute.TryGetValue(route, out var title))
            {
                definition = new TablePageDefinition
                {
                    Route = route,
                    Model = title,
                    Title = title
                };
                return true;
            }

            definition = null!;
            return false;
        }

        public Task SaveColumnWidthAsync(TableColumnWidthSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveSortAsync(TableSortSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveFiltersAsync(string route, IReadOnlyList<DataFilterCriterion> filters, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SaveColumnLayoutAsync(TableColumnLayoutSettings settings, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}


