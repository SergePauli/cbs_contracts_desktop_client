// Hosts simple reference routes with their own header, table, and detail placeholders.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.References;
using CbsContractsDesktopClient.ViewModels.Shell;
using CbsContractsDesktopClient.Stores.Table;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class ReferenceHostView : ContentHostViewBase
    {
        private readonly TablePageStore _viewModel;
        private readonly ITablePageDefinitionService _tablePageDefinitionService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly IModelMutationService _modelMutationService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private readonly AppShellViewModel _shellViewModel;
        private readonly OptionsSourceRegistry _optionsRegistry = new();
        private CancellationTokenSource? _routeCts;
        private CancellationTokenSource? _filterDebounceCts;
        private CancellationTokenSource? _viewportCts;
        private string? _route;
        private bool _isLoaded;

        public ReferenceHostView()
        {
            _viewModel = App.Services.GetRequiredService<TablePageStore>();
            _tablePageDefinitionService = App.Services.GetRequiredService<ITablePageDefinitionService>();
            _referenceDefinitionService = App.Services.GetRequiredService<IReferenceDefinitionService>();
            _modelMutationService = App.Services.GetRequiredService<IModelMutationService>();
            _referenceLookupCacheService = App.Services.GetRequiredService<IReferenceLookupCacheService>();
            _shellViewModel = App.Services.GetRequiredService<AppShellViewModel>();

            InitializeComponent();
            DataContext = _viewModel;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        public ReferenceHostView(string route)
            : this()
        {
            Route = route;
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

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            UpdateSelectionActionButtons();
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
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        private async Task NavigateToRouteAsync(string? route)
        {
            _routeCts?.Cancel();
            _routeCts = new CancellationTokenSource();

            try
            {
                if (!_tablePageDefinitionService.TryGetByRoute(route, out var definition)
                    || definition.Kind != TablePageKind.Reference)
                {
                    await _viewModel.NavigateToRouteAsync(route, _routeCts.Token);
                    return;
                }

                _ = _referenceDefinitionService.TryGetByRoute(route, out _);
                await _viewModel.NavigateToRouteAsync(definition.Route, _routeCts.Token);
                _optionsRegistry.ReplaceWith(_viewModel.CurrentFilterOptionsSources);
                AttachCurrentRowsToTableView(definition);
                ReferenceTableView.ApplyFilterInputs(_viewModel.CurrentFilters);
                UpdateFooterStats();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TablePageStore.SelectedRow)
                || e.PropertyName == nameof(TablePageStore.HasSelectedRow)
                || e.PropertyName == nameof(TablePageStore.HasActiveReference)
                || e.PropertyName == nameof(TablePageStore.TotalCount)
                || e.PropertyName == nameof(TablePageStore.CurrentTablePage)
                || e.PropertyName == nameof(TablePageStore.CanEditRows)
                || e.PropertyName == nameof(TablePageStore.CanDeleteRows))
            {
                UpdateSelectionActionButtons();
                UpdateFooterStats();
            }

            if (e.PropertyName == nameof(TablePageStore.CurrentTablePage)
                || e.PropertyName == nameof(TablePageStore.CurrentRowStyleKey))
            {
                ReferenceTableView.RowStyleKey = _viewModel.CurrentRowStyleKey;
            }

            if (e.PropertyName == nameof(TablePageStore.CurrentFilterOptionsSources))
            {
                _optionsRegistry.ReplaceWith(_viewModel.CurrentFilterOptionsSources);
                ReferenceTableView.SetFilterOptionsSources(_optionsRegistry.Snapshot());
            }

            if (e.PropertyName == nameof(TablePageStore.CurrentSortField)
                || e.PropertyName == nameof(TablePageStore.CurrentSortDirection))
            {
                ReferenceTableView.RefreshSortSnapshot(BuildCurrentSorts());
            }

            if (e.PropertyName == nameof(TablePageStore.SelectedRow))
            {
                ReferenceTableView.SetSelectedRow(_viewModel.SelectedRow);
            }
        }

        private async void CreateRowButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowReferenceEditDialogAsync(isCreateMode: true);
        }

        private async void EditSelectedRowButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowReferenceEditDialogAsync(isCreateMode: false);
        }

        private async void DeleteSelectedRowButton_Click(object sender, RoutedEventArgs e)
        {
            await DeleteSelectedRowAsync();
        }

        private async void ResetFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            await ResetFiltersAsync();
        }

        private async void ResetFiltersMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await ClearFiltersAsync();
        }

        private async void ResetColumnWidthsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ResetColumnWidthsAsync();
        }

        private async void ResetSortingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ClearSortsAsync();
            ReferenceTableView.InvalidateRows(new TableRenderRequest(
                TableRenderReason.SortChanged,
                ResetScroll: true));
        }

        private async void ReferenceTableView_SortRequested(object sender, CbsTableSortRequestedEventArgs e)
        {
            if (e.Direction.HasValue)
            {
                await _viewModel.ApplySortAsync(e.FieldKey, e.Direction.Value);
            }
            else
            {
                await _viewModel.ClearSortsAsync();
            }

            ReferenceTableView.InvalidateRows(new TableRenderRequest(
                TableRenderReason.SortChanged,
                ResetScroll: true));
        }

        private async void ReferenceTableView_LoadMoreRequested(object sender, CbsTableLoadMoreRequestedEventArgs e)
        {
            await _viewModel.LoadMoreAsync();
        }

        private void ReferenceTableView_RowSelectionChanged(object sender, CbsTableRowSelectionChangedEventArgs e)
        {
            _viewModel.SelectedRow = e.IsSelected ? e.Row : null;
            UpdateFooterStats();
        }

        private async void ReferenceTableView_RowDoubleTapped(object sender, CbsTableRowDoubleTappedEventArgs e)
        {
            _viewModel.SelectedRow = e.Row;
            UpdateFooterStats();
            await ShowReferenceEditDialogAsync(isCreateMode: false);
        }

        private async void ReferenceTableView_FilterRequested(object sender, CbsTableFilterRequestedEventArgs e)
        {
            _viewModel.AppendUiTrace(
                $"FILTER UI REQUEST field={e.FieldKey} mode={e.MatchMode} value={DescribeFilterValue(e.Value)}");
            _filterDebounceCts?.Cancel();
            var cancellationTokenSource = new CancellationTokenSource();
            _filterDebounceCts = cancellationTokenSource;

            try
            {
                await Task.Delay(250, cancellationTokenSource.Token);
                await _viewModel.ApplyFilterAsync(
                    e.FieldKey,
                    e.MatchMode,
                    e.Value,
                    cancellationTokenSource.Token);
                ReferenceTableView.InvalidateRows(new TableRenderRequest(
                    TableRenderReason.FilterChanged,
                    ResetScroll: true));
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async void ReferenceTableView_ColumnWidthChanged(object sender, CbsTableColumnWidthChangedEventArgs e)
        {
            await _viewModel.SaveColumnWidthAsync(e.FieldKey, e.Width);
        }

        private void ReferenceTableView_TraceGenerated(object sender, CbsTableTraceEventArgs e)
        {
            _viewModel.AppendUiTrace(e.Message);
        }

        private async void ReferenceTableView_ViewportChanged(object sender, CbsTableViewportChangedEventArgs e)
        {
            _viewModel.UpdateViewportRetention(
                e.StartIndex,
                e.EndIndex,
                e.RetainedBufferRows);

            _viewportCts?.Cancel();
            var cancellationTokenSource = new CancellationTokenSource();
            _viewportCts = cancellationTokenSource;

            try
            {
                await _viewModel.EnsureViewportWindowLoadedAsync(
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
                var filters = await _viewModel.ResetFiltersAsync();
                ReferenceTableView.ApplyFilterInputs(filters);
                ReferenceTableView.InvalidateRows(new TableRenderRequest(
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
                var filters = await _viewModel.ClearFiltersAsync();
                ReferenceTableView.ApplyFilterInputs(filters);
                ReferenceTableView.InvalidateRows(new TableRenderRequest(
                    TableRenderReason.FilterChanged,
                    ResetScroll: true));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось очистить фильтры", ex.Message);
            }
        }

        private async Task ShowReferenceEditDialogAsync(bool isCreateMode)
        {
            var reference = _viewModel.CurrentReference;
            if (reference is null)
            {
                return;
            }

            if (!isCreateMode && _viewModel.SelectedRow is null)
            {
                return;
            }

            var dialogViewModel = isCreateMode
                ? ReferenceEditViewModel.CreateForCreate(reference)
                : ReferenceEditViewModel.CreateForEdit(reference, _viewModel.SelectedRow!);

            var dialog = new ReferenceEditDialog(dialogViewModel)
            {
                XamlRoot = XamlRoot
            };

            TableDataRow? savedRow = null;

            dialog.SaveRequestedAsync += async args =>
            {
                var values = isCreateMode
                    ? ReferenceEditPayloadBuilder.BuildForCreate(dialogViewModel)
                    : ReferenceEditPayloadBuilder.BuildForUpdate(dialogViewModel);

                try
                {
                    savedRow = isCreateMode
                        ? await _modelMutationService.CreateAsync(reference.Model, values)
                        : await _modelMutationService.UpdateAsync(reference.Model, values);
                }
                catch (Exception ex)
                {
                    dialog.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };

            await dialog.ShowAsync();
            if (!dialog.WasSaved || savedRow is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(reference.Model);
            await RefreshReferenceAfterSaveAsync(isCreateMode, savedRow);
            ShowSuccessNotification(
                isCreateMode ? "Запись создана" : "Изменения сохранены",
                BuildReferenceNotificationMessage(reference.Title, TryGetSelectedRowId(savedRow)));
        }

        private async Task DeleteSelectedRowAsync()
        {
            if (_viewModel.CurrentReference is null || _viewModel.SelectedRow is null)
            {
                return;
            }

            var id = TryGetSelectedRowId(_viewModel.SelectedRow);
            if (id is null)
            {
                await ShowErrorDialogAsync(
                    "Не удалось удалить запись.",
                    "У выбранной записи отсутствует корректный ID.");
                return;
            }

            if (!await ConfirmDialogAsync(
                    "Удаление записи",
                    "Удалить выбранную запись?",
                    "Удалить",
                    defaultButton: ContentDialogButton.Close,
                    applyChrome: true))
            {
                return;
            }

            try
            {
                await _modelMutationService.DeleteAsync(_viewModel.CurrentReference.Model, id.Value);
                _referenceLookupCacheService.Invalidate(_viewModel.CurrentReference.Model);
                ApplyDeletedRowUpdate(id.Value);
                ShowSuccessNotification(
                    "Запись удалена",
                    BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, id.Value));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось удалить запись.", ex.Message);
            }
        }

        private void ApplyDeletedRowUpdate(long id)
        {
            _viewModel.ApplyDeletedRowUpdate(id);
            ReferenceTableView.InvalidateRows(new TableRenderRequest(TableRenderReason.PageLoaded));
        }

        private async Task RefreshReferenceAfterSaveAsync(
            bool isCreateMode,
            TableDataRow savedRow)
        {
            var isCountChanged = isCreateMode && await _viewModel.RefreshCountAfterCreateAsync();
            if (isCountChanged)
            {
                await _viewModel.RefreshViewportAfterCreateAsync();
                ReferenceTableView.InvalidateRows(new TableRenderRequest(TableRenderReason.PageLoaded));
            }

            if (isCreateMode)
            {
                return;
            }

            if (!_viewModel.ApplySavedRowUpdate(savedRow))
            {
                await _viewModel.ReloadCurrentReferenceAsync();
            }
        }

        private void UpdateSelectionActionButtons()
        {
            var hasSelectedRow = _viewModel.HasSelectedRow && _viewModel.HasActiveReference;
            var canEditSelectedRow = hasSelectedRow && _viewModel.CanEditRows;
            var canDeleteSelectedRow = hasSelectedRow && _viewModel.CanDeleteRows;

            if (EditSelectedRowButton is not null)
            {
                EditSelectedRowButton.IsEnabled = canEditSelectedRow;
                EditSelectedRowButton.Foreground = canEditSelectedRow
                    ? new SolidColorBrush(Colors.RoyalBlue)
                    : (Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }

            if (DeleteSelectedRowButton is not null)
            {
                DeleteSelectedRowButton.IsEnabled = canDeleteSelectedRow;
                DeleteSelectedRowButton.Foreground = canDeleteSelectedRow
                    ? new SolidColorBrush(Colors.Firebrick)
                    : (Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }
        }

        private void UpdateFooterStats()
        {
            if (!_viewModel.HasActiveReference)
            {
                _shellViewModel.SetFooterTableStats(string.Empty);
                return;
            }

            _shellViewModel.SetFooterTableStats(
                _viewModel.TotalCount.ToString(),
                BuildSelectedFooterText(_viewModel.SelectedRow));
        }

        private static string BuildSelectedFooterText(TableDataRow? row)
        {
            if (row is null || row.IsPlaceholder)
            {
                return string.Empty;
            }

            var name = row.GetValue("name")?.ToString();
            var id = row.GetValue("id")?.ToString();

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
            {
                return $"{name} (ID: {id})";
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return string.IsNullOrWhiteSpace(id)
                ? string.Empty
                : $"ID: {id}";
        }

        private void AttachCurrentRowsToTableView(TablePageDefinition definition)
        {
            if (_viewModel.Rows is null)
            {
                return;
            }

            ReferenceTableView.AttachTableRows(
                definition,
                _viewModel.Rows,
                BuildCurrentSorts(),
                _optionsRegistry.Snapshot());
        }

        private IReadOnlyList<DataSortCriterion> BuildCurrentSorts()
        {
            return _viewModel.CurrentSortField is not null
                && _viewModel.CurrentSortDirection is DataSortDirection direction
                ? [new DataSortCriterion { FieldKey = _viewModel.CurrentSortField, Direction = direction }]
                : [];
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

        private static string BuildReferenceNotificationMessage(string title, long? id)
        {
            return id.HasValue
                ? $"{title}, ID {id.Value}"
                : title;
        }
    }
}

