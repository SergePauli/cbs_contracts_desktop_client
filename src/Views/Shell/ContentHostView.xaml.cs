using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.ViewModels.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class ContentHostView : UserControl
    {
        private readonly ReferencesContentViewModel _viewModel;
        private CancellationTokenSource? _filterDebounceCts;
        private CancellationTokenSource? _viewportCts;
        private bool _isViewportSubscribed;

        public ContentHostView()
        {
            _viewModel = App.Services.GetRequiredService<ReferencesContentViewModel>();
            InitializeComponent();
            DataContext = _viewModel;
            UpdateSelectionActionButtons();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateSelectionActionButtons();
            EnsureViewportSubscription();
            await _viewModel.EnsureLoadedAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _filterDebounceCts?.Cancel();
            _viewportCts?.Cancel();
            RemoveViewportSubscription();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReferencesContentViewModel.SelectedRow)
                || e.PropertyName == nameof(ReferencesContentViewModel.HasSelectedRow)
                || e.PropertyName == nameof(ReferencesContentViewModel.HasActiveReference))
            {
                UpdateSelectionActionButtons();
            }
        }

        private async void ResetFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            _filterDebounceCts?.Cancel();
            await _viewModel.ResetFiltersAsync();
            ReferenceTableView.ClearFilterInputs();
        }

        private void CreateRowButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AppendUiTrace("REFERENCE CREATE ACTION requested");
        }

        private void EditSelectedRowButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AppendUiTrace("REFERENCE EDIT ACTION requested");
        }

        private void DeleteSelectedRowButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AppendUiTrace("REFERENCE DELETE ACTION requested");
        }

        private async void ResetColumnWidthsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ResetColumnWidthsAsync();
        }

        private async void ResetFiltersMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _filterDebounceCts?.Cancel();
            await _viewModel.ResetFiltersAsync();
            ReferenceTableView.ClearFilterInputs();
        }

        private async void ResetSortingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ClearSortsAsync();
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
        }

        private async void ReferenceTableView_LoadMoreRequested(object sender, CbsTableLoadMoreRequestedEventArgs e)
        {
            await _viewModel.LoadMoreAsync();
        }

        private async void ReferenceTableView_FilterRequested(object sender, CbsTableFilterRequestedEventArgs e)
        {
            _viewModel.AppendUiTrace(
                $"FILTER UI REQUEST field={e.FieldKey} mode={e.MatchMode} value={(string.IsNullOrWhiteSpace(e.Value) ? "<empty>" : e.Value)}");
            _filterDebounceCts?.Cancel();
            var cancellationTokenSource = new CancellationTokenSource();
            _filterDebounceCts = cancellationTokenSource;

            try
            {
                await Task.Delay(250, cancellationTokenSource.Token);
                _viewModel.AppendUiTrace(
                    $"FILTER UI DISPATCH field={e.FieldKey} mode={e.MatchMode} value={(string.IsNullOrWhiteSpace(e.Value) ? "<empty>" : e.Value)}");
                await _viewModel.ApplyFilterAsync(
                    e.FieldKey,
                    e.MatchMode,
                    e.Value,
                    cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _viewModel.AppendUiTrace(
                    $"FILTER UI CANCELED field={e.FieldKey} mode={e.MatchMode}");
            }
        }

        private async void ReferenceTableView_ColumnWidthChanged(object sender, CbsTableColumnWidthChangedEventArgs e)
        {
            await _viewModel.SaveColumnWidthAsync(e.FieldKey, e.Width);
        }

        private void ReferenceTableView_TraceGenerated(object sender, CbsTableTraceEventArgs e)
        {
            if (e is null)
            {
                return;
            }

            _viewModel.AppendUiTrace(e.Message);
        }

        private async void ReferenceTableView_ViewportChanged(object? sender, CbsTableViewportChangedEventArgs e)
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

        private void EnsureViewportSubscription()
        {
            if (_isViewportSubscribed)
            {
                return;
            }

            ReferenceTableView.ViewportChanged += ReferenceTableView_ViewportChanged;
            _isViewportSubscribed = true;
        }

        private void RemoveViewportSubscription()
        {
            if (!_isViewportSubscribed)
            {
                return;
            }

            ReferenceTableView.ViewportChanged -= ReferenceTableView_ViewportChanged;
            _isViewportSubscribed = false;
        }

        private void UpdateSelectionActionButtons()
        {
            var hasSelectedRow = _viewModel.HasSelectedRow && _viewModel.HasActiveReference;

            if (EditSelectedRowButton is not null)
            {
                EditSelectedRowButton.IsEnabled = hasSelectedRow;
                EditSelectedRowButton.Foreground = hasSelectedRow
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.RoyalBlue)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }

            if (DeleteSelectedRowButton is not null)
            {
                DeleteSelectedRowButton.IsEnabled = hasSelectedRow;
                DeleteSelectedRowButton.Foreground = hasSelectedRow
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Firebrick)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }
        }
    }
}
