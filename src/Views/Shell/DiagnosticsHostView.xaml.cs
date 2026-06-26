using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using CbsContractsDesktopClient.Services.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class DiagnosticsHostView : UserControl
    {
        private const string ContractsFolderPath = @"L:\econ";
        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);

        private readonly ObservableCollection<DiagnosticsCheckRow> _rows = [];
        private readonly SystemDiagnosticsRunner _diagnosticsRunner;
        private bool _isRefreshing;

        public DiagnosticsHostView()
        {
            InitializeComponent();
            _diagnosticsRunner = new SystemDiagnosticsRunner(
                CreateHttpClient,
                App.PrimaryApiUri,
                App.DataQueryApiUri,
                App.FnsApiUri,
                ContractsFolderPath);
            ChecksItemsControl.ItemsSource = _rows;
            Loaded += DiagnosticsHostView_Loaded;
        }

        private async void DiagnosticsHostView_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            if (_isRefreshing)
            {
                return;
            }

            _isRefreshing = true;
            RefreshButton.IsEnabled = false;
            LoadingRing.IsActive = true;

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var results = await _diagnosticsRunner.RunAsync();

                _rows.Clear();
                foreach (var result in results)
                {
                    _rows.Add(ToCheckRow(result));
                }

                LastCheckedTextBlock.Text = $"Обновлено {DateTime.Now:HH:mm:ss}, {stopwatch.ElapsedMilliseconds} мс";
            }
            finally
            {
                LoadingRing.IsActive = false;
                RefreshButton.IsEnabled = true;
                _isRefreshing = false;
            }
        }

        private static HttpClient CreateHttpClient()
        {
            return new HttpClient
            {
                Timeout = CheckTimeout
            };
        }

        private static DiagnosticsCheckRow ToCheckRow(SystemDiagnosticsResult result)
        {
            return new DiagnosticsCheckRow(
                result.Title,
                result.Target,
                result.StatusText,
                result.Detail,
                GetStatusBrush(result.IsHealthy));
        }

        private static Brush GetStatusBrush(bool isHealthy)
        {
            return (Brush)Application.Current.Resources[
                isHealthy ? "ShellTableRowSelectedBorderBrush" : "StageDeadlineTextBrush"];
        }

        private sealed record DiagnosticsCheckRow(
            string Title,
            string Target,
            string StatusText,
            string Detail,
            Brush StatusBrush);
    }
}
