using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using CbsContractsDesktopClient.Models.Navigation;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Navigation;
using CbsContractsDesktopClient.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class NavigationSidebarView : UserControl
    {
        private readonly IUserService _userService;
        private readonly AppShellViewModel _viewModel;
        private readonly IReadOnlyList<NavigationMenuSection> _sections;

        public event Action? LogoutRequested;

        public NavigationSidebarView()
        {
            InitializeComponent();

            _userService = App.Services.GetRequiredService<IUserService>();
            _viewModel = App.Services.GetRequiredService<AppShellViewModel>();
            var navigationMenuService = App.Services.GetRequiredService<INavigationMenuService>();
            _sections = navigationMenuService.BuildMenu(_userService.CurrentUser, GetDefaultRoute());

            _viewModel.ContextNavigationItems.CollectionChanged += OnContextNavigationItemsChanged;
            Unloaded += OnUnloaded;

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
                    item.SectionTitle = section.Title;
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

            if (_viewModel.ContextNavigationItems.Count > 0)
            {
                SidebarNavigationView.MenuItems.Add(new NavigationViewItemHeader
                {
                    Content = "Контекст"
                });

                foreach (var item in _viewModel.ContextNavigationItems)
                {
                    var contextItem = CreateNavigationItem(item);
                    SidebarNavigationView.MenuItems.Add(contextItem);

                    if (item.IsSelected)
                    {
                        selectedItem = contextItem;
                    }
                }
            }

            if (selectedItem != null)
            {
                SidebarNavigationView.SelectedItem = selectedItem;

                if (selectedItem.DataContext is NavigationMenuItem selectedMenuItem)
                {
                    _viewModel.SetSelectedNavigationItem(selectedMenuItem);
                }
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
                item.SectionTitle = section.Title;
                var childItem = CreateNavigationItem(item);
                childItem.Margin = new Thickness(-14, 0, 2, 0);
                sectionItem.MenuItems.Add(childItem);
            }

            return sectionItem;
        }

        private static NavigationViewItem CreateNavigationItem(NavigationMenuItem item)
        {
            var icon = new FontIcon
            {
                Glyph = item.Glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = item.Title,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4
            };

            content.Children.Add(icon);
            content.Children.Add(label);

            return new NavigationViewItem
            {
                Content = content,
                DataContext = item,
                Tag = item.Route,
                SelectsOnInvoked = !item.IsAction
            };
        }

        private void SidebarNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is not NavigationViewItem item || item.Tag is not string route)
            {
                return;
            }

            if (string.Equals(route, "/logout", StringComparison.OrdinalIgnoreCase))
            {
                LogoutRequested?.Invoke();
            }
        }

        private void SidebarNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer is not NavigationViewItem item || item.Tag is not string route)
            {
                return;
            }

            if (string.Equals(route, "/logout", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            UpdateSelection(route);

            if (item.DataContext is NavigationMenuItem selectedMenuItem)
            {
                _viewModel.SetSelectedNavigationItem(selectedMenuItem);
            }
        }

        private void UpdateSelection(string route)
        {
            foreach (var item in _sections.SelectMany(section => section.Items).Concat(_viewModel.ContextNavigationItems))
            {
                item.IsSelected = string.Equals(item.Route, route, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void OnContextNavigationItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            BuildNavigationMenu();
        }

        private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _viewModel.ContextNavigationItems.CollectionChanged -= OnContextNavigationItemsChanged;
            Unloaded -= OnUnloaded;
        }
    }
}
