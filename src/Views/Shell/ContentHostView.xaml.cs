using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.References;
using CbsContractsDesktopClient.ViewModels.Shell;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class ContentHostView : UserControl
    {
        private readonly ReferencesContentViewModel _viewModel;
        private readonly IReferenceCrudService _referenceCrudService;
        private readonly IDataQueryService _dataQueryService;
        private readonly IUserService _userService;
        private CancellationTokenSource? _filterDebounceCts;
        private CancellationTokenSource? _viewportCts;
        private bool _isViewportSubscribed;

        public ContentHostView()
        {
            _viewModel = App.Services.GetRequiredService<ReferencesContentViewModel>();
            _referenceCrudService = App.Services.GetRequiredService<IReferenceCrudService>();
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _userService = App.Services.GetRequiredService<IUserService>();
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

        private async void ReferenceTableView_RowDoubleTapped(object sender, CbsTableRowDoubleTappedEventArgs e)
        {
            _viewModel.SelectedRow = e.Row;

            if (IsInternEditBlocked())
            {
                return;
            }

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
                _viewModel.AppendUiTrace(
                    $"FILTER UI DISPATCH field={e.FieldKey} mode={e.MatchMode} value={DescribeFilterValue(e.Value)}");
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

        private async Task ShowReferenceEditDialogAsync(bool isCreateMode)
        {
            if (_viewModel.CurrentReference is null)
            {
                return;
            }

            if (!isCreateMode && _viewModel.SelectedRow is null)
            {
                return;
            }

            if (_viewModel.CurrentReference.EditorKind == ReferenceEditorKind.Profile)
            {
                await ShowProfileEditDialogAsync(isCreateMode);
                return;
            }

            var dialogViewModel = isCreateMode
                ? ReferenceEditViewModel.CreateForCreate(_viewModel.CurrentReference)
                : ReferenceEditViewModel.CreateForEdit(_viewModel.CurrentReference, _viewModel.SelectedRow!);

            var dialog = new ReferenceEditDialog(dialogViewModel)
            {
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var values = isCreateMode
                ? ReferenceEditPayloadBuilder.BuildForCreate(dialogViewModel)
                : ReferenceEditPayloadBuilder.BuildForUpdate(dialogViewModel);

            var action = isCreateMode ? "CREATE" : "EDIT";
            var keys = values.Count == 0
                ? "<empty>"
                : string.Join(", ", values.Keys);
            _viewModel.AppendUiTrace($"REFERENCE {action} DIALOG CONFIRMED keys={keys}");

            try
            {
                ReferenceDataRow savedRow;
                if (isCreateMode)
                {
                    savedRow = await _referenceCrudService.CreateAsync(_viewModel.CurrentReference, values);
                }
                else
                {
                    savedRow = await _referenceCrudService.UpdateAsync(_viewModel.CurrentReference, values);
                }

                await _viewModel.ReloadCurrentReferenceAsync();
                ShowSuccessNotification(
                    isCreateMode ? "Запись создана" : "Изменения сохранены",
                    BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, TryGetSelectedRowId(savedRow)));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(
                    isCreateMode ? "Не удалось создать запись." : "Не удалось сохранить изменения.",
                    ex.Message);
            }
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
                await ShowErrorDialogAsync("Не удалось удалить запись.", "У выбранной записи отсутствует корректный ID.");
                return;
            }

            var confirmDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Удаление записи",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                Content = "Удалить выбранную запись?"
            };

            if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                await _referenceCrudService.DeleteAsync(_viewModel.CurrentReference, id.Value);
                await _viewModel.ReloadCurrentReferenceAsync();
                ShowSuccessNotification(
                    "Запись удалена",
                    BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, id.Value));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось удалить запись.", ex.Message);
            }
        }

        private async Task ShowErrorDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = title,
                CloseButtonText = "Закрыть",
                DefaultButton = ContentDialogButton.Close,
                Content = message
            };

            await dialog.ShowAsync();
        }

        private ProfileEditDialogState CreateProfileEditDialogState(bool isCreateMode)
        {
            var departmentOptions = _viewModel.CurrentFilterOptionsSources.TryGetValue("Department", out var options)
                ? options
                : [];

            return ProfileEditStateFactory.Create(
                _viewModel.CurrentReference!,
                isCreateMode,
                isCreateMode ? null : _viewModel.SelectedRow,
                departmentOptions);
        }

        private async Task ShowProfileEditDialogAsync(bool isCreateMode)
        {
            var state = CreateProfileEditDialogState(isCreateMode);
            var viewModel = new ProfileEditViewModel(state, LoadPositionOptionsAsync);
            var dialog = new ProfileEditDialog(viewModel)
            {
                XamlRoot = XamlRoot
            };

            ReferenceDataRow? savedRow = null;

            dialog.PrimaryButtonClick += async (_, args) =>
            {
                var deferral = args.GetDeferral();
                try
                {
                    viewModel.ClearErrorInfo();

                    var payload = isCreateMode
                        ? ProfileEditPayloadBuilder.BuildForCreate(viewModel)
                        : ProfileEditPayloadBuilder.BuildForUpdate(viewModel);

                    if (!isCreateMode && payload.Count <= 1)
                    {
                        viewModel.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    savedRow = isCreateMode
                        ? await _referenceCrudService.CreateAsync(_viewModel.CurrentReference!, payload)
                        : await _referenceCrudService.UpdateAsync(_viewModel.CurrentReference!, payload);
                }
                catch (Exception ex)
                {
                    viewModel.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
                finally
                {
                    deferral.Complete();
                }
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary || savedRow is null || _viewModel.CurrentReference is null)
            {
                return;
            }

            await _viewModel.ReloadCurrentReferenceAsync();
            ShowSuccessNotification(
                isCreateMode ? "Запись создана" : "Изменения сохранены",
                BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, TryGetSelectedRowId(savedRow)));
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadPositionOptionsAsync(
            string searchText,
            CancellationToken cancellationToken)
        {
            var normalizedSearchText = searchText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedSearchText))
            {
                return [];
            }

            var rows = await _dataQueryService.GetDataAsync<ReferenceDataRow>(
                new DataQueryRequest
                {
                    Model = "Position",
                    Preset = "item",
                    Filters = new Dictionary<string, object?>
                    {
                        ["name__cnt"] = normalizedSearchText
                    },
                    Sorts = ["name asc"],
                    Limit = 25
                },
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(static row => new CbsTableFilterOptionDefinition
                {
                    Value = row.GetValue("id"),
                    Label = row.GetValue("name")?.ToString() ?? string.Empty
                })
                .Where(static option => option.Value is not null && !string.IsNullOrWhiteSpace(option.Label))
                .DistinctBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static string BuildReferenceNotificationMessage(string referenceTitle, long? id)
        {
            return id is long value
                ? $"{referenceTitle}, id:{value}"
                : referenceTitle;
        }

        private static void ShowSuccessNotification(string title, string message)
        {
            try
            {
                var notification = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(message)
                    .BuildNotification();

                AppNotificationManager.Default.Show(notification);
            }
            catch (COMException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static long? TryGetSelectedRowId(ReferenceDataRow row)
        {
            var rawId = row.GetValue("id");
            return rawId switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string stringValue when long.TryParse(stringValue, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private bool IsInternEditBlocked()
        {
            var roleText = _userService.CurrentUser?.Role;
            if (string.IsNullOrWhiteSpace(roleText))
            {
                return false;
            }

            return roleText
                .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(static role => string.Equals(role, "intern", System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
