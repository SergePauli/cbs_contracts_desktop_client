using System.Collections.Generic;
using System.Linq;
using CbsContractsDesktopClient.Models.Navigation;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class NavigationSidebarView : UserControl
    {
        private readonly IUserService _userService;
        private readonly IReadOnlyList<NavigationMenuSection> _sections;
        public event Action? LogoutRequested;

        public NavigationSidebarView()
        {
            InitializeComponent();

            _userService = App.Services.GetRequiredService<IUserService>();
            var navigationMenuService = App.Services.GetRequiredService<INavigationMenuService>();
            _sections = navigationMenuService.BuildMenu(_userService.CurrentUser, GetDefaultRoute());

            BuildNavigationMenu();
        }

        private string GetDefaultRoute()
        {
            return _userService.CurrentUser?.Role?.Contains("intern") == true ? "/stages" : "/contracts";
        }

        private void BuildNavigationMenu()
        {
            SidebarNavigationView.MenuItems.Clear();
            SidebarNavigationView.FooterMenuItems.Clear();

            NavigationViewItem? selectedItem = null;

            foreach (var section in _sections)
            {
                if (!section.IsSessionSection)
                {
                    if (section.IsCollapsible)
                    {
                        SidebarNavigationView.MenuItems.Add(CreateSectionItem(section));
                        continue;
                    }

                    SidebarNavigationView.MenuItems.Add(new NavigationViewItemHeader
                    {
                        Content = section.Title
                    });
                }

                foreach (var item in section.Items)
                {
                    var navigationItem = CreateNavigationItem(item);

                    if (section.IsSessionSection)
                    {
                        SidebarNavigationView.FooterMenuItems.Add(navigationItem);
                    }
                    else
                    {
                        SidebarNavigationView.MenuItems.Add(navigationItem);
                    }

                    if (item.IsSelected)
                    {
                        selectedItem = navigationItem;
                    }
                }
            }

            if (selectedItem != null)
            {
                SidebarNavigationView.SelectedItem = selectedItem;
            }
        }

        private static NavigationViewItem CreateSectionItem(NavigationMenuSection section)
        {
            var sectionItem = new NavigationViewItem
            {
                Content = section.Title,
                SelectsOnInvoked = false,
                IsExpanded = section.IsExpanded
            };

            foreach (var item in section.Items)
            {
                sectionItem.MenuItems.Add(CreateNavigationItem(item));
            }

            return sectionItem;
        }

        private static NavigationViewItem CreateNavigationItem(NavigationMenuItem item)
        {
            return new NavigationViewItem
            {
                Content = item.Title,
                Tag = item.Route,
                SelectsOnInvoked = !item.IsAction,
                Icon = new FontIcon
                {
                    Glyph = item.Glyph,
                    FontFamily = new FontFamily("Segoe Fluent Icons")
                }
            };
        }

        private void SidebarNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is not NavigationViewItem item || item.Tag is not string route)
            {
                return;
            }

            if (string.Equals(route, "/logout", System.StringComparison.OrdinalIgnoreCase))
            {
                LogoutRequested?.Invoke();
                return;
            }

            UpdateSelection(route);
        }

        private void SidebarNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer is not NavigationViewItem item || item.Tag is not string route)
            {
                return;
            }

            if (string.Equals(route, "/logout", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            UpdateSelection(route);
        }

        private void UpdateSelection(string route)
        {
            foreach (var item in _sections.SelectMany(section => section.Items))
            {
                item.IsSelected = string.Equals(item.Route, route, System.StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
