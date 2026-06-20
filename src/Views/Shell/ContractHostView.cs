// Hosts the Contracts table and the shared contract detail footer without edit-dialog workflow.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;
using CbsContractsDesktopClient.Services.Workspace;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using CbsContractsDesktopClient.Views.Functional;
using CbsContractsDesktopClient.ViewModels.References;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed class ContractHostView : ComplexHostViewBase
    {
        private const int OziDepartmentId = 1;
        private const int CommersDepartmentId = 2;
        private const int FinDepartmentId = 3;
        private const string ContractModel = "Contract";
        private const string StageModel = "Stage";
        private const string ProfileModel = "Profile";
        private const string AddressModel = "Address";

        private static readonly IReadOnlySet<long> ContractStatusIds = new HashSet<long> { 0, 1, 5, 6, 7 };

        private readonly IDataQueryService _dataQueryService;
        private readonly IModelMutationService _modelMutationService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly IEmployeeEditWorkflow _employeeEditWorkflow;
        private readonly IContragentFnsWorkflow _contragentFnsWorkflow;
        private readonly IContragentLookupService _contragentLookupService;
        private readonly ILocalUserSettingsService _localUserSettingsService;
        private readonly IUserService _userService;
        private readonly ContractWorkflowStore _contractWorkflowStore;
        private readonly ContractTableRowDetailStrategy _rowDetailStrategy = new();
        private readonly ContractDetailView _detailView = new();
        private CancellationTokenSource? _detailCts;
        private bool _showContractCostFraction;
        private Button? _createButton;
        private Button? _editButton;
        private Button? _copyButton;
        private Button? _commentButton;
        private Button? _createEmployeeButton;
        private Button? _contragentMenuButton;
        private Button? _saveFiltersButton;
        private ToggleButton? _showCostFractionButton;
        private bool _isContragentWorkflowInProgress;

        public ContractHostView()
        {
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _modelMutationService = App.Services.GetRequiredService<IModelMutationService>();
            _referenceLookupCacheService = App.Services.GetRequiredService<IReferenceLookupCacheService>();
            _referenceDefinitionService = App.Services.GetRequiredService<IReferenceDefinitionService>();
            _employeeEditWorkflow = App.Services.GetRequiredService<IEmployeeEditWorkflow>();
            _contragentFnsWorkflow = App.Services.GetRequiredService<IContragentFnsWorkflow>();
            _contragentLookupService = App.Services.GetRequiredService<IContragentLookupService>();
            _localUserSettingsService = App.Services.GetRequiredService<ILocalUserSettingsService>();
            _userService = App.Services.GetRequiredService<IUserService>();
            _contractWorkflowStore = App.Services.GetRequiredService<ContractWorkflowStore>();
            _showContractCostFraction = _localUserSettingsService.Get().ShowContractCostFraction;
            SetDetailContent(_detailView);
            ClearDetailView();
        }

        protected override IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            _createButton = CreateHeaderIconButton("\uF8AA", "Добавить контракт");
            _createButton.Click += async (_, _) => await ShowContractCommerCreateDialogAsync();

            _editButton = CreateHeaderIconButton("\uE70F", "Редактировать контракт");
            _editButton.Click += async (_, _) => await ShowEditDialogForCurrentUserAsync();

            _copyButton = CreateHeaderIconButton("\uE8C8", "Скопировать контракт");
            _copyButton.Click += (_, _) => CopyContractInfo();

            _commentButton = CreateHeaderIconButton("\uE90A", "Добавить комментарий к контракту");
            _commentButton.Click += CommentContractButton_Click;

            _createEmployeeButton = CreateHeaderIconButton("\uE77B", "Добавить сотрудника");
            _createEmployeeButton.Click += async (_, _) => await CreateEmployeeForSelectedContractAsync();

            _contragentMenuButton = CreateHeaderIconButton("\uEC08", "Контрагент");
            _contragentMenuButton.Foreground = new SolidColorBrush(Microsoft.UI.Colors.SeaGreen);
            _contragentMenuButton.Flyout = CreateContragentMenuFlyout();

            _showCostFractionButton = CreateContractCostFractionButton();
            _showCostFractionButton.Click += async (_, _) => await ToggleContractCostFractionAsync();

            _saveFiltersButton = CreateHeaderIconButton("\uE74E", "Сохранить текущие фильтры контрактов");
            _saveFiltersButton.Click += async (_, _) => await SaveContractFiltersAsync();

            ApplyContractCostFractionMode();
            UpdateActionButtonState();
            return
            [
                _createButton,
                _editButton,
                _copyButton,
                _commentButton,
                _createEmployeeButton,
                _contragentMenuButton,
                _showCostFractionButton,
                _saveFiltersButton
            ];
        }

        protected override async Task OnRouteLoaded(TablePageDefinition definition)
        {
            await LoadContractOptionsSourcesAsync();
            ApplyContractCostFractionMode();
            UpdateActionButtonState();
            UpdateDetailView(Store.SelectedRow);
            _ = RefreshDetailAsync();
        }

        protected override Task OnRowSelected(TableDataRow? row)
        {
            UpdateActionButtonState();
            UpdateDetailView(row);
            _ = RefreshDetailAsync();
            return Task.CompletedTask;
        }

        protected override async Task OpenEditDialogAsync(TableDataRow? row)
        {
            if (row is not null)
            {
                await ShowEditDialogForCurrentUserAsync();
            }
        }

        protected override string BuildSelectedFooterText(TableDataRow row)
        {
            return _contractWorkflowStore.SelectedFooterText;
        }

        protected override async Task OnTableRowRefreshedAfterSaveAsync(TableDataRow freshRow)
        {
            UpdateDetailView(Store.SelectedRow);
            await RefreshDetailAsync();
        }

        protected override async Task OnTableReloadedAfterSaveAsync()
        {
            UpdateDetailView(Store.SelectedRow);
            await RefreshDetailAsync();
        }

        private async Task LoadContractOptionsSourcesAsync()
        {
            OptionsRegistry.Set("ContractStatus", await LoadContractStatusOptionsAsync());
            OptionsRegistry.Set("TaskKind", await LoadTaskKindCodeOptionsAsync());
            TableView.SetFilterOptionsSources(OptionsRegistry.Snapshot());
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadContractStatusOptionsAsync()
        {
            var statusOptions = await _contractWorkflowStore.GetAllStatusOptionsAsync(_referenceLookupCacheService);
            return statusOptions
                .Where(static option => JsonDataReader.TryGetLong(option.Value) is long id && ContractStatusIds.Contains(id))
                .OrderBy(static option => JsonDataReader.TryGetLong(option.Value))
                .ToList();
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadStageStatusOptionsAsync()
        {
            var statusOptions = await _contractWorkflowStore.GetAllStatusOptionsAsync(_referenceLookupCacheService);
            var optionsById = statusOptions
                .Where(static option => JsonDataReader.TryGetLong(option.Value) is not null)
                .GroupBy(static option => JsonDataReader.TryGetLong(option.Value)!.Value)
                .ToDictionary(static group => group.Key, static group => group.First());

            var result = new List<CbsTableFilterOptionDefinition>
            {
                new()
                {
                    Value = null,
                    Label = "Не определен"
                }
            };

            foreach (var statusId in StageContractStatusDialogControls.StageStatusIds.Order())
            {
                if (optionsById.TryGetValue(statusId, out var option))
                {
                    result.Add(option);
                }
            }

            return result;
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadTaskKindCodeOptionsAsync()
        {
            var items = await _referenceLookupCacheService.GetItemsAsync("TaskKind");
            return items
                .Where(static item => !string.IsNullOrWhiteSpace(item.Code))
                .Select(static item => new CbsTableFilterOptionDefinition
                {
                    Value = item.Code,
                    Label = FormatTaskKindOptionLabel(item.Code, item.DisplayName)
                })
                .DistinctBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static string FormatTaskKindOptionLabel(string? code, string? name)
        {
            var normalizedCode = string.IsNullOrWhiteSpace(code) ? "ХХ" : code.Trim();
            var normalizedName = name?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(normalizedName)
                ? normalizedCode
                : $"{normalizedCode} - {normalizedName}";
        }

        private void UpdateActionButtonState()
        {
            var hasSelectedRow = Store.SelectedRow is not null && !Store.SelectedRow.IsPlaceholder;
            var canCreateContract = Store.CanCreateRows && IsContractCreateAllowedForCurrentUser();

            ApplyCreateButtonState(_createButton, canCreateContract);
            ApplyEditButtonState(_editButton, hasSelectedRow && Store.CanEditRows);
            ApplyDefaultActionButtonState(_copyButton, hasSelectedRow);
            ApplyDefaultActionButtonState(_commentButton, hasSelectedRow && _userService.CurrentUser?.ProfileId is not null);
            ApplyCreateButtonState(_createEmployeeButton, hasSelectedRow);
            ApplyCreateButtonState(_contragentMenuButton, !_isContragentWorkflowInProgress);
            ApplyDefaultActionButtonState(_saveFiltersButton, Store.HasActiveReference);
        }

        private void UpdateDetailView(TableDataRow? row)
        {
            if (row is null || row.IsPlaceholder)
            {
                ClearDetailView();
                return;
            }

            _detailView.Visibility = Visibility.Visible;
            _detailView.ContractRow = row;
        }

        private async Task RefreshDetailAsync()
        {
            _detailCts?.Cancel();

            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                ClearDetailView();
                UpdateActionButtonState();
                return;
            }

            var selectedRow = Store.SelectedRow;
            _detailView.ContractRow = selectedRow;
            _detailView.ContragentRow = null;
            _contractWorkflowStore.ClearRowDetailSelection();
            RefreshSelectedFooterText();

            var contractId = _rowDetailStrategy.ResolveContractId(selectedRow);
            var listContragentId = _rowDetailStrategy.ResolveContragentId(selectedRow);
            if (contractId is null && listContragentId is null)
            {
                UpdateActionButtonState();
                return;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _detailCts = cancellationTokenSource;

            try
            {
                var contractTask = contractId is long selectedContractId
                    ? LoadRowDetailRowSafelyAsync(
                        () => LoadContractEditRowAsync(selectedContractId, cancellationTokenSource.Token),
                        cancellationTokenSource.Token)
                    : Task.FromResult<TableDataRow?>(null);
                var contragentTask = listContragentId is long selectedContragentId
                    ? LoadRowDetailRowSafelyAsync(
                        () => LoadContragentCardAsync(selectedContragentId, cancellationTokenSource.Token),
                        cancellationTokenSource.Token)
                    : Task.FromResult<TableDataRow?>(null);

                var contract = await contractTask;
                var contragent = await contragentTask;
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                if (Store.SelectedRow is null || !_rowDetailStrategy.IsSameSelection(Store.SelectedRow, contractId))
                {
                    return;
                }

                var contractContragentId = contract is null
                    ? null
                    : TryGetLongValue(contract, "contragent.id");
                if (contragent is null && contractContragentId is long loadedContragentId)
                {
                    contragent = await LoadRowDetailRowSafelyAsync(
                        () => LoadContragentCardAsync(loadedContragentId, cancellationTokenSource.Token),
                        cancellationTokenSource.Token);
                }

                _rowDetailStrategy.ApplySelection(_contractWorkflowStore, selectedRow, contract ?? selectedRow, contragent);
                _detailView.ContractRow = contract ?? selectedRow;
                _detailView.ContragentRow = contragent;
                RefreshSelectedFooterText();
                UpdateActionButtonState();
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    ClearDetailView();
                    UpdateActionButtonState();
                }
            }
        }

        private void ClearDetailView()
        {
            _detailCts?.Cancel();
            _detailView.RevisionRow = null;
            _detailView.ContractRow = null;
            _detailView.ContragentRow = null;
            _detailView.Visibility = Visibility.Collapsed;
            _contractWorkflowStore.ClearRowDetailSelection();
            RefreshSelectedFooterText();
        }

        private static async Task<TableDataRow?> LoadRowDetailRowSafelyAsync(
            Func<Task<TableDataRow?>> loadAsync,
            CancellationToken cancellationToken)
        {
            try
            {
                return await loadAsync();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }

        private async Task<TableDataRow?> LoadContractEditRowAsync(
            long contractId,
            CancellationToken cancellationToken = default)
        {
            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = ContractModel,
                    Preset = "edit",
                    Filters = new Dictionary<string, object?>
                    {
                        ["id__eq"] = contractId
                    },
                    Limit = 1
                },
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private async Task<TableDataRow?> LoadContragentCardAsync(
            long contragentId,
            CancellationToken cancellationToken = default)
        {
            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = "Contragent",
                    Preset = "card",
                    Filters = new Dictionary<string, object?>
                    {
                        ["id__eq"] = contragentId
                    },
                    Limit = 1
                },
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private void CopyContractInfo()
        {
            var text = _detailView.BuildClipboardText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
            ShowSuccessNotification("Контракт скопирован", text);
        }

        private void CommentContractButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement anchor)
            {
                return;
            }

            var flyout = new Flyout
            {
                Placement = FlyoutPlacementMode.Bottom
            };
            var commentBox = new TextBox
            {
                Width = 360,
                PlaceholderText = "Введите комментарий + Enter",
                AcceptsReturn = false
            };
            flyout.Content = commentBox;
            commentBox.KeyDown += async (_, args) =>
            {
                if (args.Key != VirtualKey.Enter)
                {
                    return;
                }

                args.Handled = true;
                await SaveContractCommentAsync(commentBox.Text, flyout);
            };
            flyout.Opened += (_, _) => commentBox.Focus(FocusState.Programmatic);
            flyout.ShowAt(anchor);
        }

        private async Task SaveContractCommentAsync(string? comment, Flyout flyout)
        {
            var normalizedComment = comment?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedComment))
            {
                return;
            }

            if (Store.SelectedRow is null || TryGetSelectedRowId(Store.SelectedRow) is not long contractId)
            {
                await ShowErrorDialogAsync(
                    "Комментарий к контракту",
                    "Не удалось определить выбранный контракт.");
                return;
            }

            if (_userService.CurrentUser?.ProfileId is not int profileId)
            {
                await ShowErrorDialogAsync(
                    "Комментарий к контракту",
                    "Не удалось определить profile_id пользователя.");
                return;
            }

            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = contractId,
                ["comments_attributes"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["content"] = normalizedComment,
                        ["profile_id"] = profileId
                    }
                }
            };
            var listKey = Store.SelectedRow.GetValue("list_key")?.ToString();
            if (!string.IsNullOrWhiteSpace(listKey))
            {
                payload["list_key"] = listKey;
            }

            try
            {
                var savedRow = await _modelMutationService.UpdateAsync(ContractModel, payload);
                flyout.Hide();
                ShowSuccessNotification(
                    "Комментарий сохранен",
                    "Комментарий к контракту добавлен.");
                await RefreshTableRowAfterSaveAsync(isCreateMode: false, savedRow);
                await RefreshDetailAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось сохранить комментарий", ex.Message);
            }
        }

        private async Task CreateEmployeeForSelectedContractAsync()
        {
            if (Store.SelectedRow is null)
            {
                return;
            }

            if (!_referenceDefinitionService.TryGetByRoute("/employees", out var employeeDefinition))
            {
                await ShowErrorDialogAsync(
                    "Не удалось создать сотрудника.",
                    "Справочник сотрудников не подключен.");
                return;
            }

            var contragentId =
                TryGetLongValue(Store.SelectedRow, "contragent.id")
                ?? TryGetLongValue(_contractWorkflowStore.Contragent, "id");
            var contragentName =
                JsonDataReader.TryGetText(Store.SelectedRow, "contragent.name")
                ?? JsonDataReader.TryGetText(_contractWorkflowStore.Contragent, "name", "requisites.organization.name");

            if (contragentId is null || string.IsNullOrWhiteSpace(contragentName))
            {
                await ShowErrorDialogAsync(
                    "Не удалось создать сотрудника.",
                    "В выбранном контракте отсутствует контрагент.");
                return;
            }

            var result = await _employeeEditWorkflow.ShowAsync(
                new EmployeeEditWorkflowRequest
                {
                    XamlRoot = XamlRoot,
                    IsCreateMode = true,
                    Definition = employeeDefinition,
                    InitialState = new EmployeeEditDialogState
                    {
                        Definition = employeeDefinition,
                        IsCreateMode = true,
                        ContragentId = contragentId,
                        ContragentName = contragentName,
                        IsUsed = true
                    }
                });

            if (result is null)
            {
                return;
            }

            await RefreshDetailAsync();
            ShowSuccessNotification(
                "Сотрудник создан",
                BuildReferenceNotificationMessage(result.Definition.Title, TryGetSelectedRowId(result.SavedRow)));
        }

        private async Task SaveContractFiltersAsync()
        {
            var user = _userService.CurrentUser;
            try
            {
                var settingsPayload = ContractTableFilterSettingsPayloadBuilder.Build(
                    user?.ProfileId,
                    user?.Statuses,
                    Store.CurrentFilters,
                    OptionsRegistry.Snapshot());

                if (!settingsPayload.HasChanges)
                {
                    await ShowInfoDialogAsync(
                        "Сохранение фильтров",
                        "Избранные фильтры не изменены.");
                    return;
                }

                if (!await ConfirmDialogAsync(
                        "Сохранение фильтров",
                        "Сохранить текущие фильтры контрактов как начальные установки фильтрации?",
                        "Сохранить"))
                {
                    return;
                }

                DiagnosticsFileLogger.AppendBlock(
                    "CONTRACT FILTER SETTINGS UPDATE REQUEST",
                    $"payload={JsonSerializer.Serialize(settingsPayload.Payload)}");
                await _modelMutationService.UpdateAsync(ProfileModel, settingsPayload.Payload);

                if (user is not null)
                {
                    user.Statuses = settingsPayload.StatusesJson;
                }

                Store.AppendUiTrace("CONTRACT FILTER SETTINGS SAVED");
                ShowSuccessNotification(
                    "Настройки сохранены",
                    "Начальные установки фильтрации контрактов обновлены.");
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось сохранить фильтры", ex.Message);
            }
        }

        private async Task ToggleContractCostFractionAsync()
        {
            _showContractCostFraction = _showCostFractionButton?.IsChecked == true;
            ApplyContractCostFractionMode();

            var settings = await _localUserSettingsService.GetAsync();
            settings.ShowContractCostFraction = _showContractCostFraction;
            await _localUserSettingsService.SaveAsync(settings);
        }

        private void ApplyContractCostFractionMode()
        {
            TableView.ShowStageCostFraction = _showContractCostFraction;
            if (_showCostFractionButton is not null)
            {
                _showCostFractionButton.IsChecked = _showContractCostFraction;
                _showCostFractionButton.Foreground = _showContractCostFraction
                    ? GetBrush("ShellPrimaryTextBrush")
                    : GetBrush("ShellSecondaryTextBrush");
            }
        }

        private async Task ShowContractCommerEditDialogAsync()
        {
            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                return;
            }

            var contract = _contractWorkflowStore.Contract;
            if (contract is null && TryGetSelectedRowId(Store.SelectedRow) is long contractId)
            {
                contract = await LoadContractEditRowAsync(contractId);
            }

            if (contract is null)
            {
                await ShowErrorDialogAsync(
                    "Редактирование контракта",
                    "Не удалось загрузить карточку контракта для редактирования.");
                return;
            }

            var allStatusOptions = await _contractWorkflowStore.GetAllStatusOptionsAsync(_referenceLookupCacheService);
            var taskKindItems = await _referenceLookupCacheService.GetItemsAsync("TaskKind");
            _contractWorkflowStore.BeginContractEdit(contract);
            var dialog = new ContractCommerEditDialog(
                _contractWorkflowStore,
                contract,
                OptionsRegistry.Get("TaskKind"),
                taskKindItems,
                OptionsRegistry.Get("ContractStatus"),
                allStatusOptions,
                _contragentLookupService.LoadOptionsAsync)
            {
                XamlRoot = XamlRoot
            };
            TableDataRow? savedRow = null;
            AttachContractCommerSaveHandler(dialog, isCreateMode: false, saved => savedRow = saved);
            await dialog.ShowAsync();
            if (!dialog.WasSaved || savedRow is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(ContractModel);
            await RefreshTableRowAfterSaveAsync(isCreateMode: false, savedRow);
            await RefreshDetailAsync();
            ShowSuccessNotification(
                "Контракт сохранен",
                "Изменения контракта сохранены.");
        }

        private async Task ShowEditDialogForCurrentUserAsync()
        {
            if (_userService.CurrentUser?.DepartmentId == OziDepartmentId)
            {
                await ShowContractOziStageEditDialogAsync();
                return;
            }

            if (_userService.CurrentUser?.DepartmentId == FinDepartmentId)
            {
                await ShowContractFinStageEditDialogAsync();
                return;
            }

            await ShowContractCommerEditDialogAsync();
        }

        private async Task ShowContractOziStageEditDialogAsync()
        {
            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                return;
            }

            var contract = _contractWorkflowStore.Contract;
            if (contract is null && TryGetSelectedRowId(Store.SelectedRow) is long contractId)
            {
                contract = await LoadContractEditRowAsync(contractId);
            }

            if (contract is null)
            {
                await ShowErrorDialogAsync(
                    "Редактирование этапа",
                    "Не удалось загрузить карточку контракта для редактирования этапа.");
                return;
            }

            try
            {
                _contractWorkflowStore.BeginContractEdit(contract, _contractWorkflowStore.Contragent);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось открыть этап.", ex.Message);
                return;
            }

            var selectedStageEditState = _contractWorkflowStore.SelectedStageEditState;
            if (_contractWorkflowStore.SelectedStage is null || selectedStageEditState is null)
            {
                await ShowErrorDialogAsync(
                    "Редактирование этапа",
                    "В выбранном контракте нет этапа для редактирования.");
                return;
            }

            var statusOptions = await LoadStageStatusOptionsAsync();
            var employeeItems = await LoadOziEmployeeItemsAsync();
            StageOziEditDialog dialog;
            try
            {
                dialog = new StageOziEditDialog(
                    selectedStageEditState,
                    _contractWorkflowStore.SelectedContractEditState,
                    statusOptions,
                    employeeItems,
                    _userService.CurrentUser?.ProfileId,
                    BuildStageNavigationState(selectedStageEditState),
                    NavigateStageEditDialogAsync)
                {
                    XamlRoot = XamlRoot
                };
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось открыть этап.", ex.Message);
                return;
            }

            TableDataRow? savedStageRow = null;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    var stagePayload = dialog.BuildPayload();
                    if (!HasUpdatePayloadChanges(stagePayload))
                    {
                        dialog.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    savedStageRow = await _modelMutationService.UpdateAsync(StageModel, stagePayload);

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
            if (!dialog.WasSaved || savedStageRow is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(StageModel);
            _referenceLookupCacheService.Invalidate(ContractModel);
            await RefreshSelectedContractAfterStageSaveAsync();
            ShowSuccessNotification(
                "Этап сохранен",
                BuildReferenceNotificationMessage("Этап", TryGetSelectedRowId(savedStageRow)));
        }

        private async Task ShowContractFinStageEditDialogAsync()
        {
            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                return;
            }

            var contract = _contractWorkflowStore.Contract;
            if (contract is null && TryGetSelectedRowId(Store.SelectedRow) is long contractId)
            {
                contract = await LoadContractEditRowAsync(contractId);
            }

            if (contract is null)
            {
                await ShowErrorDialogAsync(
                    "Редактирование этапа",
                    "Не удалось загрузить карточку контракта для редактирования этапа.");
                return;
            }

            try
            {
                _contractWorkflowStore.BeginContractEdit(contract, _contractWorkflowStore.Contragent);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось открыть этап.", ex.Message);
                return;
            }

            var selectedStageEditState = _contractWorkflowStore.SelectedStageEditState;
            if (_contractWorkflowStore.SelectedStage is null || selectedStageEditState is null)
            {
                await ShowErrorDialogAsync(
                    "Редактирование этапа",
                    "В выбранном контракте нет этапа для редактирования.");
                return;
            }

            var statusOptions = await LoadStageStatusOptionsAsync();
            StageFinEditDialog dialog;
            try
            {
                dialog = new StageFinEditDialog(
                    selectedStageEditState,
                    _contractWorkflowStore.SelectedContractEditState,
                    statusOptions,
                    _userService.CurrentUser?.ProfileId,
                    BuildStageNavigationState(selectedStageEditState),
                    NavigateStageEditDialogAsync)
                {
                    XamlRoot = XamlRoot
                };
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось открыть этап.", ex.Message);
                return;
            }

            TableDataRow? savedStageRow = null;
            var hasSavedContract = false;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    var stagePayload = dialog.BuildPayload();
                    var hasStageChanges = HasUpdatePayloadChanges(stagePayload);
                    var hasContractChanges = dialog.HasContractExternalNumberChanges();
                    if (!hasStageChanges && !hasContractChanges)
                    {
                        dialog.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    if (hasStageChanges)
                    {
                        savedStageRow = await _modelMutationService.UpdateAsync(StageModel, stagePayload);
                    }

                    if (hasContractChanges)
                    {
                        await _modelMutationService.UpdateAsync(
                            ContractModel,
                            dialog.BuildContractExternalNumberPayload());
                        hasSavedContract = true;
                    }
                }
                catch (Exception ex)
                {
                    dialog.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };

            await dialog.ShowAsync();
            if (!dialog.WasSaved || (savedStageRow is null && !hasSavedContract))
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(StageModel);
            _referenceLookupCacheService.Invalidate(ContractModel);
            await RefreshSelectedContractAfterStageSaveAsync();
            ShowSuccessNotification(
                "Этап сохранен",
                BuildReferenceNotificationMessage("Этап", savedStageRow is null ? selectedStageEditState.Id : TryGetSelectedRowId(savedStageRow)));
        }

        private async Task ShowContractCommerCreateDialogAsync()
        {
            if (!IsContractCreateAllowedForCurrentUser())
            {
                await ShowErrorDialogAsync(
                    "Создание контракта",
                    "Создание контрактов доступно коммерческому отделу и администраторам.");
                return;
            }

            var allStatusOptions = await _contractWorkflowStore.GetAllStatusOptionsAsync(_referenceLookupCacheService);
            var taskKindItems = await _referenceLookupCacheService.GetItemsAsync("TaskKind");
            var contract = BuildNewContractRow();
            _contractWorkflowStore.BeginContractEdit(contract);
            var dialog = new ContractCommerEditDialog(
                _contractWorkflowStore,
                contract,
                OptionsRegistry.Get("TaskKind"),
                taskKindItems,
                OptionsRegistry.Get("ContractStatus"),
                allStatusOptions,
                _contragentLookupService.LoadOptionsAsync,
                isCreateMode: true)
            {
                XamlRoot = XamlRoot
            };
            TableDataRow? savedRow = null;
            AttachContractCommerSaveHandler(dialog, isCreateMode: true, saved => savedRow = saved);
            await dialog.ShowAsync();
            if (!dialog.WasSaved || savedRow is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(ContractModel);
            await RefreshTableRowAfterSaveAsync(isCreateMode: true, savedRow);
            ShowSuccessNotification(
                "Контракт создан",
                "Новый контракт сохранен.");
        }

        private void AttachContractCommerSaveHandler(
            ContractCommerEditDialog dialog,
            bool isCreateMode,
            Action<TableDataRow> setSavedRow)
        {
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    var payload = dialog.BuildPayload(_userService.CurrentUser?.ProfileId);
                    if (!isCreateMode && !HasUpdatePayloadChanges(payload))
                    {
                        dialog.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    var savedRow = isCreateMode
                        ? await _modelMutationService.CreateAsync(ContractModel, payload)
                        : await _modelMutationService.UpdateAsync(ContractModel, payload);
                    setSavedRow(savedRow);
                }
                catch (Exception ex)
                {
                    dialog.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };
        }

        private bool IsContractCreateAllowedForCurrentUser()
        {
            var user = _userService.CurrentUser;
            return user?.DepartmentId == CommersDepartmentId
                || UserHasRole(user?.Role, "admin");
        }

        private static TableDataRow BuildNewContractRow()
        {
            return new TableDataRow
            {
                Values = new Dictionary<string, JsonElement>
                {
                    ["year"] = JsonSerializer.SerializeToElement(DateTime.Now.Year)
                }
            };
        }

        private static bool UserHasRole(string? roleCsv, string role)
        {
            return !string.IsNullOrWhiteSpace(roleCsv)
                && roleCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(item => string.Equals(item, role, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasUpdatePayloadChanges(IReadOnlyDictionary<string, object?> payload)
        {
            return payload.Keys.Any(static key =>
                !string.Equals(key, "id", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "list_key", StringComparison.OrdinalIgnoreCase));
        }

        private async Task RefreshSelectedContractAfterStageSaveAsync(CancellationToken cancellationToken = default)
        {
            if (Store.SelectedRow is null || TryGetSelectedRowId(Store.SelectedRow) is not long contractId)
            {
                await RefreshDetailAsync();
                return;
            }

            await RefreshTableRowByIdAsync(contractId, cancellationToken);
            await RefreshDetailAsync();
        }

        private StageEditDialogNavigationState BuildStageNavigationState(StageEditState stage)
        {
            var stages = _contractWorkflowStore.GetVisibleStageEditStates();
            var index = FindStageIndex(stages, stage);
            return new StageEditDialogNavigationState(
                CanPrevious: index > 0,
                CanNext: index >= 0 && index < stages.Count - 1);
        }

        private Task<StageEditDialogNavigationResult?> NavigateStageEditDialogAsync(
            StageEditDialogNavigationDirection direction)
        {
            if (direction == StageEditDialogNavigationDirection.None)
            {
                return Task.FromResult<StageEditDialogNavigationResult?>(null);
            }

            var offset = direction == StageEditDialogNavigationDirection.Previous ? -1 : 1;
            if (!_contractWorkflowStore.TrySelectAdjacentStageEditState(offset))
            {
                return Task.FromResult<StageEditDialogNavigationResult?>(null);
            }

            var stage = _contractWorkflowStore.SelectedStageEditState;
            if (stage is null)
            {
                return Task.FromResult<StageEditDialogNavigationResult?>(null);
            }

            return Task.FromResult<StageEditDialogNavigationResult?>(new StageEditDialogNavigationResult(
                stage,
                _contractWorkflowStore.SelectedContractEditState,
                BuildStageNavigationState(stage)));
        }

        private static int FindStageIndex(IReadOnlyList<StageEditState> stages, StageEditState selectedStage)
        {
            for (var index = 0; index < stages.Count; index++)
            {
                var stage = stages[index];
                if ((stage.Id > 0 && stage.Id == selectedStage.Id)
                    || (!string.IsNullOrWhiteSpace(stage.ListKey)
                        && string.Equals(stage.ListKey, selectedStage.ListKey, StringComparison.Ordinal)))
                {
                    return index;
                }
            }

            return -1;
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
                    Name = JsonDataReader.GetText(row, "name", "person.name", "full_name"),
                    FullName = JsonDataReader.GetText(row, "full_name", "name", "person.name"),
                    Code = JsonDataReader.GetText(row, "code"),
                    Row = row
                })
                .Where(static item => item.Id is not null && !string.IsNullOrWhiteSpace(item.DisplayName))
                .ToList();
        }

        private MenuFlyout CreateContragentMenuFlyout()
        {
            var flyout = new MenuFlyout();

            var createItem = new MenuFlyoutItem { Text = "Добавить" };
            createItem.Click += async (_, _) => await RunContragentToolbarWorkflowAsync(ShowCreateContragentDialogAsync);
            flyout.Items.Add(createItem);

            var importItem = new MenuFlyoutItem { Text = "Импорт с ФНС" };
            importItem.Click += async (_, _) => await RunContragentToolbarWorkflowAsync(ImportContragentFromFnsAsync);
            flyout.Items.Add(importItem);

            return flyout;
        }

        private async Task RunContragentToolbarWorkflowAsync(Func<Task<CbsTableFilterOptionDefinition?>> workflow)
        {
            _isContragentWorkflowInProgress = true;
            UpdateActionButtonState();

            try
            {
                await workflow();
            }
            finally
            {
                _isContragentWorkflowInProgress = false;
                UpdateActionButtonState();
            }
        }

        private async Task<CbsTableFilterOptionDefinition?> ImportContragentFromFnsAsync()
        {
            if (!_referenceDefinitionService.TryGetByRoute("/contragents", out var reference))
            {
                await ShowErrorDialogAsync(
                    "Не удалось импортировать контрагента.",
                    "Справочник контрагентов не подключен.");
                return null;
            }

            var result = await _contragentFnsWorkflow.ImportAsync(
                new ContragentFnsWorkflowRequest
                {
                    XamlRoot = XamlRoot,
                    Definition = reference
                });

            if (result is null)
            {
                return null;
            }

            _referenceLookupCacheService.Invalidate(reference.Model);
            ShowSuccessNotification(
                result.SuccessTitle,
                BuildReferenceNotificationMessage(result.Definition.Title, TryGetSelectedRowId(result.SavedRow)));
            return BuildContragentOption(result.SavedRow);
        }

        private async Task<CbsTableFilterOptionDefinition?> ShowCreateContragentDialogAsync()
        {
            if (!_referenceDefinitionService.TryGetByRoute("/contragents", out var reference))
            {
                await ShowErrorDialogAsync(
                    "Не удалось создать контрагента.",
                    "Справочник контрагентов не подключен.");
                return null;
            }

            var ownershipOptions = await LoadSimpleReferenceOptionsAsync("Ownership", "card");
            var regionOptions = await LoadSimpleReferenceOptionsAsync("Area", "item");
            var state = ContragentEditStateFactory.Create(
                reference,
                isCreateMode: true,
                sourceRow: null,
                ownershipOptions,
                regionOptions);
            var viewModel = new ContragentEditViewModel(state, LoadAddressOptionsAsync);
            var dialog = new ContragentEditDialog(viewModel)
            {
                XamlRoot = XamlRoot
            };

            TableDataRow? savedRow = null;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    viewModel.ClearErrorInfo();
                    await EnsureContragentAddressAsync(viewModel);
                    var payload = ContragentEditPayloadBuilder.BuildForCreate(viewModel);
                    savedRow = await _modelMutationService.CreateAsync(reference.Model, payload);
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
                return null;
            }

            _referenceLookupCacheService.Invalidate(reference.Model);
            ShowSuccessNotification(
                "Контрагент создан",
                BuildReferenceNotificationMessage(reference.Title, TryGetSelectedRowId(savedRow)));

            return BuildContragentOption(savedRow);
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

            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = AddressModel,
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
                    Model = AddressModel,
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
                .Select(static option => option!)
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

        private static CbsTableFilterOptionDefinition BuildContragentOption(TableDataRow row)
        {
            var id = TryGetSelectedRowId(row);
            if (id is null)
            {
                throw new InvalidOperationException("Created contragent row must contain id.");
            }

            var label =
                JsonDataReader.TryGetText(row, "full_name")
                ?? JsonDataReader.TryGetText(row, "name")
                ?? JsonDataReader.TryGetText(row, "requisites.organization.full_name")
                ?? JsonDataReader.TryGetText(row, "requisites.organization.name");
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new InvalidOperationException("Created contragent row must contain name.");
            }

            return new CbsTableFilterOptionDefinition
            {
                Value = id.Value,
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

        private static ToggleButton CreateContractCostFractionButton()
        {
            var size = (double)Application.Current.Resources["ShellActionButtonSize"];
            var button = new ToggleButton
            {
                Width = size,
                Height = size,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = null,
                Content = "%",
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            ToolTipService.SetToolTip(button, "Показывать дробную часть суммы контракта");
            return button;
        }

        private static string BuildReferenceNotificationMessage(string referenceTitle, long? id)
        {
            return id.HasValue
                ? $"{referenceTitle}, ID {id.Value}"
                : referenceTitle;
        }

        private static long? TryGetLongValue(TableDataRow? row, string fieldKey)
        {
            return row is null ? null : JsonDataReader.TryGetLong(row.GetValue(fieldKey));
        }

        private static Brush? GetBrush(string resourceKey)
        {
            return Application.Current.Resources[resourceKey] as Brush;
        }
    }
}
