using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;
using CbsContractsDesktopClient.Services.Workspace;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.Shared.Dates;
using CbsContractsDesktopClient.ViewModels.References;
using CbsContractsDesktopClient.Stores.Table;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.Functional;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using static CbsContractsDesktopClient.Shared.Dates.BusinessCalendar;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class ContentHostView : ContentHostViewBase
    {
        private readonly TablePageStore _viewModel;
        private readonly IModelMutationService _modelMutationService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private readonly IHolidayRecalculationService _holidayRecalculationService;
        private readonly IDataQueryService _dataQueryService;
        private readonly IUserService _userService;
        private readonly ILocalUserSettingsService _localUserSettingsService;
        private readonly ContractWorkflowStore _contractWorkflowStore;
        private CancellationTokenSource? _filterDebounceCts;
        private CancellationTokenSource? _viewportCts;
        private CancellationTokenSource? _contragentDetailCts;
        private CancellationTokenSource? _rowDetailCts;
        private CancellationTokenSource? _routeCts;
        private string? _route;
        private bool _isViewportSubscribed;
        private bool _isLoaded;
        private bool _isHolidayRecalcInProgress;
        private bool _showStageCostFraction;
        private const int OziDepartmentId = 1;
        private const int CommersDepartmentId = 2;
        private const int FinDepartmentId = 3;
        private const string AddressModel = "Address";
        private const string RevisionTitle = "Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅРѕРµ СЃРѕРіР»Р°С€РµРЅРёРµ";
        private const string ContractModel = "Contract";
        private const string ProfileModel = "Profile";
        private static readonly IReadOnlyList<ContractRowDetailStrategy> RowDetailStrategies =
        [
            new RevisionRowDetailStrategy(),
            new StageRowDetailStrategy(),
            new ContractTableRowDetailStrategy()
        ];

        public ContentHostView()
        {
            _viewModel = App.Services.GetRequiredService<TablePageStore>();
            _modelMutationService = App.Services.GetRequiredService<IModelMutationService>();
            _referenceDefinitionService = App.Services.GetRequiredService<IReferenceDefinitionService>();
            _referenceLookupCacheService = App.Services.GetRequiredService<IReferenceLookupCacheService>();
            _holidayRecalculationService = App.Services.GetRequiredService<IHolidayRecalculationService>();
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _userService = App.Services.GetRequiredService<IUserService>();
            _localUserSettingsService = App.Services.GetRequiredService<ILocalUserSettingsService>();
            _contractWorkflowStore = App.Services.GetRequiredService<ContractWorkflowStore>();
            _showStageCostFraction = _localUserSettingsService.Get().ShowStageCostFraction;
            InitializeComponent();
            DataContext = _viewModel;
            ApplyStageCostFractionMode();
            UpdateSelectionActionButtons();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _contractWorkflowStore.PropertyChanged += OnContractWorkflowStorePropertyChanged;
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
            UpdateTableRowStyle();
            EnsureViewportSubscription();
            if (string.IsNullOrWhiteSpace(Route))
            {
                await _viewModel.EnsureLoadedAsync();
            }
            else
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
            _contragentDetailCts?.Cancel();
            _rowDetailCts?.Cancel();
            _contractWorkflowStore.PropertyChanged -= OnContractWorkflowStorePropertyChanged;
            RemoveViewportSubscription();
        }

        private async Task NavigateToRouteAsync(string? route)
        {
            _routeCts?.Cancel();
            _routeCts = new CancellationTokenSource();
            try
            {
                await _viewModel.NavigateToRouteAsync(route, _routeCts.Token);
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
                || e.PropertyName == nameof(TablePageStore.CurrentTablePage)
                || e.PropertyName == nameof(TablePageStore.CanEditRows)
                || e.PropertyName == nameof(TablePageStore.CanDeleteRows))
            {
                UpdateSelectionActionButtons();
            }

            if (e.PropertyName == nameof(TablePageStore.CurrentTablePage)
                || e.PropertyName == nameof(TablePageStore.CurrentRowStyleKey))
            {
                UpdateTableRowStyle();
            }

            if (e.PropertyName == nameof(TablePageStore.SelectedRow)
                || e.PropertyName == nameof(TablePageStore.ShowContragentDetailView))
            {
                _ = RefreshContragentDetailContractsAsync();
            }

            if (e.PropertyName == nameof(TablePageStore.SelectedRow))
            {
                _ = RefreshRowDetailAsync();
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

        private async Task RefreshRowDetailAsync()
        {
            _rowDetailCts?.Cancel();
            RevisionsDetailView.ContractRow = null;
            RevisionsDetailView.ContragentRow = null;
            _contractWorkflowStore.ClearRowDetailSelection();

            var strategy = ResolveRowDetailStrategy();
            if (strategy is null || !_viewModel.ShowContractDetailView || _viewModel.SelectedRow is null)
            {
                return;
            }

            var contractId = strategy.ResolveContractId(_viewModel.SelectedRow);
            var listContragentId = strategy.ResolveContragentId(_viewModel.SelectedRow);
            if (contractId is null && listContragentId is null)
            {
                return;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _rowDetailCts = cancellationTokenSource;

            try
            {
                var contractTask = contractId is long selectedContractId
                    ? LoadRowDetailRowSafelyAsync(
                        "Contract/edit",
                        () => LoadRevisionContractCardAsync(selectedContractId, cancellationTokenSource.Token),
                        cancellationTokenSource.Token)
                    : Task.FromResult<TableDataRow?>(null);
                var contragentTask = listContragentId is long selectedContragentId
                    ? LoadRowDetailRowSafelyAsync(
                        "Contragent/card",
                        () => LoadRevisionContragentCardAsync(selectedContragentId, cancellationTokenSource.Token),
                        cancellationTokenSource.Token)
                    : Task.FromResult<TableDataRow?>(null);

                var contract = await contractTask;
                var contragent = await contragentTask;
                if (cancellationTokenSource.IsCancellationRequested || !_viewModel.ShowContractDetailView)
                {
                    return;
                }

                if (_viewModel.SelectedRow is null
                    || !strategy.IsSameSelection(_viewModel.SelectedRow, contractId))
                {
                    return;
                }

                var contractContragentId = contract is null
                    ? null
                    : TryGetLongValue(contract, "contragent.id");
                if (contragent is null && contractContragentId is long loadedContragentId)
                {
                    contragent = await LoadRowDetailRowSafelyAsync(
                        "Contragent/card from Contract/card",
                        () => LoadRevisionContragentCardAsync(loadedContragentId, cancellationTokenSource.Token),
                        cancellationTokenSource.Token);
                }

                strategy.ApplySelection(_contractWorkflowStore, _viewModel.SelectedRow, contract, contragent);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    _contractWorkflowStore.ClearRowDetailSelection();
                }
            }
        }

        private static async Task<TableDataRow?> LoadRowDetailRowSafelyAsync(
            string title,
            Func<Task<TableDataRow?>> loadAsync,
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
            var filters = await _viewModel.ResetFiltersAsync();
            ReferenceTableView.ApplyFilterInputs(filters);
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
                ShowSuccessNotification("Р”Р°РЅРЅС‹Рµ СЃРєРѕРїРёСЂРѕРІР°РЅС‹", "РљР°СЂС‚РѕС‡РєР° РєРѕРЅС‚СЂР°РєС‚Р° СЃРєРѕРїРёСЂРѕРІР°РЅР° РІ Р±СѓС„РµСЂ РѕР±РјРµРЅР°.");
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
            ShowSuccessNotification("Р”Р°РЅРЅС‹Рµ СЃРєРѕРїРёСЂРѕРІР°РЅС‹", "РљР°СЂС‚РѕС‡РєР° РєРѕРЅС‚СЂР°РіРµРЅС‚Р° СЃРєРѕРїРёСЂРѕРІР°РЅР° РІ Р±СѓС„РµСЂ РѕР±РјРµРЅР°.");
        }

        private void CopyStageInfoButton_Click(object sender, RoutedEventArgs e)
        {
            var text = StageClipboardFormatter.BuildClipboardText(_viewModel.SelectedRow);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            ShowSuccessNotification("Р”Р°РЅРЅС‹Рµ СЃРєРѕРїРёСЂРѕРІР°РЅС‹", "Р­С‚Р°Рї СЃРєРѕРїРёСЂРѕРІР°РЅ РІ Р±СѓС„РµСЂ РѕР±РјРµРЅР°.");
        }

        private void CommentStageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement anchor)
            {
                return;
            }

            var commentBox = new TextBox
            {
                Width = 400,
                PlaceholderText = "Р’РІРµРґРёС‚Рµ РєРѕРјРјРµРЅС‚Р°СЂРёР№ + Enter"
            };
            var flyout = new Flyout
            {
                Content = new StackPanel
                {
                    Width = 400,
                    Children =
                    {
                        commentBox
                    }
                }
            };

            commentBox.KeyDown += async (_, args) =>
            {
                if (args.Key != VirtualKey.Enter)
                {
                    return;
                }

                args.Handled = true;
                await SaveStageCommentAsync(commentBox.Text, flyout);
            };
            flyout.Opened += (_, _) => commentBox.Focus(FocusState.Programmatic);
            flyout.ShowAt(anchor);
        }

        private async Task SaveStageCommentAsync(string? comment, Flyout flyout)
        {
            var normalizedComment = comment?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedComment))
            {
                return;
            }

            if (_viewModel.SelectedRow is null || TryGetSelectedRowId(_viewModel.SelectedRow) is not long stageId)
            {
                await ShowErrorDialogAsync("РљРѕРјРјРµРЅС‚Р°СЂРёР№ Рє СЌС‚Р°РїСѓ", "РќРµ СѓРґР°Р»РѕСЃСЊ РѕРїСЂРµРґРµР»РёС‚СЊ РІС‹Р±СЂР°РЅРЅС‹Р№ СЌС‚Р°Рї.");
                return;
            }

            if (_userService.CurrentUser?.ProfileId is not int profileId)
            {
                await ShowErrorDialogAsync("РљРѕРјРјРµРЅС‚Р°СЂРёР№ Рє СЌС‚Р°РїСѓ", "РќРµ СѓРґР°Р»РѕСЃСЊ РѕРїСЂРµРґРµР»РёС‚СЊ profile_id РїРѕР»СЊР·РѕРІР°С‚РµР»СЏ.");
                return;
            }

            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = stageId
            };
            var listKey = _viewModel.SelectedRow.GetValue("list_key")?.ToString();
            if (!string.IsNullOrWhiteSpace(listKey))
            {
                payload["list_key"] = listKey;
            }

            StageEditPayloadBuilderHelpers.AppendCommentAttributes(payload, normalizedComment, profileId);

            try
            {
                await SaveStagePayloadAsync(payload);
                flyout.Hide();
                ShowSuccessNotification("РљРѕРјРјРµРЅС‚Р°СЂРёР№ СЃРѕС…СЂР°РЅС‘РЅ", "РљРѕРјРјРµРЅС‚Р°СЂРёР№ Рє СЌС‚Р°РїСѓ РґРѕР±Р°РІР»РµРЅ.");
                await RefreshRowDetailAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕС…СЂР°РЅРёС‚СЊ РєРѕРјРјРµРЅС‚Р°СЂРёР№", ex.Message);
            }
        }

        private async void CreateStageEmployeeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedRow is null)
            {
                return;
            }

            if (!_referenceDefinitionService.TryGetByRoute("/employees", out var employeeDefinition))
            {
                await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕР·РґР°С‚СЊ СЃРѕС‚СЂСѓРґРЅРёРєР°.", "РЎРїСЂР°РІРѕС‡РЅРёРє СЃРѕС‚СЂСѓРґРЅРёРєРѕРІ РЅРµ РїРѕРґРєР»СЋС‡РµРЅ.");
                return;
            }

            var contragentId =
                TryGetLongValue(_viewModel.SelectedRow, "contract.contragent.id")
                ?? TryGetLongValue(_viewModel.SelectedRow, "contract.contragent_id")
                ?? TryGetLongValue(_viewModel.SelectedRow, "contragent.id")
                ?? TryGetLongValue(_viewModel.SelectedRow, "contragent_id");
            var contragentName =
                TryGetText(_viewModel.SelectedRow, "contract.contragent.name")
                ?? TryGetText(_viewModel.SelectedRow, "contract.contragent.org.name")
                ?? TryGetText(_viewModel.SelectedRow, "contract.contragent.org.full_name")
                ?? TryGetText(_viewModel.SelectedRow, "contragent.name");

            if (contragentId is null || string.IsNullOrWhiteSpace(contragentName))
            {
                await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕР·РґР°С‚СЊ СЃРѕС‚СЂСѓРґРЅРёРєР°.", "Р’ РІС‹Р±СЂР°РЅРЅРѕРј СЌС‚Р°РїРµ РѕС‚СЃСѓС‚СЃС‚РІСѓРµС‚ РєРѕРЅС‚СЂР°РіРµРЅС‚.");
                return;
            }

            await ShowEmployeeEditDialogAsync(
                isCreateMode: true,
                employeeDefinition: employeeDefinition,
                initialState: new EmployeeEditDialogState
                {
                    Definition = employeeDefinition,
                    IsCreateMode = true,
                    ContragentId = contragentId,
                    ContragentName = contragentName,
                    IsUsed = true
                });
        }

        private void ShowStageCostFractionButton_Click(object sender, RoutedEventArgs e)
        {
            _showStageCostFraction = ShowStageCostFractionButton.IsChecked == true;
            ApplyStageCostFractionMode();
            UpdateSelectionActionButtons();
            _ = SaveStageCostFractionModeAsync(_showStageCostFraction);
        }

        private void ApplyStageCostFractionMode()
        {
            if (ReferenceTableView is not null)
            {
                ReferenceTableView.ShowStageCostFraction = _showStageCostFraction;
            }

            if (ShowStageCostFractionButton is not null)
            {
                ShowStageCostFractionButton.IsChecked = _showStageCostFraction;
            }
        }

        private async Task SaveStageCostFractionModeAsync(bool showStageCostFraction)
        {
            var settings = await _localUserSettingsService.GetAsync();
            settings.ShowStageCostFraction = showStageCostFraction;
            await _localUserSettingsService.SaveAsync(settings);
        }

        private void ShowContragentCreateMenu(FrameworkElement anchor)
        {
            var menu = new MenuFlyout();

            var manualItem = new MenuFlyoutItem
            {
                Text = "Р СѓС‡РЅРѕР№ РІРІРѕРґ"
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
                await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ РѕС‚РєСЂС‹С‚СЊ СЃРѕС‚СЂСѓРґРЅРёРєР°.", "РЎРїСЂР°РІРѕС‡РЅРёРє СЃРѕС‚СЂСѓРґРЅРёРєРѕРІ РЅРµ РїРѕРґРєР»СЋС‡РµРЅ.");
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
            var confirmed = await ConfirmFilterClearAsync();
            if (!confirmed)
            {
                return;
            }

            _filterDebounceCts?.Cancel();
            var filters = await _viewModel.ClearFiltersAsync();
            ReferenceTableView.ApplyFilterInputs(filters);
        }

        private async void SaveStageFiltersMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsStagesTable)
            {
                await ShowInfoDialogAsync(
                    "РЎРѕС…СЂР°РЅРµРЅРёРµ С„РёР»СЊС‚СЂРѕРІ",
                    "РЎРѕС…СЂР°РЅРµРЅРёРµ РёР·Р±СЂР°РЅРЅС‹С… С„РёР»СЊС‚СЂРѕРІ РїРѕРґРґРµСЂР¶Р°РЅРѕ С‚РѕР»СЊРєРѕ РґР»СЏ С‚Р°Р±Р»РёС†С‹ СЌС‚Р°РїРѕРІ.");
                return;
            }

            var user = _userService.CurrentUser;
            try
            {
                var settingsPayload = StageTableFilterSettingsPayloadBuilder.Build(
                    user?.ProfileId,
                    user?.Statuses,
                    user?.ContractsTypes,
                    _viewModel.CurrentFilters,
                    _viewModel.CurrentFilterOptionsSources);

                if (!settingsPayload.HasChanges)
                {
                    await ShowInfoDialogAsync(
                        "РЎРѕС…СЂР°РЅРµРЅРёРµ С„РёР»СЊС‚СЂРѕРІ",
                        "РР·Р±СЂР°РЅРЅС‹Рµ С„РёР»СЊС‚СЂС‹ РЅРµ РёР·РјРµРЅРµРЅС‹.");
                    return;
                }

                var confirmed = await ConfirmSettingsChangeAsync(
                    "РЎРѕС…СЂР°РЅРёС‚СЊ С‚РµРєСѓС‰РёРµ С„РёР»СЊС‚СЂС‹ СЌС‚Р°РїРѕРІ РєР°Рє РЅР°С‡Р°Р»СЊРЅС‹Рµ СѓСЃС‚Р°РЅРѕРІРєРё С„РёР»СЊС‚СЂР°С†РёРё?");
                if (!confirmed)
                {
                    return;
                }

                DiagnosticsFileLogger.AppendBlock(
                    "STAGE FILTER SETTINGS UPDATE REQUEST",
                    $"payload={System.Text.Json.JsonSerializer.Serialize(settingsPayload.Payload)}");
                await _modelMutationService.UpdateAsync(ProfileModel, settingsPayload.Payload);

                if (user is not null)
                {
                    user.Statuses = settingsPayload.StatusesJson;
                    user.ContractsTypes = settingsPayload.ContractsTypesJson;
                }

                _viewModel.AppendUiTrace("STAGE FILTER SETTINGS SAVED");
                ShowSuccessNotification(
                    "РќР°СЃС‚СЂРѕР№РєРё СЃРѕС…СЂР°РЅРµРЅС‹",
                    "РќР°С‡Р°Р»СЊРЅС‹Рµ СѓСЃС‚Р°РЅРѕРІРєРё С„РёР»СЊС‚СЂР°С†РёРё СЌС‚Р°РїРѕРІ РѕР±РЅРѕРІР»РµРЅС‹.");
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕС…СЂР°РЅРёС‚СЊ С„РёР»СЊС‚СЂС‹", ex.Message);
            }
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
            var isStagesTable = IsStagesTableActive();
            var isContractDetailTable = IsContractDetailTableActive();
            var hasWorkflowContract = _contractWorkflowStore.Contract is { IsPlaceholder: false };
            var canRecalculateHoliday = hasSelectedRow && isHolidayReference && !_isHolidayRecalcInProgress;
            var canCopyStageInfo = hasSelectedRow && isStagesTable;
            var canCommentStage = hasSelectedRow && isStagesTable && _userService.CurrentUser?.ProfileId is not null;
            var canCreateStageEmployee = hasSelectedRow && isStagesTable;
            var canCopyContragentDetails = hasSelectedRow && isContragentReference;
            var showContractCopyDetails = isContractDetailTable && !isStagesTable;
            var canCopyRevisionContract = showContractCopyDetails && hasWorkflowContract;
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

            if (CopyContragentDetailsButton is not null)
            {
                CopyContragentDetailsButton.Visibility = isContragentReference || showContractCopyDetails ? Visibility.Visible : Visibility.Collapsed;
                CopyContragentDetailsButton.IsEnabled = canCopyDetails;
                ToolTipService.SetToolTip(
                    CopyContragentDetailsButton,
                    showContractCopyDetails
                        ? "РЎРєРѕРїРёСЂРѕРІР°С‚СЊ РґР°РЅРЅС‹Рµ РєРѕРЅС‚СЂР°РєС‚Р°"
                        : "РЎРєРѕРїРёСЂРѕРІР°С‚СЊ РґР°РЅРЅС‹Рµ РєРѕРЅС‚СЂР°РіРµРЅС‚Р°");
                CopyContragentDetailsButton.Foreground = canCopyDetails
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkSlateBlue)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }

            if (CopyStageInfoButton is not null)
            {
                CopyStageInfoButton.Visibility = isStagesTable ? Visibility.Visible : Visibility.Collapsed;
                CopyStageInfoButton.IsEnabled = canCopyStageInfo;
                CopyStageInfoButton.Foreground = canCopyStageInfo
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DarkSlateBlue)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }

            if (CommentStageButton is not null)
            {
                CommentStageButton.Visibility = isStagesTable ? Visibility.Visible : Visibility.Collapsed;
                CommentStageButton.IsEnabled = canCommentStage;
                CommentStageButton.Foreground = canCommentStage
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.SeaGreen)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }

            if (CreateStageEmployeeButton is not null)
            {
                CreateStageEmployeeButton.Visibility = isStagesTable ? Visibility.Visible : Visibility.Collapsed;
                CreateStageEmployeeButton.IsEnabled = canCreateStageEmployee;
                CreateStageEmployeeButton.Foreground = canCreateStageEmployee
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.SeaGreen)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }

            if (ShowStageCostFractionButton is not null)
            {
                ShowStageCostFractionButton.Visibility = isStagesTable ? Visibility.Visible : Visibility.Collapsed;
                ShowStageCostFractionButton.IsEnabled = isStagesTable;
                ShowStageCostFractionButton.Foreground = _showStageCostFraction && isStagesTable
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
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    _viewModel.CurrentTablePage?.Route,
                    "/contracts",
                    StringComparison.OrdinalIgnoreCase);
        }

        private ContractRowDetailStrategy? ResolveRowDetailStrategy()
        {
            var route = _viewModel.CurrentTablePage?.Route;
            return RowDetailStrategies.FirstOrDefault(strategy =>
                string.Equals(strategy.Route, route, StringComparison.OrdinalIgnoreCase));
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

            var reference = _viewModel.CurrentReference;
            if (reference is null)
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
            IReadOnlyDictionary<string, object?>? savedPayload = null;

            dialog.SaveRequestedAsync += async args =>
            {
                var values = isCreateMode
                    ? ReferenceEditPayloadBuilder.BuildForCreate(dialogViewModel)
                    : ReferenceEditPayloadBuilder.BuildForUpdate(dialogViewModel);
                savedPayload = values;

                var action = isCreateMode ? "CREATE" : "EDIT";
                var keys = values.Count == 0
                    ? "<empty>"
                    : string.Join(", ", values.Keys);
                _viewModel.AppendUiTrace($"REFERENCE {action} DIALOG CONFIRMED keys={keys}");

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
            await RefreshReferenceAfterSaveAsync(isCreateMode, savedRow, savedPayload);
            ShowSuccessNotification(
                isCreateMode ? "Р—Р°РїРёСЃСЊ СЃРѕР·РґР°РЅР°" : "РР·РјРµРЅРµРЅРёСЏ СЃРѕС…СЂР°РЅРµРЅС‹",
                BuildReferenceNotificationMessage(reference.Title, TryGetSelectedRowId(savedRow)));
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
                await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ РѕС‚РєСЂС‹С‚СЊ СЂРµРІРёР·РёСЋ.", ex.Message);
                return;
            }

            TableDataRow? savedRow = null;
            IReadOnlyDictionary<string, object?>? revisionPayload = null;
            dialog.PrimaryButtonClick += async (_, args) =>
            {
                var deferral = args.GetDeferral();
                try
                {
                    revisionPayload = dialog.BuildPayload();
                    savedRow = await _modelMutationService.UpdateAsync(
                        GetCurrentTableModel(),
                        revisionPayload);
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

            _referenceLookupCacheService.Invalidate(GetCurrentTableModel());
            await RefreshReferenceAfterSaveAsync(false, savedRow, revisionPayload);
            ShowSuccessNotification(
                "Р РµРІРёР·РёСЏ СЃРѕС…СЂР°РЅРµРЅР°",
                BuildReferenceNotificationMessage(RevisionTitle, TryGetSelectedRowId(savedRow)));
        }

        private async Task ShowStageEditDialogAsync()
        {
            if (_viewModel.SelectedRow is null)
            {
                return;
            }

            if (_userService.CurrentUser?.DepartmentId == OziDepartmentId)
            {
                await ShowStageOziEditDialogAsync();
                return;
            }

            if (_userService.CurrentUser?.DepartmentId == CommersDepartmentId)
            {
                await ShowStageCommerEditDialogAsync();
                return;
            }

            if (_userService.CurrentUser?.DepartmentId == FinDepartmentId)
            {
                await ShowStageFinEditDialogAsync();
                return;
            }

            await ShowErrorDialogAsync(
                "Р РµРґР°РєС‚РёСЂРѕРІР°РЅРёРµ СЌС‚Р°РїР°",
                "Р”РёР°Р»РѕРі СЂРµРґР°РєС‚РёСЂРѕРІР°РЅРёСЏ СЌС‚Р°РїР° РґР»СЏ РІР°С€РµРіРѕ РѕС‚РґРµР»Р° РїРѕРєР° РЅРµ СЂРµР°Р»РёР·РѕРІР°РЅ.");
        }

        private async Task ShowStageOziEditDialogAsync()
        {
            var sourceRow = await LoadStageEditRowAsync();
            if (sourceRow is null)
            {
                await ShowErrorDialogAsync("Р РµРґР°РєС‚РёСЂРѕРІР°РЅРёРµ СЌС‚Р°РїР°", "РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РіСЂСѓР·РёС‚СЊ РєР°СЂС‚РѕС‡РєСѓ РІС‹Р±СЂР°РЅРЅРѕРіРѕ СЌС‚Р°РїР°.");
                return;
            }

            _contractWorkflowStore.SetStageSelection(
                sourceRow,
                _contractWorkflowStore.Contract,
                _contractWorkflowStore.Contragent);

            var statusOptions = await _referenceLookupCacheService.GetOptionsAsync("Status");
            var employeeItems = await LoadOziEmployeeItemsAsync();
            StageOziEditDialog dialog;
            try
            {
                dialog = new StageOziEditDialog(
                    _contractWorkflowStore.SelectedStageEditState ?? StageEditState.FromRow(sourceRow),
                    _contractWorkflowStore.SelectedContractEditState,
                    statusOptions,
                    employeeItems,
                    _userService.CurrentUser?.ProfileId)
                {
                    XamlRoot = XamlRoot
                };
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ РѕС‚РєСЂС‹С‚СЊ СЌС‚Р°Рї.", ex.Message);
                return;
            }

            TableDataRow? savedRow = null;
            bool shouldRefreshSelectedRowDetails = false;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    var stagePayload = dialog.BuildPayload();
                    if (!HasUpdatePayloadChanges(stagePayload))
                    {
                        dialog.ShowErrorInfo("РќРµС‚ РёР·РјРµРЅРµРЅРёР№ РґР»СЏ СЃРѕС…СЂР°РЅРµРЅРёСЏ.");
                        args.Cancel = true;
                        return;
                    }

                    shouldRefreshSelectedRowDetails = ContainsNestedAttributes(stagePayload);
                    savedRow = await SaveStagePayloadAsync(stagePayload);

                    if (dialog.ShouldCloseContract())
                    {
                        await _modelMutationService.UpdateAsync(
                            ContractModel,
                            dialog.BuildContractClosePayload());
                    }
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

            _referenceLookupCacheService.Invalidate(GetCurrentTableModel());
            _viewModel.ApplyRowPatch(dialog.Id, dialog.BuildTablePatch());
            ShowSuccessNotification(
                "Р­С‚Р°Рї СЃРѕС…СЂР°РЅС‘РЅ",
                BuildReferenceNotificationMessage("Р­С‚Р°Рї", TryGetSelectedRowId(savedRow)));
            if (shouldRefreshSelectedRowDetails)
            {
                await RefreshRowDetailAsync();
            }
        }

        private async Task ShowStageCommerEditDialogAsync()
        {
            var sourceRow = await LoadStageEditRowAsync();
            if (sourceRow is null)
            {
                await ShowErrorDialogAsync("Р РµРґР°РєС‚РёСЂРѕРІР°РЅРёРµ СЌС‚Р°РїР°", "РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РіСЂСѓР·РёС‚СЊ РєР°СЂС‚РѕС‡РєСѓ РІС‹Р±СЂР°РЅРЅРѕРіРѕ СЌС‚Р°РїР°.");
                return;
            }

            _contractWorkflowStore.SetStageSelection(
                sourceRow,
                _contractWorkflowStore.Contract,
                _contractWorkflowStore.Contragent);

            var statusOptions = await _referenceLookupCacheService.GetOptionsAsync("Status");
            var taskKindItems = await _referenceLookupCacheService.GetItemsAsync("TaskKind");
            StageCommerEditDialog dialog;
            try
            {
                dialog = new StageCommerEditDialog(
                    _contractWorkflowStore.SelectedStageEditState ?? StageEditState.FromRow(sourceRow),
                    _contractWorkflowStore.SelectedContractEditState,
                    statusOptions,
                    taskKindItems,
                    _userService.CurrentUser?.ProfileId)
                {
                    XamlRoot = XamlRoot
                };
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ РѕС‚РєСЂС‹С‚СЊ СЌС‚Р°Рї.", ex.Message);
                return;
            }

            TableDataRow? savedRow = null;
            bool shouldRefreshSelectedRowDetails = false;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    var stagePayload = dialog.BuildPayload();
                    if (!HasUpdatePayloadChanges(stagePayload))
                    {
                        dialog.ShowErrorInfo("РќРµС‚ РёР·РјРµРЅРµРЅРёР№ РґР»СЏ СЃРѕС…СЂР°РЅРµРЅРёСЏ.");
                        args.Cancel = true;
                        return;
                    }

                    shouldRefreshSelectedRowDetails = ContainsNestedAttributes(stagePayload);
                    savedRow = await SaveStagePayloadAsync(stagePayload);

                    if (dialog.ShouldCloseContract())
                    {
                        await _modelMutationService.UpdateAsync(
                            ContractModel,
                            dialog.BuildContractClosePayload());
                    }
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

            _referenceLookupCacheService.Invalidate(GetCurrentTableModel());
            _viewModel.ApplyRowPatch(dialog.Id, dialog.BuildTablePatch());
            ShowSuccessNotification(
                "Р­С‚Р°Рї СЃРѕС…СЂР°РЅС‘РЅ",
                BuildReferenceNotificationMessage("Р­С‚Р°Рї", TryGetSelectedRowId(savedRow)));
            if (shouldRefreshSelectedRowDetails)
            {
                await RefreshRowDetailAsync();
            }
        }

        private async Task ShowStageFinEditDialogAsync()
        {
            var sourceRow = await LoadStageEditRowAsync();
            if (sourceRow is null)
            {
                await ShowErrorDialogAsync("Р РµРґР°РєС‚РёСЂРѕРІР°РЅРёРµ СЌС‚Р°РїР°", "РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РіСЂСѓР·РёС‚СЊ РєР°СЂС‚РѕС‡РєСѓ РІС‹Р±СЂР°РЅРЅРѕРіРѕ СЌС‚Р°РїР°.");
                return;
            }

            _contractWorkflowStore.SetStageSelection(
                sourceRow,
                _contractWorkflowStore.Contract,
                _contractWorkflowStore.Contragent);

            var statusOptions = await _referenceLookupCacheService.GetOptionsAsync("Status");
            StageFinEditDialog dialog;
            try
            {
                dialog = new StageFinEditDialog(
                    _contractWorkflowStore.SelectedStageEditState ?? StageEditState.FromRow(sourceRow),
                    _contractWorkflowStore.SelectedContractEditState,
                    statusOptions,
                    _userService.CurrentUser?.ProfileId)
                {
                    XamlRoot = XamlRoot
                };
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ РѕС‚РєСЂС‹С‚СЊ СЌС‚Р°Рї.", ex.Message);
                return;
            }

            TableDataRow? savedRow = null;
            bool shouldRefreshSelectedRowDetails = false;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    var stagePayload = dialog.BuildPayload();
                    var hasStageChanges = HasUpdatePayloadChanges(stagePayload);
                    var hasContractChanges = dialog.HasContractExternalNumberChanges();
                    if (!hasStageChanges && !hasContractChanges)
                    {
                        dialog.ShowErrorInfo("РќРµС‚ РёР·РјРµРЅРµРЅРёР№ РґР»СЏ СЃРѕС…СЂР°РЅРµРЅРёСЏ.");
                        args.Cancel = true;
                        return;
                    }

                    if (hasStageChanges)
                    {
                        shouldRefreshSelectedRowDetails = ContainsNestedAttributes(stagePayload);
                        savedRow = await SaveStagePayloadAsync(stagePayload);
                    }

                    if (hasContractChanges)
                    {
                        await _modelMutationService.UpdateAsync(
                            ContractModel,
                            dialog.BuildContractExternalNumberPayload());
                        shouldRefreshSelectedRowDetails = true;
                        savedRow ??= sourceRow;
                    }
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

            _referenceLookupCacheService.Invalidate(GetCurrentTableModel());
            _viewModel.ApplyRowPatch(dialog.Id, dialog.BuildTablePatch());
            ShowSuccessNotification(
                "Р­С‚Р°Рї СЃРѕС…СЂР°РЅС‘РЅ",
                BuildReferenceNotificationMessage("Р­С‚Р°Рї", TryGetSelectedRowId(savedRow)));
            if (shouldRefreshSelectedRowDetails)
            {
                await RefreshRowDetailAsync();
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
                await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ СѓРґР°Р»РёС‚СЊ Р·Р°РїРёСЃСЊ.", "РЈ РІС‹Р±СЂР°РЅРЅРѕР№ Р·Р°РїРёСЃРё РѕС‚СЃСѓС‚СЃС‚РІСѓРµС‚ РєРѕСЂСЂРµРєС‚РЅС‹Р№ ID.");
                return;
            }

            if (!await ConfirmDialogAsync(
                    "РЈРґР°Р»РµРЅРёРµ Р·Р°РїРёСЃРё",
                    "РЈРґР°Р»РёС‚СЊ РІС‹Р±СЂР°РЅРЅСѓСЋ Р·Р°РїРёСЃСЊ?",
                    "РЈРґР°Р»РёС‚СЊ",
                    defaultButton: ContentDialogButton.Close,
                    applyChrome: true))
            {
                return;
            }

            try
            {
                await _modelMutationService.DeleteAsync(_viewModel.CurrentReference.Model, id.Value);
                _referenceLookupCacheService.Invalidate(_viewModel.CurrentReference.Model);
                await _viewModel.ReloadCurrentReferenceAsync();
                ShowSuccessNotification(
                    "Р—Р°РїРёСЃСЊ СѓРґР°Р»РµРЅР°",
                    BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, id.Value));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ СѓРґР°Р»РёС‚СЊ Р·Р°РїРёСЃСЊ.", ex.Message);
            }
        }

        private async Task<bool> ConfirmSettingsChangeAsync(string message)
        {
            return await ConfirmDialogAsync(
                "РР·РјРµРЅРµРЅРёРµ РЅР°СЃС‚СЂРѕРµРє",
                message,
                "РЎРѕС…СЂР°РЅРёС‚СЊ");
        }

        private async Task<bool> ConfirmFilterClearAsync()
        {
            return await ConfirmDialogAsync(
                "РЎР±СЂРѕСЃ С„РёР»СЊС‚СЂРѕРІ",
                "РћС‡РёСЃС‚РёС‚СЊ РІСЃРµ С„РёР»СЊС‚СЂС‹ С‚РµРєСѓС‰РµР№ С‚Р°Р±Р»РёС†С‹?",
                "РћС‡РёСЃС‚РёС‚СЊ");
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
                await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ РїРµСЂРµСЃС‡РёС‚Р°С‚СЊ СЃСЂРѕРєРё.", "РќРµ СѓРґР°Р»РѕСЃСЊ РѕРїСЂРµРґРµР»РёС‚СЊ РїРµСЂРёРѕРґ РІС‹Р±СЂР°РЅРЅРѕРіРѕ РєР°Р»РµРЅРґР°СЂРЅРѕРіРѕ РґРЅСЏ.");
                return;
            }

            _isHolidayRecalcInProgress = true;
            UpdateSelectionActionButtons();

            try
            {
                var affectedStages = await LoadAffectedStagesAsync(holiday);
                if (affectedStages.Count == 0)
                {
                    await ShowErrorDialogAsync("РџРµСЂРµСЃС‡С‘С‚ РЅРµ С‚СЂРµР±СѓРµС‚СЃСЏ", "Р­С‚Р°РїС‹ РІ РІС‹Р±СЂР°РЅРЅРѕРј РёРЅС‚РµСЂРІР°Р»Рµ РЅРµ РЅР°Р№РґРµРЅС‹.");
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
                    Title = "РџРµСЂРµСЃС‡РёС‚Р°С‚СЊ СЃСЂРѕРєРё",
                    PrimaryButtonText = "РџРµСЂРµСЃС‡РёС‚Р°С‚СЊ",
                    CloseButtonText = "РћС‚РјРµРЅР°",
                    DefaultButton = ContentDialogButton.Primary,
                    Content = $"Р‘СѓРґСѓС‚ Р·Р°С‚СЂРѕРЅСѓС‚С‹ {affectedStages.Count} СЌС‚Р°РїР°(РѕРІ) РІ {uniqueContracts} РєРѕРЅС‚СЂР°РєС‚Рµ(Р°С…). РџСЂРѕРґРѕР»Р¶РёС‚СЊ?"
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

                        await _modelMutationService.UpdateAsync(GetCurrentTableModel(), patch);
                        updatedCount++;
                        if (stage.ContractId is long contractId)
                        {
                            touchedContracts.Add(contractId);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Р­С‚Р°Рї #{stage.Id}: РѕС€РёР±РєР° РѕР±РЅРѕРІР»РµРЅРёСЏ - {ex.Message}");
                    }
                }

                await _viewModel.ReloadCurrentReferenceAsync();

                if (errors.Count > 0)
                {
                    await ShowErrorDialogAsync(
                        "РџРµСЂРµСЃС‡С‘С‚ Р·Р°РІРµСЂС€С‘РЅ СЃ РѕС€РёР±РєР°РјРё",
                        $"РћР±СЂР°Р±РѕС‚Р°РЅРѕ: {processedCount}. РР·РјРµРЅРµРЅРѕ: {updatedCount}. РљРѕРЅС‚СЂР°РєС‚РѕРІ Р·Р°С‚СЂРѕРЅСѓС‚Рѕ: {touchedContracts.Count}.{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, errors.Take(10))}");
                }
                else
                {
                    ShowSuccessNotification(
                        "РџРµСЂРµСЃС‡С‘С‚ Р·Р°РІРµСЂС€С‘РЅ",
                        $"Р­С‚Р°РїРѕРІ РѕР±СЂР°Р±РѕС‚Р°РЅРѕ: {processedCount}, РёР·РјРµРЅРµРЅРѕ: {updatedCount}, РєРѕРЅС‚СЂР°РєС‚РѕРІ Р·Р°С‚СЂРѕРЅСѓС‚Рѕ: {touchedContracts.Count}");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("РћС€РёР±РєРё РїСЂРё РїРµСЂРµСЃС‡С‘С‚Рµ СЃСЂРѕРєРѕРІ", ex.Message);
            }
            finally
            {
                _isHolidayRecalcInProgress = false;
                UpdateSelectionActionButtons();
            }
        }

        private async Task<IReadOnlyList<HolidayCalendarDay>> LoadHolidayCalendarAsync(CancellationToken cancellationToken = default)
        {
            return await _holidayRecalculationService.GetHolidayCalendarDaysAsync(cancellationToken);
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

        private static HolidayInterval? TryCreateHolidayInterval(TableDataRow row)
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

        private static StageRecalcCandidate? TryCreateStageCandidate(TableDataRow row)
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
                errors.Add($"Р­С‚Р°Рї #{stage.Id}: РїСЂРѕРїСѓС‰РµРЅ - РЅРµС‚ РґР»РёС‚РµР»СЊРЅРѕСЃС‚Рё РёСЃРїРѕР»РЅРµРЅРёСЏ.");
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
                errors.Add($"Р­С‚Р°Рї #{stage.Id}: РїСЂРѕРїСѓС‰РµРЅ - РЅРµС‚ Р±Р°Р·РѕРІРѕР№ РґР°С‚С‹ РґР»СЏ СЃСЂРѕРєР° РёСЃРїРѕР»РЅРµРЅРёСЏ.");
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
                errors.Add($"Р­С‚Р°Рї #{stage.Id}: РїСЂРѕРїСѓС‰РµРЅ - РЅРµС‚ РґР»РёС‚РµР»СЊРЅРѕСЃС‚Рё РѕРїР»Р°С‚С‹.");
                return null;
            }

            if (!IsPaymentWorkingKind(stage.PaymentDeadlineKind))
            {
                return null;
            }

            var baseDate = TryParseDate(stage.FundedAt);
            if (baseDate is null)
            {
                errors.Add($"Р­С‚Р°Рї #{stage.Id}: РїСЂРѕРїСѓС‰РµРЅ - РЅРµС‚ Р±Р°Р·РѕРІРѕР№ РґР°С‚С‹ РґР»СЏ СЃСЂРѕРєР° РѕРїР»Р°С‚С‹.");
                return null;
            }

            return FormatRailsDate(AddWorkingDaysToDate(baseDate.Value, stage.PaymentDuration.Value, holidays));
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

            TableDataRow? savedRow = null;
            IReadOnlyDictionary<string, object?>? savedPayload = null;

            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    viewModel.ClearErrorInfo();

                    var payload = isCreateMode
                        ? ProfileEditPayloadBuilder.BuildForCreate(viewModel)
                        : ProfileEditPayloadBuilder.BuildForUpdate(viewModel);
                    savedPayload = payload;

                    if (!isCreateMode && payload.Count <= 1)
                    {
                        viewModel.ShowErrorInfo("РќРµС‚ РёР·РјРµРЅРµРЅРёР№ РґР»СЏ СЃРѕС…СЂР°РЅРµРЅРёСЏ.");
                        args.Cancel = true;
                        return;
                    }

                    savedRow = isCreateMode
                        ? await _modelMutationService.CreateAsync(_viewModel.CurrentReference!.Model, payload)
                        : await _modelMutationService.UpdateAsync(_viewModel.CurrentReference!.Model, payload);
                }
                catch (Exception ex)
                {
                    viewModel.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };

            await dialog.ShowAsync();
            if (!dialog.WasSaved || savedRow is null || _viewModel.CurrentReference is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(_viewModel.CurrentReference.Model);
            await RefreshReferenceAfterSaveAsync(isCreateMode, savedRow, savedPayload);
            ShowSuccessNotification(
                isCreateMode ? "Р—Р°РїРёСЃСЊ СЃРѕР·РґР°РЅР°" : "РР·РјРµРЅРµРЅРёСЏ СЃРѕС…СЂР°РЅРµРЅС‹",
                BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, TryGetSelectedRowId(savedRow)));
        }

        private async Task ShowEmployeeEditDialogAsync(
            bool isCreateMode,
            long? employeeId = null,
            ReferenceDefinition? employeeDefinition = null,
            EmployeeEditDialogState? initialState = null)
        {
            var definition = employeeDefinition ?? _viewModel.CurrentReference;
            if (definition is null)
            {
                return;
            }

            TableDataRow? sourceRow = null;
            if (!isCreateMode)
            {
                sourceRow = await LoadEmployeeEditRowAsync(employeeId);
                if (sourceRow is null)
                {
                    await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ РѕС‚РєСЂС‹С‚СЊ СЃРѕС‚СЂСѓРґРЅРёРєР°.", "РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РіСЂСѓР·РёС‚СЊ СЃРІРµР¶СѓСЋ РєР°СЂС‚РѕС‡РєСѓ СЃРѕС‚СЂСѓРґРЅРёРєР°.");
                    return;
                }
            }

            var state = initialState ?? EmployeeEditStateFactory.Create(definition, isCreateMode, sourceRow);
            var viewModel = new EmployeeEditViewModel(state, LoadPositionOptionsAsync, LoadContragentOptionsAsync);
            var dialog = new EmployeeEditDialog(viewModel)
            {
                XamlRoot = XamlRoot
            };

            TableDataRow? savedRow = null;
            IReadOnlyDictionary<string, object?>? savedPayload = null;

            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    viewModel.ClearErrorInfo();

                    var payload = isCreateMode
                        ? EmployeeEditPayloadBuilder.BuildForCreate(viewModel)
                        : EmployeeEditPayloadBuilder.BuildForUpdate(viewModel);
                    savedPayload = payload;

                    if (!isCreateMode && payload.Count <= 1)
                    {
                        viewModel.ShowErrorInfo("РќРµС‚ РёР·РјРµРЅРµРЅРёР№ РґР»СЏ СЃРѕС…СЂР°РЅРµРЅРёСЏ.");
                        args.Cancel = true;
                        return;
                    }

                    savedRow = isCreateMode
                        ? await _modelMutationService.CreateAsync(definition.Model, payload)
                        : await _modelMutationService.UpdateAsync(definition.Model, payload);
                }
                catch (Exception ex)
                {
                    viewModel.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };

            await dialog.ShowAsync();
            if (!dialog.WasSaved || savedRow is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(definition.Model);
            await RefreshReferenceAfterSaveAsync(isCreateMode, savedRow, savedPayload);
            ShowSuccessNotification(
                isCreateMode ? "РЎРѕС‚СЂСѓРґРЅРёРє СЃРѕР·РґР°РЅ" : "РР·РјРµРЅРµРЅРёСЏ СЃРѕС‚СЂСѓРґРЅРёРєР° СЃРѕС…СЂР°РЅРµРЅС‹",
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

            TableDataRow? sourceRow = null;
            if (!isCreateMode && initialState is null)
            {
                sourceRow = await LoadContragentEditRowAsync();
                if (sourceRow is null)
                {
                    await ShowErrorDialogAsync("РќРµ СѓРґР°Р»РѕСЃСЊ РѕС‚РєСЂС‹С‚СЊ РєРѕРЅС‚СЂР°РіРµРЅС‚Р°.", "РќРµ СѓРґР°Р»РѕСЃСЊ Р·Р°РіСЂСѓР·РёС‚СЊ СЃРІРµР¶СѓСЋ РєР°СЂС‚РѕС‡РєСѓ РєРѕРЅС‚СЂР°РіРµРЅС‚Р°.");
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

            TableDataRow? savedRow = null;
            IReadOnlyDictionary<string, object?>? savedPayload = null;

            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    viewModel.ClearErrorInfo();
                    await EnsureContragentAddressAsync(viewModel);

                    var payload = isLegalEntityChangeMode
                        ? ContragentEditPayloadBuilder.BuildForLegalEntityChange(viewModel)
                        : isCreateMode
                            ? ContragentEditPayloadBuilder.BuildForCreate(viewModel)
                            : ContragentEditPayloadBuilder.BuildForUpdate(viewModel);
                    savedPayload = payload;

                    if (!isCreateMode && !isLegalEntityChangeMode && payload.Count <= 1)
                    {
                        viewModel.ShowErrorInfo("РќРµС‚ РёР·РјРµРЅРµРЅРёР№ РґР»СЏ СЃРѕС…СЂР°РЅРµРЅРёСЏ.");
                        args.Cancel = true;
                        return;
                    }

                    savedRow = isCreateMode
                        ? await _modelMutationService.CreateAsync(_viewModel.CurrentReference!.Model, payload)
                        : await _modelMutationService.UpdateAsync(_viewModel.CurrentReference!.Model, payload);
                }
                catch (Exception ex)
                {
                    viewModel.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };

            await dialog.ShowAsync();
            if (!dialog.WasSaved || savedRow is null || _viewModel.CurrentReference is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(_viewModel.CurrentReference.Model);
            await RefreshReferenceAfterSaveAsync(isCreateMode, savedRow, savedPayload);
            ShowSuccessNotification(
                isCreateMode
                    ? "РљРѕРЅС‚СЂР°РіРµРЅС‚ СЃРѕР·РґР°РЅ"
                    : isLegalEntityChangeMode
                        ? "Р®СЂ.Р»РёС†Рѕ РєРѕРЅС‚СЂР°РіРµРЅС‚Р° РёР·РјРµРЅРµРЅРѕ"
                        : "РР·РјРµРЅРµРЅРёСЏ РєРѕРЅС‚СЂР°РіРµРЅС‚Р° СЃРѕС…СЂР°РЅРµРЅС‹",
                BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, TryGetSelectedRowId(savedRow)));
        }

        private async Task<TableDataRow?> LoadEmployeeEditRowAsync(
            long? employeeId = null,
            CancellationToken cancellationToken = default)
        {
            var id = employeeId ?? (_viewModel.SelectedRow is null ? null : TryGetSelectedRowId(_viewModel.SelectedRow));
            if (id is null)
            {
                return null;
            }

            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
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

        private async Task<TableDataRow?> LoadStageEditRowAsync(CancellationToken cancellationToken = default)
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

            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
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

        private async Task<TableDataRow?> LoadRevisionContractCardAsync(
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
            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                request,
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private async Task<TableDataRow?> LoadRevisionContragentCardAsync(
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
            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                request,
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private async Task<TableDataRow?> LoadContragentEditRowAsync(CancellationToken cancellationToken = default)
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

        private async Task<TableDataRow?> LoadContragentEditRowAsync(long id, CancellationToken cancellationToken = default)
        {
            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
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

        private async Task<IReadOnlyList<ReferenceLookupItem>> LoadOziEmployeeItemsAsync(
            CancellationToken cancellationToken = default)
        {
            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = "Employee",
                    Preset = "item",
                    Filters = new Dictionary<string, object?>
                    {
                        ["contragent_id__eq"] = 1L,
                        ["used__eq"] = true
                    },
                    Sorts = ["priority asc"],
                    Limit = 1000
                },
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(static row => new ReferenceLookupItem
                {
                    Model = "Employee",
                    Preset = "item",
                    Id = row.GetValue("id"),
                    Name = GetText(row, "name", "person.name", "full_name"),
                    FullName = GetText(row, "full_name", "name", "person.name"),
                    Code = GetText(row, "code"),
                    Row = row
                })
                .Where(static item => item.Id is not null && !string.IsNullOrWhiteSpace(item.DisplayName))
                .ToList();
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

            var createdAddress = await _modelMutationService.CreateAsync(
                AddressModel,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["value"] = viewModel.AddressReal.Trim(),
                    ["area_id"] = viewModel.SelectedRegionId
                },
                cancellationToken);

            var createdId = TryGetSelectedRowId(createdAddress);
            if (createdId is null)
            {
                throw new InvalidOperationException("РќРµ СѓРґР°Р»РѕСЃСЊ РїРѕР»СѓС‡РёС‚СЊ ID СЃРѕР·РґР°РЅРЅРѕРіРѕ Р°РґСЂРµСЃР°.");
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

            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
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

            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
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

        private static CbsTableFilterOptionDefinition? ToAddressOption(TableDataRow row)
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

            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
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

            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
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

        private static bool ContainsNestedAttributes(IReadOnlyDictionary<string, object?> payload)
        {
            return payload.Keys.Any(static key => key.EndsWith("_attributes", StringComparison.OrdinalIgnoreCase));
        }

        private Task<TableDataRow> SaveStagePayloadAsync(IReadOnlyDictionary<string, object?> payload)
        {
            return _modelMutationService.UpdateAsync(GetCurrentTableModel(), payload);
        }

        private string GetCurrentTableModel()
        {
            return _viewModel.CurrentTablePage?.Model
                ?? throw new InvalidOperationException("Current table page is required for model mutation.");
        }

        private static bool HasUpdatePayloadChanges(IReadOnlyDictionary<string, object?> payload)
        {
            return payload.Keys.Any(static key =>
                !string.Equals(key, "id", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "list_key", StringComparison.OrdinalIgnoreCase));
        }

        private async Task RefreshReferenceAfterSaveAsync(
            bool isCreateMode,
            TableDataRow savedRow,
            IReadOnlyDictionary<string, object?>? payload)
        {
            if (isCreateMode
                || payload is null
                || !_viewModel.ApplySavedRowUpdate(savedRow, payload))
            {
                await _viewModel.ReloadCurrentReferenceAsync();
            }
        }

        private static long? TryGetLongValue(TableDataRow row, string fieldKey)
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

    }
}

