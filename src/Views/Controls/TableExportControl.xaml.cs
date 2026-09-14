using System.ComponentModel;
using System.IO;
using CbsContractsDesktopClient.Models.Export;
using CbsContractsDesktopClient.Services.Export;
using CbsContractsDesktopClient.Stores.Table;
using CbsContractsDesktopClient.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace CbsContractsDesktopClient.Views.Controls;

public sealed partial class TableExportControl : UserControl
{
    private readonly TablePageStore _store = App.Services.GetRequiredService<TablePageStore>();
    private readonly TableExcelExportService _exporter = App.Services.GetRequiredService<TableExcelExportService>();
    private CancellationTokenSource? _exportCts;
    public TableHostView Table { get; set; } = null!;

    public TableExportControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _store.PropertyChanged += OnStoreChanged;
        RefreshAvailability();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _store.PropertyChanged -= OnStoreChanged;
        _exportCts?.Cancel();
    }

    private void OnStoreChanged(object? sender, PropertyChangedEventArgs e) => RefreshAvailability();

    private void RefreshAvailability()
    {
        Visibility = _store.CanExport ? Visibility.Visible : Visibility.Collapsed;
        ExportButton.IsEnabled = _store.CanExport && _store.HasActiveReference && !_store.IsLoading && _exportCts is null;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _exportCts?.Cancel();

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        using var cancellation = new CancellationTokenSource();
        _exportCts = cancellation;
        RefreshAvailability();
        try
        {
            var request = _store.CreateExportRequest();
            var presentation = TablePresentationResources.Capture(Table.ShowStageCostFraction, Table.Density);
            var widths = request.Columns.Select(CbsTableView.GetColumnPixelWidth).ToArray();
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = string.Concat(request.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c))
                    + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm")
            };
            picker.FileTypeChoices.Add("Excel", [".xlsx"]);
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow));
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            cancellation.Token.ThrowIfCancellationRequested();
            ProgressText.Text = "Подготовка…";
            ProgressText.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Visible;
            var progress = new Progress<TableExportProgress>(value =>
            {
                if (!cancellation.IsCancellationRequested && ReferenceEquals(_exportCts, cancellation))
                {
                    ProgressText.Text = $"{value.Written:N0} / {value.Total:N0}";
                    ProgressText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                }
            });
            await _exporter.ExportAsync(request, presentation, widths, file.Path, progress, cancellation.Token);
            ProgressText.Text = "Сохранено";
            ToolTipService.SetToolTip(ProgressText, file.Path);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            ProgressText.Text = "Отменено";
        }
        catch (Exception ex)
        {
            ProgressText.Text = "Ошибка экспорта";
            if (IsLoaded)
                await new ContentDialog
                {
                    XamlRoot = XamlRoot, Title = "Экспорт в Excel", Content = ex.Message + Environment.NewLine + ex.InnerException?.Message,
                    CloseButtonText = "Закрыть"
                }.ShowAsync();
        }
        finally
        {
            _exportCts = null;
            ProgressText.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
            CancelButton.Visibility = Visibility.Collapsed;
            RefreshAvailability();
        }
    }
}
