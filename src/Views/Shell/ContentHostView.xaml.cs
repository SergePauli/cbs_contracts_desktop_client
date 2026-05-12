using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.Functional;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class ContentHostView : UserControl
    {
        private readonly ReferencesContentViewModel _viewModel;
        private readonly IReferenceCrudService _referenceCrudService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private readonly IHolidayRecalculationService _holidayRecalculationService;
        private readonly IFnsContragentService _fnsContragentService;
        private readonly IDataQueryService _dataQueryService;
        private readonly IUserService _userService;
        private readonly ContractWorkflowStore _contractWorkflowStore;
        private CancellationTokenSource? _filterDebounceCts;
        private CancellationTokenSource? _viewportCts;
        private CancellationTokenSource? _contragentDetailCts;
        private CancellationTokenSource? _revisionDetailCts;
        private bool _isViewportSubscribed;
        private bool _isHolidayRecalcInProgress;
        private bool _isFnsCompareInProgress;
        private const int CommersDepartmentId = 2;
        private static readonly ReferenceDefinition StageEditDefinition = new()
        {
            Route = "/internal/Stage",
            Model = "Stage",
            Title = "Stage",
            Preset = "edit"
        };
        private static readonly ReferenceDefinition AddressEditDefinition = new()
        {
            Route = "/internal/Address",
            Model = "Address",
            Title = "Address",
            Preset = "edit"
        };
        private static readonly ReferenceDefinition RevisionEditDefinition = new()
        {
            Route = "/revisions",
            Model = "Revision",
            Title = "Дополнительное соглашение",
            Preset = "edit"
        };

        public ContentHostView()
        {
            _viewModel = App.Services.GetRequiredService<ReferencesContentViewModel>();
            _referenceCrudService = App.Services.GetRequiredService<IReferenceCrudService>();
            _referenceDefinitionService = App.Services.GetRequiredService<IReferenceDefinitionService>();
            _referenceLookupCacheService = App.Services.GetRequiredService<IReferenceLookupCacheService>();
            _holidayRecalculationService = App.Services.GetRequiredService<IHolidayRecalculationService>();
            _fnsContragentService = App.Services.GetRequiredService<IFnsContragentService>();
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _userService = App.Services.GetRequiredService<IUserService>();
            _contractWorkflowStore = App.Services.GetRequiredService<ContractWorkflowStore>();
            InitializeComponent();
            DataContext = _viewModel;
            UpdateSelectionActionButtons();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _contractWorkflowStore.PropertyChanged += OnContractWorkflowStorePropertyChanged;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateSelectionActionButtons();
            UpdateTableRowStyle();
            EnsureViewportSubscription();
            await _viewModel.EnsureLoadedAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _filterDebounceCts?.Cancel();
            _viewportCts?.Cancel();
            _contragentDetailCts?.Cancel();
            _revisionDetailCts?.Cancel();
            _contractWorkflowStore.PropertyChanged -= OnContractWorkflowStorePropertyChanged;
            RemoveViewportSubscription();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReferencesContentViewModel.SelectedRow)
                || e.PropertyName == nameof(ReferencesContentViewModel.HasSelectedRow)
                || e.PropertyName == nameof(ReferencesContentViewModel.HasActiveReference)
                || e.PropertyName == nameof(ReferencesContentViewModel.CurrentTablePage)
                || e.PropertyName == nameof(ReferencesContentViewModel.CanEditRows)
                || e.PropertyName == nameof(ReferencesContentViewModel.CanDeleteRows))
            {
                UpdateSelectionActionButtons();
            }

            if (e.PropertyName == nameof(ReferencesContentViewModel.CurrentTablePage)
                || e.PropertyName == nameof(ReferencesContentViewModel.CurrentRowStyleKey))
            {
                UpdateTableRowStyle();
            }

            if (e.PropertyName == nameof(ReferencesContentViewModel.SelectedRow)
                || e.PropertyName == nameof(ReferencesContentViewModel.ShowContragentDetailView))
            {
                _ = RefreshContragentDetailContractsAsync();
            }

            if (e.PropertyName == nameof(ReferencesContentViewModel.SelectedRow))
            {
                _ = RefreshRevisionDetailAsync();
            }
        }

        private void OnContractWorkflowStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ContractWorkflowStore.Contract))
            {
                UpdateSelectionActionButtons();
            }
        }

        private void UpdateTableRowStyle()
        {
            ReferenceTableView.RowStyleKey = _viewModel.CurrentRowStyleKey;
        }

        private async Task RefreshContragentDetailContractsAsync()
        {
            _contragentDetailCts?.Cancel();
            ContragentDetailView.ContractsRow = null;

            if (!_viewModel.ShowContragentDetailView || _viewModel.SelectedRow is null)
            {
                return;
            }

            var id = TryGetSelectedRowId(_viewModel.SelectedRow);
            if (id is null)
            {
                return;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _contragentDetailCts = cancellationTokenSource;

            try
            {
                var row = await LoadContragentEditRowAsync(id.Value, cancellationTokenSource.Token);
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                if (_viewModel.SelectedRow is null || TryGetSelectedRowId(_viewModel.SelectedRow) != id)
                {
                    return;
                }

                ContragentDetailView.ContractsRow = row;
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    ContragentDetailView.ContractsRow = null;
                }
            }
        }

        private async Task RefreshRevisionDetailAsync()
        {
            _revisionDetailCts?.Cancel();
            RevisionsDetailView.ContractRow = null;
            RevisionsDetailView.ContragentRow = null;
            _contractWorkflowStore.ClearRevisionSelection();

            if (!_viewModel.ShowContractDetailView || _viewModel.SelectedRow is null)
            {
                return;
            }

            var contractId = TryGetLongValue(_viewModel.SelectedRow, "contract.id");
            var listContragentId = TryGetLongValue(_viewModel.SelectedRow, "contract.contragent.id");
            if (contractId is null && listContragentId is null)
            {
                return;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _revisionDetailCts = cancellationTokenSource;

            try
            {
                var contractTask = contractId is long selectedContractId
                    ? LoadRevisionDetailRowSafelyAsync(
                        "Contract/edit",
                        () => LoadRevisionContractCardAsync(selectedContractId, cancellationTokenSource.Token),
                        cancellationTokenSource.Token)
                    : Task.FromResult<ReferenceDataRow?>(null);
                var contragentTask = listContragentId is long selectedContragentId
                    ? LoadRevisionDetailRowSafelyAsync(
                        "Contragent/card",
                        () => LoadRevisionContragentCardAsync(selectedContragentId, cancellationTokenSource.Token),
                        cancellationTokenSource.Token)
                    : Task.FromResult<ReferenceDataRow?>(null);

                var contract = await contractTask;
                var contragent = await contragentTask;
                if (cancellationTokenSource.IsCancellationRequested || !_viewModel.ShowContractDetailView)
                {
                    return;
                }

                if (_viewModel.SelectedRow is null
                    || (contractId is not null && TryGetLongValue(_viewModel.SelectedRow, "contract.id") != contractId))
                {
                    return;
                }

                var contractContragentId = contract is null
                    ? null
                    : TryGetLongValue(contract, "contragent.id");
                if (contragent is null && contractContragentId is long loadedContragentId)
                {
                    contragent = await LoadRevisionDetailRowSafelyAsync(
                        "Contragent/card from Contract/card",
                        () => LoadRevisionContragentCardAsync(loadedContragentId, cancellationTokenSource.Token),
                        cancellationTokenSource.Token);
                }

                _contractWorkflowStore.SetRevisionSelection(_viewModel.SelectedRow, contract, contragent);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    _contractWorkflowStore.ClearRevisionSelection();
                }
            }
        }

        private static async Task<ReferenceDataRow?> LoadRevisionDetailRowSafelyAsync(
            string title,
            Func<Task<ReferenceDataRow?>> loadAsync,
            CancellationToken cancellationToken)
        {
            try
            {
                return await loadAsync();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return null;
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
            if (_viewModel.IsContragentReference && sender is FrameworkElement anchor)
            {
                ShowContragentCreateMenu(anchor);
                return;
            }

            await ShowReferenceEditDialogAsync(isCreateMode: true);
        }

        private async void EditSelectedRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.ShowRevisionsDetailView)
            {
                await ShowRevisionEditDialogAsync();
                return;
            }

            await ShowReferenceEditDialogAsync(isCreateMode: false);
        }

        private async void DeleteSelectedRowButton_Click(object sender, RoutedEventArgs e)
        {
            await DeleteSelectedRowAsync();
        }

        private async void HolidayRecalcButton_Click(object sender, RoutedEventArgs e)
        {
            await RecalculateHolidayStagesAsync();
        }

        private async void FnsCompareButton_Click(object sender, RoutedEventArgs e)
        {
            await CompareSelectedContragentWithFnsAsync();
        }

        private void CopyContragentDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsContractDetailTableActive())
            {
                var contractText = RevisionsDetailView.BuildClipboardText();
                if (string.IsNullOrWhiteSpace(contractText))
                {
                    return;
                }

                var contractDataPackage = new DataPackage();
                contractDataPackage.SetText(contractText);
                Clipboard.SetContent(contractDataPackage);
                ShowSuccessNotification("Данные скопированы", "Карточка контракта скопирована в буфер обмена.");
                return;
            }

            var text = ContragentDetailView.BuildClipboardText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            ShowSuccessNotification("Данные скопированы", "Карточка контрагента скопирована в буфер обмена.");
        }

        private void ShowContragentCreateMenu(FrameworkElement anchor)
        {
            var menu = new MenuFlyout();

            if (_viewModel.HasSelectedRow)
            {
                var legalEntityChangeItem = new MenuFlyoutSubItem
                {
                    Text = "Смена юр.лица"
                };
                var importLegalEntityFnsItem = new MenuFlyoutItem
                {
                    Text = "Импорт из ФНС"
                };
                importLegalEntityFnsItem.Click += async (_, _) => await ChangeContragentLegalEntityFromFnsAsync();
                legalEntityChangeItem.Items.Add(importLegalEntityFnsItem);

                var manualLegalEntityItem = new MenuFlyoutItem
                {
                    Text = "Ручной ввод"
                };
                manualLegalEntityItem.Click += async (_, _) => await ChangeContragentLegalEntityManuallyAsync();
                legalEntityChangeItem.Items.Add(manualLegalEntityItem);

                menu.Items.Add(legalEntityChangeItem);

                menu.Items.Add(new MenuFlyoutSeparator());
            }

            var importFnsItem = new MenuFlyoutItem
            {
                Text = "Импорт из ФНС"
            };
            importFnsItem.Click += async (_, _) => await ImportContragentFromFnsAsync();
            menu.Items.Add(importFnsItem);

            var manualItem = new MenuFlyoutItem
            {
                Text = "Ручной ввод"
            };
            manualItem.Click += async (_, _) => await ShowContragentEditDialogAsync(isCreateMode: true);
            menu.Items.Add(manualItem);

            menu.ShowAt(anchor);
        }

        private async void ContragentDetailView_EmployeeEditRequested(object sender, EmployeeBoxEditRequestedEventArgs e)
        {
            if (IsInternEditBlocked() || e.Employee.Id is not long employeeId)
            {
                return;
            }

            if (!_referenceDefinitionService.TryGetByRoute("/employees", out var employeeDefinition))
            {
                await ShowErrorDialogAsync("Не удалось открыть сотрудника.", "Справочник сотрудников не подключен.");
                return;
            }

            await ShowEmployeeEditDialogAsync(isCreateMode: false, employeeId, employeeDefinition);
        }

        private async void ResetColumnWidthsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ResetColumnWidthsAsync();
        }

        private async void ConfigureColumnsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentTablePage is null)
            {
                return;
            }

            var dialog = new TableColumnLayoutDialog(_viewModel.CurrentTablePage.Columns)
            {
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            await _viewModel.SaveColumnLayoutAsync(dialog.BuildColumns());
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

        private void ReferenceTableView_RowSelectionChanged(object sender, CbsTableRowSelectionChangedEventArgs e)
        {
            if (!e.IsSelected)
            {
                _viewModel.SelectedRow = null;
            }
        }

        private async void ReferenceTableView_RowDoubleTapped(object sender, CbsTableRowDoubleTappedEventArgs e)
        {
            _viewModel.SelectedRow = e.Row;

            if (IsInternEditBlocked())
            {
                return;
            }

            if (_viewModel.ShowRevisionsDetailView)
            {
                await ShowRevisionEditDialogAsync();
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
            var canEditSelectedRow = hasSelectedRow && _viewModel.CanEditRows;
            var canDeleteSelectedRow = hasSelectedRow && _viewModel.CanDeleteRows;
            var isHolidayReference = string.Equals(_viewModel.CurrentReference?.Route, "/holidays", StringComparison.OrdinalIgnoreCase);
            var isContragentReference = _viewModel.IsContragentReference;
            var isContractDetailTable = IsContractDetailTableActive();
            var hasWorkflowContract = _contractWorkflowStore.Contract is { IsPlaceholder: false };
            var canRecalculateHoliday = hasSelectedRow && isHolidayReference && !_isHolidayRecalcInProgress;
            var canCompareFns = hasSelectedRow && isContragentReference && !_isFnsCompareInProgress;
            var canCopyContragentDetails = hasSelectedRow && isContragentReference;
            var canCopyRevisionContract = isContractDetailTable && hasWorkflowContract;
            var canCopyDetails = canCopyContragentDetails || canCopyRevisionContract;

            if (EditSelectedRowButton is not null)
            {
                EditSelectedRowButton.IsEnabled = canEditSelectedRow;
                EditSelectedRowButton.Foreground = canEditSelectedRow
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.RoyalBlue)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }

            if (DeleteSelectedRowButton is not null)
            {
                DeleteSelectedRowButton.IsEnabled = canDeleteSelectedRow;
                DeleteSelectedRowButton.Foreground = canDeleteSelectedRow
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Firebrick)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }

            var holidayRecalcButton = FindName("HolidayRecalcButton") as Button;
            if (holidayRecalcButton is not null)
            {
                holidayRecalcButton.Visibility = isHolidayReference ? Visibility.Visible : Visibility.Collapsed;
                holidayRecalcButton.IsEnabled = canRecalculateHoliday;
                holidayRecalcButton.Foreground = canRecalculateHoliday
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.SteelBlue)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }

            if (FnsCompareButton is not null)
            {
                FnsCompareButton.Visibility = isContragentReference ? Visibility.Visible : Visibility.Collapsed;
                FnsCompareButton.IsEnabled = canCompareFns;
                FnsCompareButton.Foreground = canCompareFns
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.SeaGreen)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }

            if (CopyContragentDetailsButton is not null)
            {
                CopyContragentDetailsButton.Visibility = isContragentReference || isContractDetailTable ? Visibility.Visible : Visibility.Collapsed;
                CopyContragentDetailsButton.IsEnabled = canCopyDetails;
                ToolTipService.SetToolTip(
                    CopyContragentDetailsButton,
                    isContractDetailTable
                        ? "Скопировать данные контракта"
                        : "Скопировать данные контрагента");
                CopyContragentDetailsButton.Foreground = canCopyDetails
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkSlateBlue)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }
        }

        private bool IsRevisionsTableActive()
        {
            return string.Equals(
                _viewModel.CurrentTablePage?.Route,
                "/revisions",
                StringComparison.OrdinalIgnoreCase);
        }

        private bool IsStagesTableActive()
        {
            return string.Equals(
                _viewModel.CurrentTablePage?.Route,
                "/stages",
                StringComparison.OrdinalIgnoreCase);
        }

        private bool IsContractDetailTableActive()
        {
            return string.Equals(
                    _viewModel.CurrentTablePage?.Route,
                    "/revisions",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    _viewModel.CurrentTablePage?.Route,
                    "/stages",
                    StringComparison.OrdinalIgnoreCase);
        }

        private async Task ShowReferenceEditDialogAsync(bool isCreateMode)
        {
            if (!isCreateMode && IsStagesTableActive())
            {
                await ShowStageEditDialogAsync();
                return;
            }

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

            if (_viewModel.CurrentReference.EditorKind == ReferenceEditorKind.Employee)
            {
                await ShowEmployeeEditDialogAsync(isCreateMode);
                return;
            }

            if (_viewModel.CurrentReference.EditorKind == ReferenceEditorKind.Contragent)
            {
                await ShowContragentEditDialogAsync(isCreateMode);
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

                _referenceLookupCacheService.Invalidate(_viewModel.CurrentReference.Model);
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

        private async Task ShowRevisionEditDialogAsync()
        {
            if (_viewModel.SelectedRow is null)
            {
                return;
            }

            RevisionEditDialog dialog;
            try
            {
                dialog = new RevisionEditDialog(_viewModel.SelectedRow)
                {
                    XamlRoot = XamlRoot
                };
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось открыть ревизию.", ex.Message);
                return;
            }

            ReferenceDataRow? savedRow = null;
            dialog.PrimaryButtonClick += async (_, args) =>
            {
                var deferral = args.GetDeferral();
                try
                {
                    savedRow = await _referenceCrudService.UpdateAsync(
                        RevisionEditDefinition,
                        dialog.BuildPayload());
                }
                catch (Exception ex)
                {
                    dialog.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
                finally
                {
                    deferral.Complete();
                }
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary || savedRow is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(RevisionEditDefinition.Model);
            await _viewModel.ReloadCurrentReferenceAsync();
            ShowSuccessNotification(
                "Ревизия сохранена",
                BuildReferenceNotificationMessage(RevisionEditDefinition.Title, TryGetSelectedRowId(savedRow)));
        }

        private async Task ShowStageEditDialogAsync()
        {
            if (_viewModel.SelectedRow is null)
            {
                return;
            }

            if (_userService.CurrentUser?.DepartmentId != CommersDepartmentId)
            {
                await ShowErrorDialogAsync(
                    "Редактирование этапа",
                    "Диалог редактирования этапа для вашего отдела пока не реализован.");
                return;
            }

            await ShowStageCommerEditDialogAsync();
        }

        private async Task ShowStageCommerEditDialogAsync()
        {
            var sourceRow = await LoadStageEditRowAsync();
            if (sourceRow is null)
            {
                await ShowErrorDialogAsync("Редактирование этапа", "Не удалось загрузить карточку выбранного этапа.");
                return;
            }

            var statusOptions = await _referenceLookupCacheService.GetOptionsAsync("Status");
            var taskKindItems = await _referenceLookupCacheService.GetItemsAsync("TaskKind");
            StageCommerEditDialog dialog;
            try
            {
                dialog = new StageCommerEditDialog(
                    sourceRow,
                    _viewModel.SelectedRow,
                    _contractWorkflowStore.Contract,
                    statusOptions,
                    taskKindItems,
                    _userService.CurrentUser?.ProfileId)
                {
                    XamlRoot = XamlRoot
                };
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось открыть этап.", ex.Message);
                return;
            }

            ReferenceDataRow? savedRow = null;
            dialog.PrimaryButtonClick += async (_, args) =>
            {
                var deferral = args.GetDeferral();
                try
                {
                    if (!dialog.Validate())
                    {
                        args.Cancel = true;
                        return;
                    }

                    savedRow = await _referenceCrudService.UpdateAsync(
                        StageEditDefinition,
                        dialog.BuildPayload());
                }
                catch (Exception ex)
                {
                    dialog.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
                finally
                {
                    deferral.Complete();
                }
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary || savedRow is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(StageEditDefinition.Model);
            await _viewModel.ReloadCurrentReferenceAsync();
            ShowSuccessNotification(
                "Этап сохранён",
                BuildReferenceNotificationMessage("Этап", TryGetSelectedRowId(savedRow)));
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
            DialogChrome.Apply(confirmDialog);

            if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                await _referenceCrudService.DeleteAsync(_viewModel.CurrentReference, id.Value);
                _referenceLookupCacheService.Invalidate(_viewModel.CurrentReference.Model);
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
            DialogChrome.Apply(dialog);

            await dialog.ShowAsync();
        }

        private async Task CompareSelectedContragentWithFnsAsync()
        {
            if (_viewModel.CurrentReference is null || _viewModel.SelectedRow is null || !_viewModel.IsContragentReference)
            {
                return;
            }

            _isFnsCompareInProgress = true;
            UpdateSelectionActionButtons();

            try
            {
                var sourceRow = await LoadContragentEditRowAsync();
                if (sourceRow is null)
                {
                    await ShowErrorDialogAsync("Не удалось выполнить сверку", "Не удалось загрузить свежую карточку контрагента.");
                    return;
                }

                var ownershipOptions = await LoadSimpleReferenceOptionsAsync("Ownership", "card");
                var regionOptions = await LoadSimpleReferenceOptionsAsync("Area", "item");
                var state = ContragentEditStateFactory.Create(
                    _viewModel.CurrentReference,
                    isCreateMode: false,
                    sourceRow,
                    ownershipOptions,
                    regionOptions);

                if (string.IsNullOrWhiteSpace(state.Inn) || !IsValidInn(state.Inn))
                {
                    await ShowErrorDialogAsync("Сверка с ФНС", "Укажите корректный ИНН для сверки.");
                    return;
                }

                var fnsResults = await _fnsContragentService.SearchByReqAsync(state.Inn.Trim(), state.Kpp);
                if (fnsResults.Count == 0)
                {
                    await ShowErrorDialogAsync("Сверка с ФНС", "Данные в ФНС не найдены.");
                    return;
                }

                var remote = SelectFnsResult(fnsResults, state.Kpp);
                var editViewModel = new ContragentEditViewModel(state, LoadAddressOptionsAsync);
                var compareRows = BuildFnsCompareRows(editViewModel, remote);
                if (compareRows.Count == 0 || compareRows.All(static row => string.IsNullOrWhiteSpace(row.RemoteValue)))
                {
                    await ShowErrorDialogAsync("Сверка с ФНС", "ФНС не вернула данных, которые можно применить к карточке.");
                    return;
                }

                var compareContent = BuildFnsCompareDialogContent(compareRows);
                await Task.Yield();

                var dialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = BuildFnsCompareDialogHeader(),
                    PrimaryButtonText = "Применить",
                    CloseButtonText = "Отмена",
                    DefaultButton = ContentDialogButton.Primary,
                    Content = compareContent
                };
                dialog.Resources["ContentDialogMinWidth"] = 1180d;
                dialog.Resources["ContentDialogMaxWidth"] = 1280d;
                DialogChrome.Apply(dialog, "Сверка с ФНС");

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }

                foreach (var row in compareRows.Where(static row => row.IsChecked))
                {
                    row.Apply(editViewModel);
                }

                await EnsureContragentAddressAsync(editViewModel);
                if (!editViewModel.CanSubmit)
                {
                    await ShowErrorDialogAsync("Сверка с ФНС", "Отмеченные строки не меняют карточку контрагента.");
                    return;
                }

                var payload = ContragentEditPayloadBuilder.BuildForUpdate(editViewModel);
                if (payload.Count <= 1)
                {
                    await ShowErrorDialogAsync("Сверка с ФНС", "Отмеченные строки не меняют карточку контрагента.");
                    return;
                }

                var savedRow = await _referenceCrudService.UpdateAsync(_viewModel.CurrentReference, payload);
                _referenceLookupCacheService.Invalidate(_viewModel.CurrentReference.Model);
                await _viewModel.ReloadCurrentReferenceAsync();
                ShowSuccessNotification(
                    "Данные обновлены",
                    BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, TryGetSelectedRowId(savedRow)));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось выполнить сверку с ФНС", ex.Message);
            }
            finally
            {
                _isFnsCompareInProgress = false;
                UpdateSelectionActionButtons();
            }
        }

        private static FnsContragentLookupResult SelectFnsResult(
            IReadOnlyList<FnsContragentLookupResult> results,
            string? kpp)
        {
            if (!string.IsNullOrWhiteSpace(kpp))
            {
                var byKpp = results.FirstOrDefault(result =>
                    string.Equals(result.Organization.Kpp, kpp.Trim(), StringComparison.OrdinalIgnoreCase));

                if (byKpp is not null)
                {
                    return byKpp;
                }
            }

            return results[0];
        }

        private async Task ImportContragentFromFnsAsync()
        {
            if (_viewModel.CurrentReference is null || !_viewModel.IsContragentReference)
            {
                return;
            }

            var criteria = await ShowFnsImportCriteriaDialogAsync();
            if (criteria is null)
            {
                return;
            }

            if (!IsValidInn(criteria.Inn))
            {
                await ShowErrorDialogAsync("Импорт из ФНС", "Укажите корректный ИНН.");
                return;
            }

            try
            {
                var results = await _fnsContragentService.SearchByReqAsync(criteria.Inn);
                if (results.Count == 0)
                {
                    await ShowErrorDialogAsync("Импорт из ФНС", "Данные в ФНС не найдены.");
                    return;
                }

                var filteredResults = FilterFnsImportResults(results, criteria);
                if (filteredResults.Count == 0)
                {
                    await ShowErrorDialogAsync("Импорт из ФНС", "По указанным КПП или наименованию данные не найдены.");
                    return;
                }

                var selectedResult = filteredResults.Count == 1
                    ? filteredResults[0]
                    : await ShowFnsImportResultSelectionDialogAsync(filteredResults);
                if (selectedResult is null)
                {
                    return;
                }

                var state = await CreateContragentImportStateAsync(selectedResult);
                await ShowContragentEditDialogAsync(isCreateMode: true, state);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось импортировать данные из ФНС", ex.Message);
            }
        }

        private async Task ChangeContragentLegalEntityManuallyAsync()
        {
            if (_viewModel.CurrentReference is null || _viewModel.SelectedRow is null || !_viewModel.IsContragentReference)
            {
                return;
            }

            var state = await CreateLegalEntityChangeStateAsync();
            if (state is null)
            {
                return;
            }

            await ShowContragentEditDialogAsync(
                isCreateMode: false,
                state,
                isLegalEntityChangeMode: true);
        }

        private async Task ChangeContragentLegalEntityFromFnsAsync()
        {
            if (_viewModel.CurrentReference is null || _viewModel.SelectedRow is null || !_viewModel.IsContragentReference)
            {
                return;
            }

            try
            {
                var fnsResult = await SelectFnsImportResultAsync();
                if (fnsResult is null)
                {
                    return;
                }

                var state = await CreateLegalEntityChangeStateAsync(fnsResult);
                if (state is null)
                {
                    return;
                }

                await ShowContragentEditDialogAsync(
                    isCreateMode: false,
                    state,
                    isLegalEntityChangeMode: true);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось сменить юр.лицо через ФНС", ex.Message);
            }
        }

        private async Task<FnsContragentLookupResult?> SelectFnsImportResultAsync()
        {
            var criteria = await ShowFnsImportCriteriaDialogAsync();
            if (criteria is null)
            {
                return null;
            }

            if (!IsValidInn(criteria.Inn))
            {
                await ShowErrorDialogAsync("Импорт из ФНС", "Укажите корректный ИНН.");
                return null;
            }

            var results = await _fnsContragentService.SearchByReqAsync(criteria.Inn);
            if (results.Count == 0)
            {
                await ShowErrorDialogAsync("Импорт из ФНС", "Данные в ФНС не найдены.");
                return null;
            }

            var filteredResults = FilterFnsImportResults(results, criteria);
            if (filteredResults.Count == 0)
            {
                await ShowErrorDialogAsync("Импорт из ФНС", "По указанным КПП или наименованию данные не найдены.");
                return null;
            }

            return filteredResults.Count == 1
                ? filteredResults[0]
                : await ShowFnsImportResultSelectionDialogAsync(filteredResults);
        }

        private async Task<FnsImportCriteria?> ShowFnsImportCriteriaDialogAsync()
        {
            var innBox = new TextBox
            {
                Header = "ИНН",
                PlaceholderText = "Введите ИНН",
                MinWidth = 360
            };
            var kppBox = new TextBox
            {
                Header = "КПП",
                PlaceholderText = "Необязательно",
                MinWidth = 360
            };
            var nameBox = new TextBox
            {
                Header = "Наименование",
                PlaceholderText = "Короткое или полное имя",
                MinWidth = 360
            };
            var content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    innBox,
                    kppBox,
                    nameBox
                }
            };

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Импорт из ФНС",
                PrimaryButtonText = "Найти",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Primary,
                Content = content
            };
            DialogChrome.Apply(dialog);

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return null;
            }

            var inn = NormalizeSingleLine(innBox.Text);
            if (string.IsNullOrWhiteSpace(inn))
            {
                return null;
            }

            return new FnsImportCriteria(
                inn,
                NormalizeSingleLine(kppBox.Text),
                NormalizeSingleLine(nameBox.Text));
        }

        private static IReadOnlyList<FnsContragentLookupResult> FilterFnsImportResults(
            IReadOnlyList<FnsContragentLookupResult> results,
            FnsImportCriteria criteria)
        {
            if (string.IsNullOrWhiteSpace(criteria.Kpp) && string.IsNullOrWhiteSpace(criteria.Name))
            {
                return results;
            }

            return results
                .Where(result =>
                    MatchesFnsImportKpp(result, criteria.Kpp)
                    || MatchesFnsImportName(result, criteria.Name))
                .ToList();
        }

        private static bool MatchesFnsImportKpp(FnsContragentLookupResult result, string kpp)
        {
            return !string.IsNullOrWhiteSpace(kpp)
                && string.Equals(
                    NormalizeSingleLine(result.Organization.Kpp),
                    kpp,
                    StringComparison.CurrentCultureIgnoreCase);
        }

        private static bool MatchesFnsImportName(FnsContragentLookupResult result, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var fullName = NormalizeSingleLine(result.Organization.FullName);
            var shortName = NormalizeSingleLine(result.Organization.Name);
            return fullName.Contains(name, StringComparison.CurrentCultureIgnoreCase)
                || shortName.Contains(name, StringComparison.CurrentCultureIgnoreCase);
        }

        private async Task<FnsContragentLookupResult?> ShowFnsImportResultSelectionDialogAsync(
            IReadOnlyList<FnsContragentLookupResult> results)
        {
            var items = results
                .Select(static result => new FnsImportSelectionItem(BuildFnsImportSelectionLabel(result), result))
                .ToList();
            var comboBox = new ComboBox
            {
                Header = "КПП / подразделение",
                ItemsSource = items,
                DisplayMemberPath = nameof(FnsImportSelectionItem.Label),
                SelectedIndex = 0,
                MinWidth = 680
            };

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Выберите регистрацию",
                PrimaryButtonText = "Продолжить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Primary,
                Content = comboBox
            };
            dialog.Resources["ContentDialogMinWidth"] = 760d;
            DialogChrome.Apply(dialog);

            return await dialog.ShowAsync() == ContentDialogResult.Primary
                ? (comboBox.SelectedItem as FnsImportSelectionItem)?.Result
                : null;
        }

        private async Task<ContragentEditDialogState> CreateContragentImportStateAsync(
            FnsContragentLookupResult result)
        {
            var ownershipOptions = await LoadSimpleReferenceOptionsAsync("Ownership", "card");
            var regionOptions = await LoadSimpleReferenceOptionsAsync("Area", "item");

            return new ContragentEditDialogState
            {
                Definition = _viewModel.CurrentReference!,
                IsCreateMode = true,
                ObjUuid = string.IsNullOrWhiteSpace(result.ObjUuid) ? Guid.NewGuid().ToString() : result.ObjUuid,
                RequisitesListKey = result.RequisitesListKey,
                Inn = NormalizeSingleLine(result.Organization.Inn),
                Kpp = NormalizeSingleLine(result.Organization.Kpp),
                OwnershipId = result.Organization.OwnershipId,
                Name = NormalizeSingleLine(result.Organization.Name),
                FullName = NormalizeSingleLine(result.Organization.FullName),
                RegionId = result.Region?.Id ?? result.RealAddress.AreaId,
                RegionName = result.Region?.Name ?? string.Empty,
                AddressReal = NormalizeSingleLine(result.RealAddress.Value),
                Description = NormalizeSingleLine(result.Description),
                Ogrn = NormalizeSingleLine(result.Organization.Ogrn),
                Okfc = NormalizeSingleLine(result.Organization.Okfc),
                Okopf = NormalizeSingleLine(result.Organization.Okopf),
                Okpo = NormalizeSingleLine(result.Organization.Okpo),
                Okogu = NormalizeSingleLine(result.Organization.Okogu),
                Oktmo = NormalizeSingleLine(result.Organization.Oktmo),
                Contacts = result.Contacts
                    .Where(static contact => !string.IsNullOrWhiteSpace(contact.Value))
                    .Select(static contact => new EmployeeContactEditItem
                    {
                        ListKey = contact.ListKey,
                        Value = NormalizeSingleLine(contact.Value),
                        Type = NormalizeSingleLine(contact.Type)
                    })
                    .ToList(),
                ContactsText = string.Join(
                    Environment.NewLine,
                    result.Contacts
                        .Select(static contact => NormalizeSingleLine(contact.Value))
                        .Where(static value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.CurrentCultureIgnoreCase)),
                OwnershipOptions = ownershipOptions,
                RegionOptions = regionOptions
            };
        }

        private async Task<ContragentEditDialogState?> CreateLegalEntityChangeStateAsync(
            FnsContragentLookupResult? result = null)
        {
            if (_viewModel.CurrentReference is null)
            {
                return null;
            }

            var sourceRow = await LoadContragentEditRowAsync();
            if (sourceRow is null)
            {
                await ShowErrorDialogAsync("Смена юр.лица", "Не удалось загрузить свежую карточку контрагента.");
                return null;
            }

            var ownershipOptions = await LoadSimpleReferenceOptionsAsync("Ownership", "card");
            var regionOptions = await LoadSimpleReferenceOptionsAsync("Area", "item");
            var currentState = ContragentEditStateFactory.Create(
                _viewModel.CurrentReference,
                isCreateMode: false,
                sourceRow,
                ownershipOptions,
                regionOptions);
            var newRegistration = CreateNewLegalEntityRegistration(result);
            var organizationHistory = currentState.OrganizationHistory
                .Select(static registration => CopyRegistrationForLegalEntityChange(registration))
                .Prepend(newRegistration)
                .ToList();

            return new ContragentEditDialogState
            {
                Definition = currentState.Definition,
                IsCreateMode = false,
                Id = currentState.Id,
                ObjUuid = currentState.ObjUuid,
                RequisitesId = currentState.RequisitesId,
                RequisitesListKey = currentState.RequisitesListKey,
                OrganizationId = currentState.OrganizationId,
                Inn = newRegistration.Inn,
                Kpp = newRegistration.Kpp,
                Division = newRegistration.Division,
                OwnershipId = newRegistration.OwnershipId,
                OwnershipName = newRegistration.OwnershipName,
                Name = newRegistration.Name,
                RegionId = currentState.RegionId,
                RegionName = currentState.RegionName,
                RealAddressId = currentState.RealAddressId,
                RealAddressListKey = currentState.RealAddressListKey,
                AddressRealAddressId = currentState.AddressRealAddressId,
                AddressReal = currentState.AddressReal,
                FullName = newRegistration.FullName,
                Description = currentState.Description,
                Ogrn = newRegistration.Ogrn,
                Okfc = newRegistration.Okfc,
                Okopf = newRegistration.Okopf,
                Okpo = newRegistration.Okpo,
                Okogu = newRegistration.Okogu,
                Okved = newRegistration.Okved,
                Oktmo = newRegistration.Oktmo,
                BankName = currentState.BankName,
                BankBik = currentState.BankBik,
                BankAccount = currentState.BankAccount,
                BankCorAccount = currentState.BankCorAccount,
                Contacts = currentState.Contacts,
                ContactsText = currentState.ContactsText,
                OrganizationHistory = organizationHistory,
                OwnershipOptions = ownershipOptions,
                RegionOptions = regionOptions,
                InitialAddressOption = currentState.InitialAddressOption
            };
        }

        private static ContragentOrganizationHistoryItem CreateNewLegalEntityRegistration(
            FnsContragentLookupResult? result)
        {
            return new ContragentOrganizationHistoryItem
            {
                ListKey = Guid.NewGuid().ToString(),
                IsActive = true,
                OriginalIsActive = false,
                Name = NormalizeSingleLine(result?.Organization.Name),
                FullName = NormalizeSingleLine(result?.Organization.FullName),
                Inn = NormalizeSingleLine(result?.Organization.Inn),
                Kpp = NormalizeSingleLine(result?.Organization.Kpp),
                OwnershipId = result?.Organization.OwnershipId,
                Ogrn = NormalizeSingleLine(result?.Organization.Ogrn),
                Okfc = NormalizeSingleLine(result?.Organization.Okfc),
                Okopf = NormalizeSingleLine(result?.Organization.Okopf),
                Okpo = NormalizeSingleLine(result?.Organization.Okpo),
                Okogu = NormalizeSingleLine(result?.Organization.Okogu),
                Oktmo = NormalizeSingleLine(result?.Organization.Oktmo)
            };
        }

        private static ContragentOrganizationHistoryItem CopyRegistrationForLegalEntityChange(
            ContragentOrganizationHistoryItem registration)
        {
            return new ContragentOrganizationHistoryItem
            {
                Id = registration.Id,
                OrganizationId = registration.OrganizationId,
                Name = registration.Name,
                FullName = registration.FullName,
                Inn = registration.Inn,
                Kpp = registration.Kpp,
                Division = registration.Division,
                OwnershipName = registration.OwnershipName,
                OwnershipId = registration.OwnershipId,
                OwnershipCode = registration.OwnershipCode,
                Ogrn = registration.Ogrn,
                Okfc = registration.Okfc,
                Okopf = registration.Okopf,
                Okpo = registration.Okpo,
                Okogu = registration.Okogu,
                Okved = registration.Okved,
                Oktmo = registration.Oktmo,
                ListKey = registration.ListKey,
                CreatedAt = registration.CreatedAt,
                UpdatedAt = registration.UpdatedAt,
                OriginalIsActive = registration.OriginalIsActive,
                IsActive = false,
                IsMarkedForDestroy = registration.IsMarkedForDestroy
            };
        }

        private static string BuildFnsImportSelectionLabel(FnsContragentLookupResult result)
        {
            var requisites = string.Join(
                " / ",
                new[] { result.Organization.Inn, result.Organization.Kpp }
                    .Select(NormalizeSingleLine)
                    .Where(static value => !string.IsNullOrWhiteSpace(value)));
            var name = NormalizeSingleLine(result.Organization.Name);
            var address = NormalizeSingleLine(result.RealAddress.Value);

            return string.Join(
                " · ",
                new[] { requisites, name, address }
                    .Where(static value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string NormalizeSingleLine(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static IReadOnlyList<FnsCompareRow> BuildFnsCompareRows(
            ContragentEditViewModel local,
            FnsContragentLookupResult remote)
        {
            var remoteContacts = remote.Contacts
                .Where(static contact => !string.IsNullOrWhiteSpace(contact.Value) && !string.IsNullOrWhiteSpace(contact.Type))
                .ToList();
            var localContactValues = SplitContactText(local.ContactsText).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            var newRemoteContacts = remoteContacts
                .Where(contact => !localContactValues.Contains(contact.Value.Trim()))
                .ToList();

            return
            [
                TextRow("name", "Наименование", local.Name, remote.Organization.Name, (viewModel, value) => viewModel.Name = value),
                TextRow("full_name", "Полное наименование", local.FullName, remote.Organization.FullName, (viewModel, value) => viewModel.FullName = value),
                TextRow("inn", "ИНН", local.Inn, remote.Organization.Inn, (viewModel, value) => viewModel.Inn = value),
                TextRow("kpp", "КПП", local.Kpp, remote.Organization.Kpp, (viewModel, value) => viewModel.Kpp = value),
                TextRow("ogrn", "ОГРН", local.Ogrn, remote.Organization.Ogrn, (viewModel, value) => viewModel.Ogrn = value),
                TextRow("okopf", "ОКОПФ", local.Okopf, remote.Organization.Okopf, (viewModel, value) => viewModel.Okopf = value),
                TextRow("okpo", "ОКПО", local.Okpo, remote.Organization.Okpo, (viewModel, value) => viewModel.Okpo = value),
                TextRow("okogu", "ОКОГУ", local.Okogu, remote.Organization.Okogu, (viewModel, value) => viewModel.Okogu = value),
                TextRow("okfc", "ОКФС", local.Okfc, remote.Organization.Okfc, (viewModel, value) => viewModel.Okfc = value),
                TextRow("oktmo", "ОКТМО", local.Oktmo, remote.Organization.Oktmo, (viewModel, value) => viewModel.Oktmo = value),
                LookupRow(
                    "ownership",
                    "Форма собственности",
                    GetOptionLabel(local.OwnershipOptions, local.SelectedOwnershipId),
                    GetOptionLabel(local.OwnershipOptions, remote.Organization.OwnershipId) ?? remote.Organization.OwnershipOkopf,
                    remote.Organization.OwnershipId,
                    (viewModel, value) => viewModel.SelectedOwnershipId = value),
                LookupRow(
                    "region",
                    "Регион",
                    GetOptionLabel(local.RegionOptions, local.SelectedRegionId),
                    GetOptionLabel(local.RegionOptions, remote.Region?.Id) ?? remote.Region?.Name,
                    remote.Region?.Id,
                    (viewModel, value) => viewModel.SelectedRegionId = value),
                TextRow("address", "Адрес", local.AddressReal, remote.RealAddress.Value, (viewModel, value) => viewModel.CommitAddressInput(value)),
                new FnsCompareRow
                {
                    Key = "contacts",
                    Label = "Контакты",
                    LocalValue = string.Join(", ", SplitContactText(local.ContactsText)),
                    RemoteValue = string.Join(", ", remoteContacts.Select(static contact => contact.Value)),
                    CanApply = newRemoteContacts.Count > 0,
                    IsChecked = newRemoteContacts.Count > 0,
                    Apply = viewModel =>
                    {
                        if (newRemoteContacts.Count == 0)
                        {
                            return;
                        }

                        var values = SplitContactText(viewModel.ContactsText).ToList();
                        values.AddRange(newRemoteContacts.Select(static contact => contact.Value.Trim()));
                        viewModel.ContactsText = string.Join(Environment.NewLine, values.Distinct(StringComparer.CurrentCultureIgnoreCase));
                    }
                }
            ];
        }

        private static FnsCompareRow TextRow(
            string key,
            string label,
            string localValue,
            string? remoteValue,
            Action<ContragentEditViewModel, string> apply)
        {
            var normalizedRemote = remoteValue?.Trim() ?? string.Empty;
            return new FnsCompareRow
            {
                Key = key,
                Label = label,
                LocalValue = localValue.Trim(),
                RemoteValue = normalizedRemote,
                CanApply = !string.IsNullOrWhiteSpace(normalizedRemote),
                IsChecked = !string.IsNullOrWhiteSpace(normalizedRemote)
                    && !string.Equals(localValue.Trim(), normalizedRemote, StringComparison.CurrentCulture),
                Apply = viewModel => apply(viewModel, normalizedRemote)
            };
        }

        private static FnsCompareRow LookupRow(
            string key,
            string label,
            string? localValue,
            string? remoteValue,
            long? remoteId,
            Action<ContragentEditViewModel, long?> apply)
        {
            return new FnsCompareRow
            {
                Key = key,
                Label = label,
                LocalValue = localValue?.Trim() ?? string.Empty,
                RemoteValue = remoteValue?.Trim() ?? string.Empty,
                CanApply = remoteId is not null,
                IsChecked = remoteId is not null && !string.Equals(localValue?.Trim(), remoteValue?.Trim(), StringComparison.CurrentCulture),
                Apply = viewModel => apply(viewModel, remoteId)
            };
        }

        private static FrameworkElement BuildFnsCompareDialogContent(IReadOnlyList<FnsCompareRow> rows)
        {
            var root = new StackPanel
            {
                Spacing = 0,
                Width = 1180
            };

            root.Children.Add(BuildFnsCompareHeaderRow());

            for (var index = 0; index < rows.Count; index++)
            {
                root.Children.Add(BuildFnsCompareValueRow(rows[index], index));
            }

            return new ScrollViewer
            {
                MaxHeight = 580,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollMode = ScrollMode.Enabled,
                Content = root
            };
        }

        private static Grid BuildFnsCompareDialogHeader()
        {
            var header = new Grid
            {
                Width = 1180,
                Padding = new Thickness(4, 4, 4, 4)
            };

            header.Children.Add(new TextBlock
            {
                Text = "Сверка с ФНС",
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            return header;
        }

        private static Grid BuildFnsCompareHeaderRow()
        {
            var grid = CreateFnsCompareGrid();
            grid.Padding = new Thickness(0, 0, 0, 6);
            grid.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellMutedPanelBackgroundBrush"];

            AddCompareText(grid, "Поле", 0, isHeader: true);
            AddCompareText(grid, "Локально", 1, isHeader: true);
            AddCompareText(grid, "ФНС", 2, isHeader: true);
            AddCompareText(grid, "Обн", 3, isHeader: true, horizontalAlignment: HorizontalAlignment.Center);
            return grid;
        }

        private static Grid BuildFnsCompareValueRow(FnsCompareRow row, int index)
        {
            var grid = CreateFnsCompareGrid();
            grid.Padding = new Thickness(0, 4, 0, 4);
            grid.Background = index % 2 == 1
                ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellMutedPanelBackgroundBrush"]
                : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellPanelBackgroundBrush"];

            AddCompareText(grid, row.Label, 0);
            AddCompareText(grid, string.IsNullOrWhiteSpace(row.LocalValue) ? "-" : row.LocalValue, 1);
            AddCompareText(grid, string.IsNullOrWhiteSpace(row.RemoteValue) ? "-" : row.RemoteValue, 2);

            var checkBox = new CheckBox
            {
                Content = null,
                IsChecked = row.IsChecked,
                IsEnabled = row.CanApply,
                MinWidth = 0,
                Width = 20,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = row
            };
            checkBox.Checked += FnsCompareCheckBoxChanged;
            checkBox.Unchecked += FnsCompareCheckBoxChanged;

            var checkBoxHost = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            checkBoxHost.Children.Add(checkBox);
            Grid.SetColumn(checkBoxHost, 3);
            grid.Children.Add(checkBoxHost);
            return grid;
        }

        private static Grid CreateFnsCompareGrid()
        {
            var grid = new Grid
            {
                ColumnSpacing = 10,
                Width = 1180
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            return grid;
        }

        private static void AddCompareText(
            Grid grid,
            string text,
            int column,
            bool isHeader = false,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.WrapWholeWords,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = horizontalAlignment,
                FontWeight = isHeader ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
            };

            Grid.SetColumn(textBlock, column);
            grid.Children.Add(textBlock);
        }

        private static void FnsCompareCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { Tag: FnsCompareRow row } checkBox)
            {
                row.IsChecked = checkBox.IsChecked == true;
            }
        }

        private static IReadOnlyList<string> SplitContactText(string contactsText)
        {
            return contactsText
                .Split([Environment.NewLine, "\n", ";", ","], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .ToList();
        }

        private static string? GetOptionLabel(IReadOnlyList<CbsTableFilterOptionDefinition> options, object? value)
        {
            if (value is null)
            {
                return null;
            }

            var normalizedValue = value.ToString();
            return options.FirstOrDefault(option =>
                string.Equals(option.Value?.ToString(), normalizedValue, StringComparison.OrdinalIgnoreCase))?.Label;
        }

        private static bool IsValidInn(string value)
        {
            var text = value.Trim();
            return (text.Length == 10 || text.Length == 12)
                && text.All(char.IsDigit);
        }

        private async Task RecalculateHolidayStagesAsync()
        {
            if (_viewModel.SelectedRow is null)
            {
                return;
            }

            var holiday = TryCreateHolidayInterval(_viewModel.SelectedRow);
            if (holiday is null)
            {
                await ShowErrorDialogAsync("Не удалось пересчитать сроки.", "Не удалось определить период выбранного календарного дня.");
                return;
            }

            _isHolidayRecalcInProgress = true;
            UpdateSelectionActionButtons();

            try
            {
                var affectedStages = await LoadAffectedStagesAsync(holiday);
                if (affectedStages.Count == 0)
                {
                    await ShowErrorDialogAsync("Пересчёт не требуется", "Этапы в выбранном интервале не найдены.");
                    return;
                }

                var uniqueContracts = affectedStages
                    .Select(static stage => stage.ContractId)
                    .Where(static contractId => contractId is not null)
                    .Distinct()
                    .Count();

                var confirmDialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "Пересчитать сроки",
                    PrimaryButtonText = "Пересчитать",
                    CloseButtonText = "Отмена",
                    DefaultButton = ContentDialogButton.Primary,
                    Content = $"Будут затронуты {affectedStages.Count} этапа(ов) в {uniqueContracts} контракте(ах). Продолжить?"
                };
                DialogChrome.Apply(confirmDialog);

                if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }

                var holidays = await LoadHolidayCalendarAsync();
                var processedCount = 0;
                var updatedCount = 0;
                var errors = new List<string>();
                var touchedContracts = new HashSet<long>();

                foreach (var stage in affectedStages)
                {
                    processedCount++;

                    try
                    {
                        var patch = BuildStagePatch(stage, holiday, holidays, errors);
                        if (patch is null)
                        {
                            continue;
                        }

                        await _referenceCrudService.UpdateAsync(StageEditDefinition, patch);
                        updatedCount++;
                        if (stage.ContractId is long contractId)
                        {
                            touchedContracts.Add(contractId);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Этап #{stage.Id}: ошибка обновления - {ex.Message}");
                    }
                }

                await _viewModel.ReloadCurrentReferenceAsync();

                if (errors.Count > 0)
                {
                    await ShowErrorDialogAsync(
                        "Пересчёт завершён с ошибками",
                        $"Обработано: {processedCount}. Изменено: {updatedCount}. Контрактов затронуто: {touchedContracts.Count}.{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, errors.Take(10))}");
                }
                else
                {
                    ShowSuccessNotification(
                        "Пересчёт завершён",
                        $"Этапов обработано: {processedCount}, изменено: {updatedCount}, контрактов затронуто: {touchedContracts.Count}");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Ошибки при пересчёте сроков", ex.Message);
            }
            finally
            {
                _isHolidayRecalcInProgress = false;
                UpdateSelectionActionButtons();
            }
        }

        private async Task<IReadOnlyList<HolidayCalendarDay>> LoadHolidayCalendarAsync(CancellationToken cancellationToken = default)
        {
            var rows = await _holidayRecalculationService.GetHolidayCalendarAsync(cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(TryCreateHolidayCalendarDay)
                .Where(static item => item is not null)
                .Cast<HolidayCalendarDay>()
                .ToList();
        }

        private async Task<IReadOnlyList<StageRecalcCandidate>> LoadAffectedStagesAsync(
            HolidayInterval holiday,
            CancellationToken cancellationToken = default)
        {
            var rows = await _holidayRecalculationService.GetAffectedStagesAsync(
                holiday.IntervalStart,
                holiday.IntervalEnd,
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(TryCreateStageCandidate)
                .Where(static item => item is not null)
                .Cast<StageRecalcCandidate>()
                .ToList();
        }

        private static HolidayInterval? TryCreateHolidayInterval(ReferenceDataRow row)
        {
            var beginAt = TryParseDate(row.GetValue("begin_at"));
            if (beginAt is null)
            {
                return null;
            }

            var endAt = TryParseDate(row.GetValue("end_at")) ?? beginAt.Value.Date.AddDays(1).AddTicks(-1);

            return new HolidayInterval(
                IntervalStart: FormatRailsDate(beginAt.Value),
                IntervalEnd: FormatRailsDate(endAt),
                StartDate: beginAt.Value,
                EndDate: endAt);
        }

        private static HolidayCalendarDay? TryCreateHolidayCalendarDay(ReferenceDataRow row)
        {
            var beginAt = TryParseDate(row.GetValue("begin_at"));
            if (beginAt is null)
            {
                return null;
            }

            var endAt = TryParseDate(row.GetValue("end_at"));
            return new HolidayCalendarDay(
                BeginAt: FormatRailsDate(beginAt.Value),
                BeginDate: beginAt.Value.Date,
                EndAt: endAt is null ? null : FormatRailsDate(endAt.Value),
                EndDate: endAt?.Date,
                IsWorkingDay: TryGetBool(row.GetValue("work")) ?? false);
        }

        private static StageRecalcCandidate? TryCreateStageCandidate(ReferenceDataRow row)
        {
            var id = TryGetLong(row.GetValue("id"));
            if (id is null)
            {
                return null;
            }

            return new StageRecalcCandidate(
                Id: id.Value,
                ListKey: row.GetValue("list_key")?.ToString(),
                Head: row.GetValue("head")?.ToString(),
                Name: row.GetValue("name")?.ToString(),
                DeadlineKind: row.GetValue("deadline_kind")?.ToString(),
                PaymentDeadlineKind: row.GetValue("payment_deadline_kind")?.ToString(),
                Duration: TryGetInt(row.GetValue("duration")),
                PaymentDuration: TryGetInt(row.GetValue("payment_duration")),
                StartAt: row.GetValue("start_at")?.ToString(),
                DeadlineAt: row.GetValue("deadline_at")?.ToString(),
                PaymentAt: row.GetValue("payment_at")?.ToString(),
                PrepaymentAt: row.GetValue("prepayment_at")?.ToString(),
                FundedAt: row.GetValue("funded_at")?.ToString(),
                PaymentDeadlineAt: row.GetValue("payment_deadline_at")?.ToString(),
                ContractId: TryGetLong(row.GetValue("contract.id")));
        }

        private static Dictionary<string, object?>? BuildStagePatch(
            StageRecalcCandidate stage,
            HolidayInterval holiday,
            IReadOnlyList<HolidayCalendarDay> holidays,
            List<string> errors)
        {
            var execAffected = IsExecWorkingKind(stage.DeadlineKind)
                && IntersectsRange(stage.StartAt, stage.DeadlineAt, holiday.StartDate, holiday.EndDate);

            var payAffected = IsPaymentWorkingKind(stage.PaymentDeadlineKind)
                && IntersectsRange(stage.FundedAt, stage.PaymentDeadlineAt, holiday.StartDate, holiday.EndDate);

            var patch = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = stage.Id
            };

            if (!string.IsNullOrWhiteSpace(stage.ListKey))
            {
                patch["list_key"] = stage.ListKey;
            }

            if (execAffected)
            {
                var proposedExec = NextDeadlineFor(stage, holidays, errors);
                if (!string.IsNullOrWhiteSpace(proposedExec)
                    && !string.Equals(stage.DeadlineAt, proposedExec, StringComparison.Ordinal))
                {
                    patch["deadline_at"] = proposedExec;
                }
            }

            if (payAffected)
            {
                var proposedPay = NextPaymentDeadlineFor(stage, holidays, errors);
                if (!string.IsNullOrWhiteSpace(proposedPay)
                    && !string.Equals(stage.PaymentDeadlineAt, proposedPay, StringComparison.Ordinal))
                {
                    patch["payment_deadline_at"] = proposedPay;
                }
            }

            return patch.Count > 1 ? patch : null;
        }

        private static string? NextDeadlineFor(
            StageRecalcCandidate stage,
            IReadOnlyList<HolidayCalendarDay> holidays,
            List<string> errors)
        {
            if (stage.Duration is null)
            {
                errors.Add($"Этап #{stage.Id}: пропущен - нет длительности исполнения.");
                return null;
            }

            DateTime? baseDate = stage.DeadlineKind switch
            {
                "working_prepayment" => TryParseDate(stage.PrepaymentAt) ?? TryParseDate(stage.PaymentAt),
                "working_days" => TryParseDate(stage.StartAt),
                _ => null
            };

            if (baseDate is null)
            {
                errors.Add($"Этап #{stage.Id}: пропущен - нет базовой даты для срока исполнения.");
                return null;
            }

            return FormatRailsDate(AddWorkingDaysToDate(baseDate.Value, stage.Duration.Value, holidays));
        }

        private static string? NextPaymentDeadlineFor(
            StageRecalcCandidate stage,
            IReadOnlyList<HolidayCalendarDay> holidays,
            List<string> errors)
        {
            if (stage.PaymentDuration is null)
            {
                errors.Add($"Этап #{stage.Id}: пропущен - нет длительности оплаты.");
                return null;
            }

            if (!IsPaymentWorkingKind(stage.PaymentDeadlineKind))
            {
                return null;
            }

            var baseDate = TryParseDate(stage.FundedAt);
            if (baseDate is null)
            {
                errors.Add($"Этап #{stage.Id}: пропущен - нет базовой даты для срока оплаты.");
                return null;
            }

            return FormatRailsDate(AddWorkingDaysToDate(baseDate.Value, stage.PaymentDuration.Value, holidays));
        }

        private static DateTime AddWorkingDaysToDate(
            DateTime current,
            int days,
            IReadOnlyList<HolidayCalendarDay> holidays)
        {
            var result = current.Date.AddDays(-1);
            var daysCount = 0;

            while (daysCount < days)
            {
                result = result.AddDays(1);
                var calendarDay = holidays.FirstOrDefault(day =>
                    day.BeginAt == FormatRailsDate(result)
                    || day.EndAt == FormatRailsDate(result)
                    || (day.EndDate is DateTime endDate && endDate > result && day.BeginDate < result));

                var dayOfWeek = result.DayOfWeek;
                var isWeekend = dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                var isWorkingDay = (!isWeekend || calendarDay?.IsWorkingDay == true)
                    && (calendarDay is null || calendarDay.IsWorkingDay);

                if (isWorkingDay)
                {
                    daysCount++;
                }
            }

            return result;
        }

        private static bool IntersectsRange(string? itemStart, string? itemEnd, DateTime? intervalStart, DateTime? intervalEnd)
        {
            var start = TryParseDate(itemStart);
            var end = TryParseDate(itemEnd);
            if (start is null || end is null || intervalStart is null || intervalEnd is null)
            {
                return false;
            }

            return start.Value < intervalEnd.Value && end.Value > intervalStart.Value;
        }

        private static bool IsExecWorkingKind(string? kind)
        {
            return string.Equals(kind, "working_days", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "working_prepayment", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPaymentWorkingKind(string? kind)
        {
            return string.Equals(kind, "w_days", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime? TryParseDate(object? value)
        {
            if (value is null)
            {
                return null;
            }

            return value switch
            {
                DateTime dateTime => dateTime,
                DateTimeOffset dateTimeOffset => dateTimeOffset.LocalDateTime,
                string text when DateTime.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                    out var parsedDateTime) => parsedDateTime,
                string text when DateTime.TryParse(
                    text,
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                    out var parsedCurrentCultureDateTime) => parsedCurrentCultureDateTime,
                _ => null
            };
        }

        private static string FormatRailsDate(DateTime value)
        {
            return value.ToString("ddd MMM dd yyyy", CultureInfo.InvariantCulture);
        }

        private static long? TryGetLong(object? value)
        {
            return value switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string text when long.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static int? TryGetInt(object? value)
        {
            return value switch
            {
                int int32Value => int32Value,
                long int64Value => (int)int64Value,
                decimal decimalValue => (int)decimalValue,
                string text when int.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static bool? TryGetBool(object? value)
        {
            return value switch
            {
                bool boolValue => boolValue,
                string text when bool.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
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

            _referenceLookupCacheService.Invalidate(_viewModel.CurrentReference.Model);
            await _viewModel.ReloadCurrentReferenceAsync();
            ShowSuccessNotification(
                isCreateMode ? "Запись создана" : "Изменения сохранены",
                BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, TryGetSelectedRowId(savedRow)));
        }

        private async Task ShowEmployeeEditDialogAsync(
            bool isCreateMode,
            long? employeeId = null,
            ReferenceDefinition? employeeDefinition = null)
        {
            var definition = employeeDefinition ?? _viewModel.CurrentReference;
            if (definition is null)
            {
                return;
            }

            ReferenceDataRow? sourceRow = null;
            if (!isCreateMode)
            {
                sourceRow = await LoadEmployeeEditRowAsync(employeeId);
                if (sourceRow is null)
                {
                    await ShowErrorDialogAsync("Не удалось открыть сотрудника.", "Не удалось загрузить свежую карточку сотрудника.");
                    return;
                }
            }

            var state = EmployeeEditStateFactory.Create(definition, isCreateMode, sourceRow);
            var viewModel = new EmployeeEditViewModel(state, LoadPositionOptionsAsync, LoadContragentOptionsAsync);
            var dialog = new EmployeeEditDialog(viewModel)
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
                        ? EmployeeEditPayloadBuilder.BuildForCreate(viewModel)
                        : EmployeeEditPayloadBuilder.BuildForUpdate(viewModel);

                    if (!isCreateMode && payload.Count <= 1)
                    {
                        viewModel.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    savedRow = isCreateMode
                        ? await _referenceCrudService.CreateAsync(definition, payload)
                        : await _referenceCrudService.UpdateAsync(definition, payload);
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
            if (result != ContentDialogResult.Primary || savedRow is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(definition.Model);
            await _viewModel.ReloadCurrentReferenceAsync();
            ShowSuccessNotification(
                isCreateMode ? "Сотрудник создан" : "Изменения сотрудника сохранены",
                BuildReferenceNotificationMessage(definition.Title, TryGetSelectedRowId(savedRow)));
        }

        private async Task ShowContragentEditDialogAsync(
            bool isCreateMode,
            ContragentEditDialogState? initialState = null,
            bool isLegalEntityChangeMode = false)
        {
            if (_viewModel.CurrentReference is null)
            {
                return;
            }

            ReferenceDataRow? sourceRow = null;
            if (!isCreateMode && initialState is null)
            {
                sourceRow = await LoadContragentEditRowAsync();
                if (sourceRow is null)
                {
                    await ShowErrorDialogAsync("Не удалось открыть контрагента.", "Не удалось загрузить свежую карточку контрагента.");
                    return;
                }
            }

            var ownershipOptions = await LoadSimpleReferenceOptionsAsync("Ownership", "card");
            var regionOptions = await LoadSimpleReferenceOptionsAsync("Area", "item");
            var state = initialState
                ?? ContragentEditStateFactory.Create(
                    _viewModel.CurrentReference,
                    isCreateMode,
                    sourceRow,
                    ownershipOptions,
                    regionOptions);
            var viewModel = new ContragentEditViewModel(state, LoadAddressOptionsAsync);
            var dialog = new ContragentEditDialog(viewModel)
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
                    await EnsureContragentAddressAsync(viewModel);

                    var payload = isLegalEntityChangeMode
                        ? ContragentEditPayloadBuilder.BuildForLegalEntityChange(viewModel)
                        : isCreateMode
                            ? ContragentEditPayloadBuilder.BuildForCreate(viewModel)
                            : ContragentEditPayloadBuilder.BuildForUpdate(viewModel);

                    if (!isCreateMode && !isLegalEntityChangeMode && payload.Count <= 1)
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

            _referenceLookupCacheService.Invalidate(_viewModel.CurrentReference.Model);
            await _viewModel.ReloadCurrentReferenceAsync();
            ShowSuccessNotification(
                isCreateMode
                    ? "Контрагент создан"
                    : isLegalEntityChangeMode
                        ? "Юр.лицо контрагента изменено"
                        : "Изменения контрагента сохранены",
                BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, TryGetSelectedRowId(savedRow)));
        }

        private async Task<ReferenceDataRow?> LoadEmployeeEditRowAsync(
            long? employeeId = null,
            CancellationToken cancellationToken = default)
        {
            var id = employeeId ?? (_viewModel.SelectedRow is null ? null : TryGetSelectedRowId(_viewModel.SelectedRow));
            if (id is null)
            {
                return null;
            }

            var rows = await _dataQueryService.GetDataAsync<ReferenceDataRow>(
                new DataQueryRequest
                {
                    Model = "Employee",
                    Preset = "edit",
                    Filters = new Dictionary<string, object?>
                    {
                        ["id__eq"] = id.Value
                    },
                    Limit = 1
                },
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private async Task<ReferenceDataRow?> LoadStageEditRowAsync(CancellationToken cancellationToken = default)
        {
            if (_viewModel.SelectedRow is null)
            {
                return null;
            }

            var id = TryGetSelectedRowId(_viewModel.SelectedRow);
            if (id is null)
            {
                return null;
            }

            var rows = await _dataQueryService.GetDataAsync<ReferenceDataRow>(
                new DataQueryRequest
                {
                    Model = "Stage",
                    Preset = "edit",
                    Filters = new Dictionary<string, object?>
                    {
                        ["id__eq"] = id.Value
                    },
                    Limit = 1
                },
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private async Task<ReferenceDataRow?> LoadRevisionContractCardAsync(
            long contractId,
            CancellationToken cancellationToken = default)
        {
            var request = new DataQueryRequest
            {
                Model = "Contract",
                Preset = "edit",
                Filters = new Dictionary<string, object?>
                {
                    ["id__eq"] = contractId
                },
                Limit = 1
            };
            var rows = await _dataQueryService.GetDataAsync<ReferenceDataRow>(
                request,
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private async Task<ReferenceDataRow?> LoadRevisionContragentCardAsync(
            long contragentId,
            CancellationToken cancellationToken = default)
        {
            var request = new DataQueryRequest
            {
                Model = "Contragent",
                Preset = "card",
                Filters = new Dictionary<string, object?>
                {
                    ["id__eq"] = contragentId
                },
                Limit = 1
            };
            var rows = await _dataQueryService.GetDataAsync<ReferenceDataRow>(
                request,
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private async Task<ReferenceDataRow?> LoadContragentEditRowAsync(CancellationToken cancellationToken = default)
        {
            if (_viewModel.SelectedRow is null)
            {
                return null;
            }

            var id = TryGetSelectedRowId(_viewModel.SelectedRow);
            if (id is null)
            {
                return null;
            }

            return await LoadContragentEditRowAsync(id.Value, cancellationToken);
        }

        private async Task<ReferenceDataRow?> LoadContragentEditRowAsync(long id, CancellationToken cancellationToken = default)
        {
            var rows = await _dataQueryService.GetDataAsync<ReferenceDataRow>(
                new DataQueryRequest
                {
                    Model = "Contragent",
                    Preset = "edit",
                    Filters = new Dictionary<string, object?>
                    {
                        ["id__eq"] = id
                    },
                    Limit = 1
                },
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadSimpleReferenceOptionsAsync(
            string model,
            string preset,
            CancellationToken cancellationToken = default)
        {
            if (string.Equals(model, "Ownership", StringComparison.OrdinalIgnoreCase))
            {
                var items = await _referenceLookupCacheService.GetItemsAsync(model, preset, cancellationToken);
                return items
                    .Select(static item => new CbsTableFilterOptionDefinition
                    {
                        Value = item.Id,
                        Label = BuildOwnershipOptionLabel(item)
                    })
                    .Where(static option => option.Value is not null && !string.IsNullOrWhiteSpace(option.Label))
                    .DistinctBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            return await _referenceLookupCacheService.GetOptionsAsync(model, preset, cancellationToken);
        }

        private async Task EnsureContragentAddressAsync(
            ContragentEditViewModel viewModel,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(viewModel.AddressReal))
            {
                viewModel.SelectAddressOption(null);
                return;
            }

            if (viewModel.SelectedAddressId is not null)
            {
                return;
            }

            var existingAddress = await FindAddressOptionByValueAsync(viewModel.AddressReal, cancellationToken);
            if (existingAddress is not null)
            {
                viewModel.SelectAddressOption(existingAddress);
                return;
            }

            var createdAddress = await _referenceCrudService.CreateAsync(
                AddressEditDefinition,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["value"] = viewModel.AddressReal.Trim(),
                    ["area_id"] = viewModel.SelectedRegionId
                },
                cancellationToken);

            var createdId = TryGetSelectedRowId(createdAddress);
            if (createdId is null)
            {
                throw new InvalidOperationException("Не удалось получить ID созданного адреса.");
            }

            viewModel.SelectAddressOption(new CbsTableFilterOptionDefinition
            {
                Value = createdId.Value,
                Label = viewModel.AddressReal.Trim()
            });
        }

        private async Task<CbsTableFilterOptionDefinition?> FindAddressOptionByValueAsync(
            string value,
            CancellationToken cancellationToken = default)
        {
            var normalizedValue = value.Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return null;
            }

            var rows = await _dataQueryService.GetDataAsync<ReferenceDataRow>(
                new DataQueryRequest
                {
                    Model = "Address",
                    Preset = "item",
                    Filters = new Dictionary<string, object?>
                    {
                        ["value__eq"] = normalizedValue
                    },
                    Limit = 1
                },
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(ToAddressOption)
                .FirstOrDefault(static option => option is not null);
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadAddressOptionsAsync(
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
                    Model = "Address",
                    Preset = "item",
                    Filters = new Dictionary<string, object?>
                    {
                        ["value__cnt"] = normalizedSearchText
                    },
                    Sorts = ["value asc"],
                    Limit = 25
                },
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(ToAddressOption)
                .Where(static option => option is not null)
                .Cast<CbsTableFilterOptionDefinition>()
                .DistinctBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static CbsTableFilterOptionDefinition? ToAddressOption(ReferenceDataRow row)
        {
            var id = row.GetValue("id");
            var label = row.GetValue("value")?.ToString();
            return id is null || string.IsNullOrWhiteSpace(label)
                ? null
                : new CbsTableFilterOptionDefinition
                {
                    Value = id,
                    Label = label
                };
        }

        private static string BuildOwnershipOptionLabel(ReferenceLookupItem item)
        {
            if (string.IsNullOrWhiteSpace(item.FullName)
                || string.Equals(item.Name, item.FullName, StringComparison.CurrentCultureIgnoreCase))
            {
                return item.DisplayName;
            }

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                return item.FullName;
            }

            return $"{item.Name} - {item.FullName}";
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

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadContragentOptionsAsync(
            string searchText,
            CancellationToken cancellationToken)
        {
            var normalizedSearchText = searchText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedSearchText))
            {
                return [];
            }

            var request = new DataQueryRequest
            {
                Model = "Contragent",
                Preset = "item",
                Filters = new Dictionary<string, object?>
                {
                    ["org.name_or_org.full_name__cnt"] = normalizedSearchText
                },
                Sorts = ["org.name asc"],
                Limit = 25
            };

            var rows = await _dataQueryService.GetDataAsync<ReferenceDataRow>(
                request,
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(static row => new CbsTableFilterOptionDefinition
                {
                    Value = row.GetValue("id"),
                    Label =
                        row.GetValue("full_name")?.ToString()
                        ?? row.GetValue("name")?.ToString()
                        ?? string.Empty
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

        private static long? TryGetLongValue(ReferenceDataRow row, string fieldKey)
        {
            var value = row.GetValue(fieldKey);
            return value switch
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

        private sealed record HolidayInterval(string IntervalStart, string IntervalEnd, DateTime StartDate, DateTime EndDate);

        private sealed record HolidayCalendarDay(
            string BeginAt,
            DateTime BeginDate,
            string? EndAt,
            DateTime? EndDate,
            bool IsWorkingDay);

        private sealed record StageRecalcCandidate(
            long Id,
            string? ListKey,
            string? Head,
            string? Name,
            string? DeadlineKind,
            string? PaymentDeadlineKind,
            int? Duration,
            int? PaymentDuration,
            string? StartAt,
            string? DeadlineAt,
            string? PaymentAt,
            string? PrepaymentAt,
            string? FundedAt,
            string? PaymentDeadlineAt,
            long? ContractId);

        private sealed record FnsImportCriteria(string Inn, string Kpp, string Name);

        private sealed record FnsImportSelectionItem(string Label, FnsContragentLookupResult Result);

        private sealed class FnsCompareRow
        {
            public required string Key { get; init; }

            public required string Label { get; init; }

            public string LocalValue { get; init; } = string.Empty;

            public string RemoteValue { get; init; } = string.Empty;

            public bool CanApply { get; init; }

            public bool IsChecked { get; set; }

            public required Action<ContragentEditViewModel> Apply { get; init; }
        }
    }
}
