using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using CbsContractsDesktopClient.Models.Navigation;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Navigation;
using CbsContractsDesktopClient.ViewModels.Shell;
using CbsContractsDesktopClient.ViewModels.Workflow;
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
        private readonly ContractWorkflowStore _contractWorkflowStore;
        private readonly IReadOnlyList<NavigationMenuSection> _sections;

        public event Action? LogoutRequested;

        public NavigationSidebarView()
        {
            InitializeComponent();

            _userService = App.Services.GetRequiredService<IUserService>();
            _viewModel = App.Services.GetRequiredService<AppShellViewModel>();
            _contractWorkflowStore = App.Services.GetRequiredService<ContractWorkflowStore>();
            var navigationMenuService = App.Services.GetRequiredService<INavigationMenuService>();
            _sections = navigationMenuService.BuildMenu(_userService.CurrentUser, GetDefaultRoute());

            _viewModel.ContextNavigationItems.CollectionChanged += OnContextNavigationItemsChanged;
            _contractWorkflowStore.PropertyChanged += OnContractWorkflowStorePropertyChanged;
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
                if (!section.IsSessionSection
                    && string.Equals(section.Title, "Справочники", StringComparison.OrdinalIgnoreCase))
                {
                    AddFileSection();
                }

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

        private void AddFileSection()
        {
            var fileItems = BuildFileNavigationItems();
            if (fileItems.Count == 0)
            {
                return;
            }

            SidebarNavigationView.MenuItems.Add(new NavigationViewItemHeader
            {
                Content = "Файлы"
            });

            foreach (var item in fileItems)
            {
                SidebarNavigationView.MenuItems.Add(CreateNavigationItem(item));
            }
        }

        private IReadOnlyList<NavigationMenuItem> BuildFileNavigationItems()
        {
            var contract = _contractWorkflowStore.Contract;
            if (contract is null || contract.IsPlaceholder)
            {
                return [];
            }

            var revisions = ReadRevisions(contract);
            if (revisions.Count == 0)
            {
                return [];
            }

            var result = new List<NavigationMenuItem>();
            foreach (var revision in revisions)
            {
                var priority = ReadIntProperty(revision, "priority") ?? 0;
                AddFileItem(result, priority == 0 ? "договор" : $"допсогл_{priority}", "\uE8A5", ReadStringProperty(revision, "doc_link"));
                AddFileItem(result, priority == 0 ? "скан" : $"скан_{priority}", "\uE8A7", ReadStringProperty(revision, "scan_link"));
                AddFileItem(result, priority == 0 ? "протокол" : $"протокол_{priority}", "\uE9D2", ReadStringProperty(revision, "protocol_link"));
            }

            var folderPath = BuildFolderPath(revisions);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                AddFileItem(result, "каталог", "\uE8B7", folderPath);
            }

            return result;
        }

        private static void AddFileItem(
            ICollection<NavigationMenuItem> items,
            string title,
            string glyph,
            string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            items.Add(new NavigationMenuItem
            {
                Title = title,
                Glyph = glyph,
                Route = $"file-action:{items.Count}",
                FilePath = filePath,
                IsAction = true
            });
        }

        private static string? BuildFolderPath(IReadOnlyList<JsonElement> revisions)
        {
            var firstPath = revisions
                .Select(static revision =>
                    ReadStringProperty(revision, "doc_link")
                    ?? ReadStringProperty(revision, "scan_link")
                    ?? ReadStringProperty(revision, "protocol_link"))
                .FirstOrDefault(static path => !string.IsNullOrWhiteSpace(path));
            if (string.IsNullOrWhiteSpace(firstPath))
            {
                return null;
            }

            var lastBackslash = firstPath.LastIndexOf('\\');
            var lastSlash = firstPath.LastIndexOf('/');
            var index = Math.Max(lastBackslash, lastSlash);
            return index < 0 ? null : firstPath[..(index + 1)];
        }

        private static IReadOnlyList<JsonElement> ReadRevisions(ReferenceDataRow contract)
        {
            if (!contract.Values.TryGetValue("revisions", out var revisions)
                || revisions.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return revisions
                .EnumerateArray()
                .Where(static revision => revision.ValueKind == JsonValueKind.Object)
                .ToList();
        }

        private static string? ReadStringProperty(JsonElement item, string propertyName)
        {
            return item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static int? ReadIntProperty(JsonElement item, string propertyName)
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt32(out var intValue) => intValue,
                JsonValueKind.Number when value.TryGetInt64(out var longValue) => checked((int)longValue),
                JsonValueKind.String when int.TryParse(value.GetString(), out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private void SidebarNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer is not NavigationViewItem item || item.Tag is not string route)
            {
                return;
            }

            if (item.DataContext is NavigationMenuItem { IsAction: true } actionItem
                && !string.IsNullOrWhiteSpace(actionItem.FilePath))
            {
                OpenFileAction(actionItem.FilePath);
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

            if (item.DataContext is NavigationMenuItem { IsAction: true })
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

        private void OnContractWorkflowStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ContractWorkflowStore.Contract))
            {
                BuildNavigationMenu();
            }
        }

        private static void OpenFileAction(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                DiagnosticsFileLogger.AppendBlock(
                    "FILE NAVIGATION OPEN FAILED",
                    $"{filePath}{Environment.NewLine}{ex.GetType().Name}: {ex.Message}");
            }
        }

        private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _viewModel.ContextNavigationItems.CollectionChanged -= OnContextNavigationItemsChanged;
            _contractWorkflowStore.PropertyChanged -= OnContractWorkflowStorePropertyChanged;
            Unloaded -= OnUnloaded;
        }
    }
}
