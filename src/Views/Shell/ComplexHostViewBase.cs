// Provides the common route, header actions, settings, and table plumbing for complex table host views.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions;
using CbsContractsDesktopClient.Stores.Table;
using CbsContractsDesktopClient.ViewModels.Shell;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.Shell
{
    public abstract class ComplexHostViewBase : ContentHostViewBase
    {
        private readonly ITablePageDefinitionService _tablePageDefinitionService;
        private readonly IDataQueryService _dataQueryService;
        private readonly AppShellViewModel _shellViewModel;
        private CancellationTokenSource? _routeCts;
        private CancellationTokenSource? _filterDebounceCts;
        private CancellationTokenSource? _viewportCts;
        private readonly StackPanel _primaryHeaderActionsPanel;
        private readonly StackPanel _secondaryHeaderActionsPanel;
        private readonly StackPanel _headerActionsHost;
        private readonly Border _headerActionSeparator;
        private readonly ContentControl _detailContentControl;
        private readonly Grid _tableHost;
        private readonly TextBlock _headerTitleTextBlock;
        private readonly TextBlock _placeholderTextBlock;
        private readonly ProgressRing _progressRing;
        private readonly InfoBar _errorInfoBar;
        private Button? _settingsButton;
        private MenuFlyoutItem? _configureColumnsItem;
        private string? _route;
        private bool _isLoaded;
        private bool _isStoreEventsSubscribed;

        protected ComplexHostViewBase()
        {
            Store = App.Services.GetRequiredService<TablePageStore>();
            _tablePageDefinitionService = App.Services.GetRequiredService<ITablePageDefinitionService>();
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _shellViewModel = App.Services.GetRequiredService<AppShellViewModel>();

            _tableHost = new Grid
            {
                Padding = new Thickness(0),
                Background = GetBrush("ShellMutedPanelBackgroundBrush"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            TableView = CreateTableHostView();

            _headerTitleTextBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis
            };
            _placeholderTextBlock = new TextBlock
            {
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis
            };
            _progressRing = new ProgressRing
            {
                Width = 16,
                Height = 16
            };
            _primaryHeaderActionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            _secondaryHeaderActionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            _headerActionSeparator = new Border
            {
                Width = 1,
                Height = 18,
                Margin = new Thickness(2, 0, 2, 0),
                Background = GetBrush("ShellTableGridLineBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            _headerActionsHost = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            _errorInfoBar = new InfoBar
            {
                IsOpen = false,
                Severity = InfoBarSeverity.Warning,
                Title = "Таблица недоступна"
            };
            _detailContentControl = new ContentControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Visibility = Visibility.Collapsed
            };

            Content = BuildLayout();
            DataContext = Store;
            WireTableEvents();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SubscribeStoreEvents();
            RefreshHeaderState();
        }

        public string? Route
        {
            get => _route;
            set
            {
                if (string.Equals(_route, value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _route = value;
                if (_isLoaded)
                {
                    _ = NavigateToRouteAsync(value);
                }
            }
        }

        protected TablePageStore Store { get; }

        protected OptionsSourceRegistry OptionsRegistry { get; } = new();

        protected TableHostView TableView { get; private set; }

        protected TablePageDefinition? CurrentDefinition { get; private set; }

        protected virtual IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            return [];
        }

        protected virtual int PrimaryHeaderActionCount => 0;

        protected virtual Task OnRouteLoaded(TablePageDefinition definition)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnRowSelected(TableDataRow? row)
        {
            return Task.CompletedTask;
        }

        protected virtual void OnRowSelectionChanged(TableDataRow? row, bool isSelected)
        {
        }

        protected virtual void OnTableQueryChanged()
        {
        }

        protected virtual void OnTableQueryApplied()
        {
        }

        protected virtual Task OnRowDoubleTapped(TableDataRow row)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OpenEditDialogAsync(TableDataRow? row)
        {
            return Task.CompletedTask;
        }

        protected virtual string BuildSelectedFooterText(TableDataRow row)
        {
            return string.Empty;
        }

        protected virtual Task OnTableRowRefreshedAfterSaveAsync(TableDataRow freshRow)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnTableReloadedAfterSaveAsync()
        {
            return Task.CompletedTask;
        }

        protected void SetDetailContent(UIElement? content)
        {
            SetDetailContent(content, isVisible: content is not null);
        }

        protected void SetDetailContent(UIElement? content, bool isVisible)
        {
            if (content is FrameworkElement element)
            {
                element.HorizontalAlignment = HorizontalAlignment.Stretch;
            }

            _detailContentControl.Content = content;
            SetDetailContentVisible(content is not null && isVisible);
        }

        protected void SetDetailContentVisible(bool isVisible)
        {
            var nextVisibility = isVisible && _detailContentControl.Content is not null
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (_detailContentControl.Visibility != nextVisibility)
            {
                Store.AppendUiTrace(
                    $"DETAIL VISIBILITY host={GetType().Name} old={_detailContentControl.Visibility} next={nextVisibility} content={_detailContentControl.Content?.GetType().Name ?? "<null>"}");
            }

            _detailContentControl.Visibility = nextVisibility;
        }

        protected void RebuildHeaderActions()
        {
            _primaryHeaderActionsPanel.Children.Clear();
            _secondaryHeaderActionsPanel.Children.Clear();

            var actions = BuildHeaderActions()
                .Where(static action => action is not null)
                .ToList();
            var primaryActionCount = Math.Clamp(PrimaryHeaderActionCount, 0, actions.Count);

            foreach (var action in actions.Take(primaryActionCount))
            {
                _primaryHeaderActionsPanel.Children.Add(action);
            }

            _secondaryHeaderActionsPanel.Children.Add(_progressRing);
            foreach (var action in actions.Skip(primaryActionCount))
            {
                _secondaryHeaderActionsPanel.Children.Add(action);
            }

            _secondaryHeaderActionsPanel.Children.Add(CreateResetFiltersButton());
            _secondaryHeaderActionsPanel.Children.Add(CreateSettingsButton());
            _headerActionSeparator.Visibility =
                _primaryHeaderActionsPanel.Children.Count > 0 && _secondaryHeaderActionsPanel.Children.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        protected void RefreshSelectedFooterText()
        {
            QueueSelectedFooterTextUpdate();
        }

        protected virtual async Task RefreshTableRowAfterSaveAsync(
            bool isCreateMode,
            TableDataRow? savedRow,
            CancellationToken cancellationToken = default)
        {
            var isCountChanged = isCreateMode && await Store.RefreshCountAfterCreateAsync(cancellationToken);
            if (isCountChanged)
            {
                await Store.RefreshViewportAfterCreateAsync(cancellationToken);
                TableView.InvalidateRows(new TableRenderRequest(TableRenderReason.PageLoaded));
            }

            if (isCreateMode)
            {
                return;
            }

            var id = savedRow is null ? null : TryGetSelectedRowId(savedRow);
            if (id is null)
            {
                await ReloadTableAfterSaveAsync(cancellationToken);
                return;
            }

            if (await RefreshTableRowByIdAsync(id.Value, cancellationToken))
            {
                return;
            }

            await ReloadTableAfterSaveAsync(cancellationToken);
        }

        protected void ApplyDeletedRowUpdate(long id)
        {
            Store.ApplyDeletedRowUpdate(id);
            TableView.InvalidateRows(new TableRenderRequest(TableRenderReason.PageLoaded));
        }

        protected virtual async Task<bool> RefreshTableRowByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            var freshRow = await Store.RefreshLoadedRowByIdAsync(id, cancellationToken);
            if (freshRow is null)
            {
                return false;
            }

            await OnTableRowRefreshedAfterSaveAsync(freshRow);
            return true;
        }

        private async Task ReloadTableAfterSaveAsync(CancellationToken cancellationToken)
        {
            await Store.ReloadCurrentReferenceAsync(cancellationToken);
            await OnTableReloadedAfterSaveAsync();
        }

        private FrameworkElement BuildLayout()
        {
            var root = new Grid
            {
                Padding = new Thickness(0)
            };

            var border = new Border
            {
                Margin = new Thickness(0),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Background = GetBrush("ShellPanelBackgroundBrush"),
                BorderBrush = GetBrush("ShellPanelBorderBrush")
            };
            root.Children.Add(border);

            var hostGrid = new Grid();
            hostGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            hostGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            hostGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            hostGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            border.Child = hostGrid;

            var header = new Grid
            {
                Padding = new Thickness(4, 2, 4, 2),
                Background = GetBrush("ShellPanelBackgroundBrush")
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(header, 0);
            hostGrid.Children.Add(header);

            _headerTitleTextBlock.Style = Application.Current.Resources["BodyStrongTextBlockStyle"] as Style;
            _headerTitleTextBlock.Foreground = GetBrush("ShellPrimaryTextBrush");
            _headerTitleTextBlock.Margin = new Thickness(8, 0, 8, 0);
            _headerTitleTextBlock.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(_headerTitleTextBlock, 2);
            header.Children.Add(_headerTitleTextBlock);

            Grid.SetColumn(_placeholderTextBlock, 1);
            _placeholderTextBlock.Foreground = GetBrush("ShellSecondaryTextBrush");
            header.Children.Add(_placeholderTextBlock);

            _headerActionsHost.Children.Add(_primaryHeaderActionsPanel);
            _headerActionsHost.Children.Add(_headerActionSeparator);
            _headerActionsHost.Children.Add(_secondaryHeaderActionsPanel);
            Grid.SetColumn(_headerActionsHost, 0);
            header.Children.Add(_headerActionsHost);

            var errorHost = new Grid
            {
                Padding = new Thickness(8, 0, 8, 0),
                Background = GetBrush("ShellMutedPanelBackgroundBrush")
            };
            Grid.SetRow(errorHost, 1);
            errorHost.Children.Add(_errorInfoBar);
            hostGrid.Children.Add(errorHost);

            Grid.SetRow(_tableHost, 2);
            _tableHost.Children.Add(TableView);
            hostGrid.Children.Add(_tableHost);

            Grid.SetRow(_detailContentControl, 3);
            _detailContentControl.HorizontalAlignment = HorizontalAlignment.Stretch;
            hostGrid.Children.Add(_detailContentControl);

            return root;
        }

        private void WireTableEvents()
        {
            TableView.ColumnWidthChanged += TableView_ColumnWidthChanged;
            TableView.FilterRequested += TableView_FilterRequested;
            TableView.LoadMoreRequested += TableView_LoadMoreRequested;
            TableView.RowDoubleTapped += TableView_RowDoubleTapped;
            TableView.RowSelectionChanged += TableView_RowSelectionChanged;
            TableView.SortRequested += TableView_SortRequested;
            TableView.TraceGenerated += TableView_TraceGenerated;
            TableView.ViewportChanged += TableView_ViewportChanged;
        }

        private void UnwireTableEvents()
        {
            TableView.ColumnWidthChanged -= TableView_ColumnWidthChanged;
            TableView.FilterRequested -= TableView_FilterRequested;
            TableView.LoadMoreRequested -= TableView_LoadMoreRequested;
            TableView.RowDoubleTapped -= TableView_RowDoubleTapped;
            TableView.RowSelectionChanged -= TableView_RowSelectionChanged;
            TableView.SortRequested -= TableView_SortRequested;
            TableView.TraceGenerated -= TableView_TraceGenerated;
            TableView.ViewportChanged -= TableView_ViewportChanged;
        }

        private TableHostView CreateTableHostView()
        {
            return new TableHostView
            {
                Density = CbsTableDensity.Compact,
                SupportsMultipleRowSelection = false,
                SupportsRowSelection = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0)
            };
        }

        private void RecreateTableHostView()
        {
            _viewportCts?.Cancel();
            UnwireTableEvents();
            _tableHost.Children.Clear();
            TableView = CreateTableHostView();
            _tableHost.Children.Add(TableView);
            WireTableEvents();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            SubscribeStoreEvents();
            RebuildHeaderActions();

            if (!string.IsNullOrWhiteSpace(Route))
            {
                await NavigateToRouteAsync(Route);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = false;
            _routeCts?.Cancel();
            _filterDebounceCts?.Cancel();
            _viewportCts?.Cancel();
            UnsubscribeStoreEvents();
        }

        private void SubscribeStoreEvents()
        {
            if (_isStoreEventsSubscribed)
            {
                return;
            }

            Store.PropertyChanged += OnStorePropertyChanged;
            _isStoreEventsSubscribed = true;
        }

        private void UnsubscribeStoreEvents()
        {
            if (!_isStoreEventsSubscribed)
            {
                return;
            }

            Store.PropertyChanged -= OnStorePropertyChanged;
            _isStoreEventsSubscribed = false;
        }

        private async Task NavigateToRouteAsync(string? route)
        {
            _routeCts?.Cancel();
            _routeCts = new CancellationTokenSource();

            try
            {
                if (!_tablePageDefinitionService.TryGetByRoute(route, out var definition))
                {
                    CurrentDefinition = null;
                    TableView.DetachTableState();
                    await Store.NavigateToRouteAsync(route, _routeCts.Token);
                    RefreshHeaderState();
                    return;
                }

                CurrentDefinition = definition;
                RecreateTableHostView();
                await Store.NavigateToRouteAsync(definition.Route, _routeCts.Token);
                OptionsRegistry.ReplaceWith(Store.CurrentFilterOptionsSources);
                AttachCurrentStoreRowsToTableView(definition);
                TableView.ApplyFilterInputs(Store.CurrentFilters);
                RefreshHeaderState();
                await OnRouteLoaded(definition);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TablePageStore.CurrentTablePage)
                || e.PropertyName == nameof(TablePageStore.CurrentRowStyleKey))
            {
                CurrentDefinition = Store.CurrentTablePage;
            }

            if (e.PropertyName == nameof(TablePageStore.CurrentFilterOptionsSources))
            {
                OptionsRegistry.ReplaceWith(Store.CurrentFilterOptionsSources);
                TableView.SetFilterOptionsSources(OptionsRegistry.Snapshot());
            }

            if (e.PropertyName == nameof(TablePageStore.Rows)
                && CurrentDefinition is not null)
            {
                AttachCurrentStoreRowsToTableView(CurrentDefinition);
                TableView.ApplyFilterInputs(Store.CurrentFilters);
            }

            if (e.PropertyName == nameof(TablePageStore.CurrentSortField)
                || e.PropertyName == nameof(TablePageStore.CurrentSortDirection))
            {
                TableView.RefreshSortSnapshot(BuildCurrentSorts());
            }

            if (e.PropertyName == nameof(TablePageStore.SelectedRow))
            {
                TableView.SetSelectedRow(Store.SelectedRow);
            }

            if (e.PropertyName == nameof(TablePageStore.SelectedRow))
            {
                QueueSelectedFooterTextUpdate();
                _ = OnRowSelected(Store.SelectedRow);
            }

            if (e.PropertyName == nameof(TablePageStore.TotalCount)
                || e.PropertyName == nameof(TablePageStore.LoadedCount)
                || e.PropertyName == nameof(TablePageStore.ResidentCount)
                || e.PropertyName == nameof(TablePageStore.IsLoading))
            {
                QueueSelectedFooterTextUpdate();
            }

            if (e.PropertyName == nameof(TablePageStore.CompactHeaderText)
                || e.PropertyName == nameof(TablePageStore.PlaceholderMessage)
                || e.PropertyName == nameof(TablePageStore.IsLoading)
                || e.PropertyName == nameof(TablePageStore.HasActiveReference)
                || e.PropertyName == nameof(TablePageStore.ShowPlaceholder)
                || e.PropertyName == nameof(TablePageStore.HasError)
                || e.PropertyName == nameof(TablePageStore.ErrorMessage))
            {
                RefreshHeaderState();
            }
        }

        private async void TableView_SortRequested(object? sender, CbsTableSortRequestedEventArgs e)
        {
            OnTableQueryChanged();
            if (e.Direction.HasValue)
            {
                await Store.ApplySortAsync(e.FieldKey, e.Direction.Value);
            }
            else
            {
                await Store.ClearSortsAsync();
            }

            TableView.InvalidateRows(new TableRenderRequest(
                TableRenderReason.SortChanged,
                ResetScroll: true));
        }

        private async void TableView_LoadMoreRequested(object? sender, CbsTableLoadMoreRequestedEventArgs e)
        {
            await Store.LoadMoreAsync();
        }

        private async void TableView_RowSelectionChanged(object? sender, CbsTableRowSelectionChangedEventArgs e)
        {
            if (!TableView.SupportsMultipleRowSelection)
            {
                Store.SelectedRow = e.IsSelected ? e.Row : null;
            }

            OnRowSelectionChanged(e.Row, e.IsSelected);
            QueueSelectedFooterTextUpdate();
            await Task.CompletedTask;
        }

        private async void TableView_RowDoubleTapped(object? sender, CbsTableRowDoubleTappedEventArgs e)
        {
            if (!TableView.SupportsMultipleRowSelection)
            {
                Store.SelectedRow = e.Row;
            }

            QueueSelectedFooterTextUpdate();
            await OnRowDoubleTapped(e.Row);
            await OpenEditDialogAsync(e.Row);
        }

        private async void TableView_FilterRequested(object? sender, CbsTableFilterRequestedEventArgs e)
        {
            OnTableQueryChanged();
            Store.AppendUiTrace(
                $"FILTER UI REQUEST field={e.FieldKey} mode={e.MatchMode} value={DescribeFilterValue(e.Value)}");
            _filterDebounceCts?.Cancel();
            var cancellationTokenSource = new CancellationTokenSource();
            _filterDebounceCts = cancellationTokenSource;

            try
            {
                await Task.Delay(250, cancellationTokenSource.Token);
                await Store.ApplyFilterAsync(
                    e.FieldKey,
                    e.MatchMode,
                    e.Value,
                    cancellationTokenSource.Token);
                OnTableQueryApplied();
                TableView.InvalidateRows(new TableRenderRequest(
                    TableRenderReason.FilterChanged,
                    ResetScroll: true));
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async void TableView_ColumnWidthChanged(object? sender, CbsTableColumnWidthChangedEventArgs e)
        {
            await Store.SaveColumnWidthAsync(e.FieldKey, e.Width);
        }

        private void TableView_TraceGenerated(object? sender, CbsTableTraceEventArgs e)
        {
            Store.AppendUiTrace(e.Message);
        }

        private void AttachCurrentStoreRowsToTableView(TablePageDefinition definition)
        {
            if (Store.Rows is null)
            {
                TableView.DetachTableState();
                return;
            }

            TableView.AttachTableRows(
                definition,
                Store.Rows,
                BuildCurrentSorts(),
                OptionsRegistry.Snapshot());
        }

        protected void RefreshCurrentTableDefinitionView()
        {
            if (CurrentDefinition is null)
            {
                return;
            }

            AttachCurrentStoreRowsToTableView(CurrentDefinition);
            TableView.ApplyFilterInputs(Store.CurrentFilters);
        }

        private IReadOnlyList<DataSortCriterion> BuildCurrentSorts()
        {
            return Store.CurrentSortField is not null && Store.CurrentSortDirection is DataSortDirection direction
                ? [new DataSortCriterion { FieldKey = Store.CurrentSortField, Direction = direction }]
                : [];
        }

        private async void TableView_ViewportChanged(object? sender, CbsTableViewportChangedEventArgs e)
        {
            Store.UpdateViewportRetention(
                e.StartIndex,
                e.EndIndex,
                e.RetainedBufferRows);

            _viewportCts?.Cancel();
            var cancellationTokenSource = new CancellationTokenSource();
            _viewportCts = cancellationTokenSource;

            try
            {
                await Store.EnsureViewportWindowLoadedAsync(
                    e.StartIndex,
                    e.EndIndex,
                    e.RetainedBufferRows,
                    cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task ResetFiltersAsync()
        {
            try
            {
                OnTableQueryChanged();
                var filters = await Store.ResetFiltersAsync();
                TableView.ApplyFilterInputs(filters);
                OnTableQueryApplied();
                TableView.InvalidateRows(new TableRenderRequest(
                    TableRenderReason.FilterChanged,
                    ResetScroll: true));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось сбросить фильтры", ex.Message);
            }
        }

        private async Task ClearFiltersAsync()
        {
            if (!await ConfirmDialogAsync(
                    "Сброс фильтров",
                    "Очистить все фильтры текущей таблицы?",
                    "Очистить"))
            {
                return;
            }

            try
            {
                OnTableQueryChanged();
                var filters = await Store.ClearFiltersAsync();
                TableView.ApplyFilterInputs(filters);
                OnTableQueryApplied();
                TableView.InvalidateRows(new TableRenderRequest(
                    TableRenderReason.FilterChanged,
                    ResetScroll: true));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось очистить фильтры", ex.Message);
            }
        }

        private async Task ResetColumnWidthsAsync()
        {
            try
            {
                await Store.ResetColumnWidthsAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось сбросить ширину колонок", ex.Message);
            }
        }

        private async Task ConfigureColumnsAsync()
        {
            if (!Store.CanConfigureColumns || Store.CurrentTablePage is null)
            {
                return;
            }

            try
            {
                var dialog = new TableColumnLayoutDialog(Store.CurrentTablePage.Columns)
                {
                    XamlRoot = XamlRoot
                };

                await dialog.ShowAsync();
                if (!dialog.WasApplied)
                {
                    return;
                }

                await Store.SaveColumnLayoutAsync(dialog.BuildColumns());

                var definition = Store.CurrentTablePage
                    ?? throw new InvalidOperationException("Table page must be available after column layout save.");

                AttachCurrentStoreRowsToTableView(definition);
                TableView.ApplyFilterInputs(Store.CurrentFilters);
                TableView.SetSelectedRow(Store.SelectedRow);
                TableView.InvalidateRows(new TableRenderRequest(TableRenderReason.PageLoaded));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось изменить раскладку столбцов", ex.Message);
            }
        }

        private async Task ResetSortingAsync()
        {
            try
            {
                OnTableQueryChanged();
                await Store.ClearSortsAsync();
                TableView.InvalidateRows(new TableRenderRequest(
                    TableRenderReason.SortChanged,
                    ResetScroll: true));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось сбросить сортировку", ex.Message);
            }
        }

        private Button CreateResetFiltersButton()
        {
            var button = CreateHeaderIconButton("\uE71C", "Начальные настройки фильтрации");
            button.Content = FilterIconFactory.BuildFilterClearIcon();
            button.Click += async (_, _) => await ResetFiltersAsync();
            return button;
        }

        private Button CreateSettingsButton()
        {
            _settingsButton = CreateHeaderIconButton("\uE713", "Настройки таблицы");
            var flyout = new MenuFlyout();

            _configureColumnsItem = new MenuFlyoutItem
            {
                Text = "Расстановка столбцов",
                IsEnabled = Store.CanConfigureColumns
            };
            _configureColumnsItem.Click += async (_, _) => await ConfigureColumnsAsync();
            flyout.Items.Add(_configureColumnsItem);

            var resetWidthsItem = new MenuFlyoutItem { Text = "Сбросить ширину" };
            resetWidthsItem.Click += async (_, _) => await ResetColumnWidthsAsync();
            flyout.Items.Add(resetWidthsItem);

            var resetFiltersItem = new MenuFlyoutItem { Text = "Сбросить фильтры" };
            resetFiltersItem.Click += async (_, _) => await ClearFiltersAsync();
            flyout.Items.Add(resetFiltersItem);

            var resetSortItem = new MenuFlyoutItem { Text = "Сбросить сортировку" };
            resetSortItem.Click += async (_, _) => await ResetSortingAsync();
            flyout.Items.Add(resetSortItem);

            _settingsButton.Flyout = flyout;
            ApplyDefaultActionButtonState(_settingsButton, Store.HasActiveReference);
            return _settingsButton;
        }

        protected static Button CreateHeaderIconButton(string iconGlyph, string tooltip)
        {
            var size = (double)Application.Current.Resources["ShellActionButtonSize"];
            var button = new Button
            {
                Width = size,
                Height = size,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = null,
                Content = iconGlyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 14,
                Foreground = GetBrush("ShellSecondaryTextBrush")
            };
            ToolTipService.SetToolTip(button, tooltip);
            return button;
        }

        protected static void ApplyEditButtonState(Button? button, bool isEnabled)
        {
            ApplyHeaderActionButtonState(button, isEnabled, new SolidColorBrush(Microsoft.UI.Colors.RoyalBlue));
        }

        protected static void ApplyCreateButtonState(Button? button, bool isEnabled)
        {
            ApplyHeaderActionButtonState(button, isEnabled, new SolidColorBrush(Microsoft.UI.Colors.ForestGreen));
        }

        protected static void ApplyDeleteButtonState(Button? button, bool isEnabled)
        {
            ApplyHeaderActionButtonState(button, isEnabled, new SolidColorBrush(Microsoft.UI.Colors.Firebrick));
        }

        protected static void ApplyDefaultActionButtonState(Button? button, bool isEnabled)
        {
            ApplyHeaderActionButtonState(button, isEnabled, GetBrush("ShellPrimaryTextBrush"));
        }

        protected static void ApplyHeaderActionButtonState(Button? button, bool isEnabled, Brush activeForeground)
        {
            if (button is null)
            {
                return;
            }

            button.IsEnabled = isEnabled;
            button.Foreground = isEnabled
                ? activeForeground
                : GetBrush("ShellSecondaryTextBrush");
        }

        private void RefreshHeaderState()
        {
            _headerTitleTextBlock.Text = Store.CompactHeaderText;
            _placeholderTextBlock.Text = Store.PlaceholderMessage;
            _progressRing.IsActive = Store.IsLoading;
            _progressRing.Visibility = Store.IsLoading ? Visibility.Visible : Visibility.Collapsed;
            _errorInfoBar.IsOpen = Store.HasError;
            _errorInfoBar.Message = Store.ErrorMessage;
            _headerTitleTextBlock.Visibility = Store.HasActiveReference ? Visibility.Visible : Visibility.Collapsed;
            _placeholderTextBlock.Visibility = Store.ShowPlaceholder ? Visibility.Visible : Visibility.Collapsed;
            TableView.Visibility = Store.HasActiveReference ? Visibility.Visible : Visibility.Collapsed;
            TableView.RowStyleKey = Store.CurrentRowStyleKey;
            ApplyDefaultActionButtonState(_settingsButton, Store.HasActiveReference);
            if (_configureColumnsItem is not null)
            {
                _configureColumnsItem.IsEnabled = Store.CanConfigureColumns;
            }
        }

        private void UpdateSelectedFooterText()
        {
            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                _shellViewModel.SetFooterTableStats(Store.TotalCount.ToString(), string.Empty);
                return;
            }

            var text = BuildSelectedFooterText(Store.SelectedRow);
            _shellViewModel.SetFooterTableStats(Store.TotalCount.ToString(), text);
        }

        private void QueueSelectedFooterTextUpdate()
        {
            DispatcherQueue.TryEnqueue(UpdateSelectedFooterText);
        }

        private static Brush? GetBrush(string resourceKey)
        {
            return Application.Current.Resources[resourceKey] as Brush;
        }

        private static string DescribeFilterValue(object? value)
        {
            if (value is null)
            {
                return "<empty>";
            }

            if (value is string text)
            {
                return string.IsNullOrWhiteSpace(text) ? "<empty>" : text;
            }

            if (value is System.Collections.IEnumerable sequence)
            {
                var items = sequence.Cast<object?>().ToArray();
                return items.Length == 0
                    ? "<empty>"
                    : $"[{string.Join(", ", items.Select(static item => item?.ToString() ?? "null"))}]";
            }

            return value.ToString() ?? "<empty>";
        }

    }
}
