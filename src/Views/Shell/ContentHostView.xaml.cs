using System.ComponentModel;
using System.Threading;
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
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
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
        }

        private async void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.Tag is not string fieldKey)
            {
                return;
            }

            _filterDebounceCts?.Cancel();
            var cancellationTokenSource = new CancellationTokenSource();
            _filterDebounceCts = cancellationTokenSource;

            try
            {
                await Task.Delay(350, cancellationTokenSource.Token);
                await _viewModel.ApplyTextFilterAsync(fieldKey, textBox.Text, cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async void ResetFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            _filterDebounceCts?.Cancel();
            await _viewModel.ResetFiltersAsync();
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
    }
}
