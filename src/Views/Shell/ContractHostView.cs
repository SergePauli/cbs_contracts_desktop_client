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
using CbsContractsDesktopClient.Services.Shell;
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
        private readonly ContractWorkflowFactory _contractWorkflowFactory;
        private readonly ContractCommentWorkflow _contractCommentWorkflow;
        private readonly ContractCommerSaveWorkflow _contractCommerSaveWorkflow;
        private readonly ContractTableRowDetailStrategy _rowDetailStrategy = new();
        private readonly ContractDetailView _detailView = new();
        private ContractWorkflowContext? _appliedContractWorkflowContext;
        private bool _showContractCostFraction;
        private Button? _createButton;
        private Button? _editButton;
        private Button? _infoButton;
        private Button? _copyContractDataButton;
        private Button? _copyCellSelectionButton;
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
            _contractWorkflowFactory = App.Services.GetRequiredService<ContractWorkflowFactory>();
            _contractCommentWorkflow = App.Services.GetRequiredService<ContractCommentWorkflow>();
            _contractCommerSaveWorkflow = App.Services.GetRequiredService<ContractCommerSaveWorkflow>();
            _showContractCostFraction = _localUserSettingsService.Get().ShowContractCostFraction;
            _detailView.EmployeeEditRequested += DetailView_EmployeeEditRequested;
            SetDetailContent(_detailView, isVisible: false);
        }

        protected override IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            _createButton = CreateHeaderIconButton("\uF8AA", "Добавить контракт");
            _createButton.Click += async (_, _) => await ShowContractCommerCreateDialogAsync();

            _editButton = CreateHeaderIconButton("\uE70F", "Редактировать контракт");
            _editButton.Click += async (_, _) => await ShowEditDialogForCurrentUserAsync();

            _infoButton = CreateHeaderIconButton("\uE946", "Информация о контракте");
            _infoButton.Click += async (_, _) => await ShowContractInfoDialogAsync();

            _copyContractDataButton = CreateHeaderIconButton("\uE8F3", "Скопировать данные выбранного контракта в буфер");
            _copyContractDataButton.Click += (_, _) => CopyContractInfo();

            _copyCellSelectionButton = CreateHeaderIconButton("\uE8C8", "Скопировать выделенный диапазон");
            _copyCellSelectionButton.Click += (_, _) => TableView.CopySelectedCellRangeToClipboard();

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
                _infoButton,
                _copyContractDataButton,
                _copyCellSelectionButton,
                _commentButton,
                _createEmployeeButton,
                _contragentMenuButton,
                _showCostFractionButton,
                _saveFiltersButton
            ];
        }

        protected override int PrimaryHeaderActionCount => 3;

        protected override async Task OnRouteLoaded(TablePageDefinition definition)
        {
            await LoadContractOptionsSourcesAsync();
            ApplyContractCostFractionMode();
            UpdateActionButtonState();
            if (Store.SelectedRow is not null && !Store.SelectedRow.IsPlaceholder)
            {
                UpdateDetailView(Store.SelectedRow);
                _ = RefreshDetailAsync();
            }
        }

        protected override Task OnRowSelected(TableDataRow? row)
        {
            UpdateActionButtonState();
            if (row is null || row.IsPlaceholder)
            {
                ClearDetailView();
                return Task.CompletedTask;
            }

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
            ApplyDefaultActionButtonState(_infoButton, HasContractInfoSelection());
            ApplyDefaultActionButtonState(_copyContractDataButton, hasSelectedRow);
            ApplyDefaultActionButtonState(_copyCellSelectionButton, Store.HasActiveReference);
            ApplyDefaultActionButtonState(_commentButton, hasSelectedRow && _userService.CurrentUser?.ProfileId is not null);
            ApplyCreateButtonState(_createEmployeeButton, hasSelectedRow);
            ApplyCreateButtonState(_contragentMenuButton, !_isContragentWorkflowInProgress);
            ApplyDefaultActionButtonState(_saveFiltersButton, Store.HasActiveReference);
        }

        private bool HasContractInfoSelection()
        {
            return Store.SelectedRow is { IsPlaceholder: false } row
                && TryGetSelectedRowId(row) == _contractWorkflowStore.SelectedContractEditState?.Id
                && _contractWorkflowStore.SelectedStageEditState is not null;
        }

        private void UpdateDetailView(TableDataRow? row)
        {
            if (row is null || row.IsPlaceholder)
            {
                Store.AppendUiTrace($"CONTRACT DETAIL UPDATE empty row={DescribeDetailRow(row)}");
                return;
            }

            Store.AppendUiTrace($"CONTRACT DETAIL UPDATE row={DescribeDetailRow(row)}");
            SetDetailContentVisible(true);
            _detailView.Visibility = Visibility.Visible;
        }

        private async Task RefreshDetailAsync()
        {
            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                Store.AppendUiTrace($"CONTRACT DETAIL REFRESH empty selected={DescribeDetailRow(Store.SelectedRow)}");
                UpdateActionButtonState();
                return;
            }

            var selectedRow = Store.SelectedRow;
            Store.AppendUiTrace($"CONTRACT DETAIL REFRESH start selected={DescribeDetailRow(selectedRow)}");

            try
            {
                var context = await _contractWorkflowFactory.CreateFromContractRowAsync(selectedRow);

                if (!ApplyContractWorkflowContextIfCurrent(context))
                {
                    Store.AppendUiTrace(
                        $"CONTRACT DETAIL REFRESH stale selected={DescribeDetailRow(selectedRow)} current={DescribeDetailRow(Store.SelectedRow)}");
                    return;
                }

                Store.AppendUiTrace(
                    $"CONTRACT DETAIL REFRESH applied selected={DescribeDetailRow(selectedRow)} contract={DescribeDetailRow(context.Contract)} contragent={DescribeDetailRow(context.Contragent)}");
            }
            catch (OperationCanceledException)
            {
                Store.AppendUiTrace($"CONTRACT DETAIL REFRESH canceled-exception selected={DescribeDetailRow(selectedRow)}");
            }
            catch
            {
                Store.AppendUiTrace($"CONTRACT DETAIL REFRESH failed selected={DescribeDetailRow(selectedRow)}");
                UpdateActionButtonState();
            }
        }

        private bool ApplyContractWorkflowContextIfCurrent(ContractWorkflowContext context)
        {
            if (!_contractWorkflowFactory.IsLatest(context)
                || !context.Matches(Store.SelectedRow))
            {
                return false;
            }

            if (_appliedContractWorkflowContext?.LoadVersion == context.LoadVersion)
            {
                return true;
            }

            context.ApplyTo(_contractWorkflowStore, _rowDetailStrategy);
            _appliedContractWorkflowContext = context;
            RefreshSelectedFooterText();
            UpdateActionButtonState();
            return true;
        }

        private async Task<ContractWorkflowContext?> EnsureContractWorkflowContextForDialogAsync(string title)
        {
            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                return null;
            }

            try
            {
                if (_appliedContractWorkflowContext is { } appliedContext
                    && _contractWorkflowFactory.IsLatest(appliedContext)
                    && appliedContext.Matches(Store.SelectedRow)
                    && HasContractInfoSelection())
                {
                    return appliedContext;
                }

                var context = await _contractWorkflowFactory.CreateFromContractRowAsync(Store.SelectedRow);
                return ApplyContractWorkflowContextIfCurrent(context)
                    ? context
                    : null;
            }
            catch (Exception ex)
            {
                Store.AppendUiTrace($"CONTRACT WORKFLOW CONTEXT FAILED title={title} exception={ex}");
                await ShowErrorDialogAsync(title, ex.Message);
                return null;
            }
        }

        private void ClearDetailView()
        {
            Store.AppendUiTrace("CONTRACT DETAIL CLEAR");
            _contractWorkflowFactory.CancelCurrentLoad();
            _appliedContractWorkflowContext = null;
            _detailView.Visibility = Visibility.Collapsed;
            SetDetailContentVisible(false);
            _contractWorkflowStore.ClearRowDetailSelection();
            RefreshSelectedFooterText();
        }

        private static string DescribeDetailRow(TableDataRow? row)
        {
            if (row is null)
            {
                return "<null>";
            }

            if (row.IsPlaceholder)
            {
                return "<placeholder>";
            }

            return TryGetSelectedRowId(row)?.ToString() ?? "<no-id>";
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

            var listKey = Store.SelectedRow.GetValue("list_key")?.ToString();

            try
            {
                await _contractCommentWorkflow.SaveContractCommentAsync(
                    contractId,
                    listKey,
                    normalizedComment);
                flyout.Hide();
                ShowSuccessNotification(
                    "Комментарий сохранен",
                    "Комментарий к контракту добавлен.");
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

        private async void DetailView_EmployeeEditRequested(object? sender, EmployeeBoxEditRequestedEventArgs e)
        {
            await EditEmployeeFromDetailAsync(e.Employee);
        }

        private async Task EditEmployeeFromDetailAsync(EmployeeBoxItem employee)
        {
            if (employee.Id is not long employeeId)
            {
                return;
            }

            try
            {
                var result = await _employeeEditWorkflow.ShowAsync(
                    new EmployeeEditWorkflowRequest
                    {
                        XamlRoot = XamlRoot,
                        IsCreateMode = false,
                        EmployeeId = employeeId
                    });

                if (result is null)
                {
                    return;
                }

                await RefreshDetailAsync();
                ShowSuccessNotification(
                    "Изменения сотрудника сохранены",
                    BuildReferenceNotificationMessage(result.Definition.Title, TryGetSelectedRowId(result.SavedRow)));
            }
            catch (InvalidOperationException ex)
            {
                await ShowErrorDialogAsync("Не удалось открыть сотрудника.", ex.Message);
            }
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

            var context = await EnsureContractWorkflowContextForDialogAsync("Редактирование контракта");
            if (context is null)
            {
                return;
            }

            var contract = context.Contract;
            ContractCommerEditDialog dialog;
            try
            {
                var allStatusOptions = await _contractWorkflowStore.GetAllStatusOptionsAsync(_referenceLookupCacheService);
                var taskKindItems = await _referenceLookupCacheService.GetItemsAsync("TaskKind");
                if (!string.IsNullOrWhiteSpace(_contractWorkflowStore.ContractEditGraphRepairMessage))
                {
                    Store.AppendUiTrace($"CONTRACT EDIT GRAPH REPAIRED contract={TryGetSelectedRowId(contract)} message={_contractWorkflowStore.ContractEditGraphRepairMessage}");
                }

                dialog = new ContractCommerEditDialog(
                    _contractWorkflowStore,
                    contract,
                    OptionsRegistry.Get("TaskKind"),
                    taskKindItems,
                    OptionsRegistry.Get("ContractStatus"),
                    allStatusOptions,
                    _contragentLookupService.LoadOptionsAsync,
                    _contractWorkflowFactory.LoadContragentCardRowAsync)
                {
                    XamlRoot = XamlRoot
                };
            }
            catch (Exception ex)
            {
                Store.AppendUiTrace($"CONTRACT EDIT OPEN FAILED contract={TryGetSelectedRowId(contract)} exception={ex}");
                await ShowErrorDialogAsync("Редактирование контракта", ex.Message);
                return;
            }

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

            if (IsContractCommerEditAllowedForCurrentUser())
            {
                await ShowContractCommerEditDialogAsync();
                return;
            }

            await ShowContractInfoDialogAsync();
        }

        private async Task ShowContractInfoDialogAsync()
        {
            if (await EnsureContractWorkflowContextForDialogAsync("Информация о контракте") is null)
            {
                return;
            }

            var stage = _contractWorkflowStore.SelectedStageEditState
                ?? throw new InvalidOperationException("ContractHostView.ShowContractInfoDialogAsync: SelectedStageEditState is not set.");
            var contract = _contractWorkflowStore.SelectedContractEditState
                ?? throw new InvalidOperationException("ContractHostView.ShowContractInfoDialogAsync: SelectedContractEditState is not set.");
            var auditSummary = await ContractInfoAuditLoader.LoadAsync(
                _dataQueryService,
                contract.Id,
                contract.Status.Id == WorkflowStatusIds.Closed);

            var dialog = new ContractInfoDialog(
                stage,
                contract,
                _contractWorkflowStore.GetContractDocumentRevisionEditState(),
                auditSummary,
                BuildStageNavigationState(stage),
                NavigateStageEditDialogAsync)
            {
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }

        private async Task ShowContractOziStageEditDialogAsync()
        {
            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                return;
            }

            if (await EnsureContractWorkflowContextForDialogAsync("Редактирование этапа") is null)
            {
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
                    NavigateStageEditDialogAsync,
                    _contractWorkflowStore.ShouldCloseContractAfterSelectedStageClosed)
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

                    var shouldCloseContract = dialog.ShouldCloseContract();
                    Store.AppendUiTrace($"CONTRACT CLOSE CHECK {_contractWorkflowStore.BuildContractCloseDecisionTrace(shouldCloseContract)}");
                    if (shouldCloseContract)
                    {
                        var contractPayload = dialog.BuildContractClosePayload();
                        await _modelMutationService.UpdateAsync(
                            ContractModel,
                            contractPayload);
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

            if (await EnsureContractWorkflowContextForDialogAsync("Редактирование этапа") is null)
            {
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
                _contractWorkflowFactory.LoadContragentCardRowAsync,
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
        }

        private void AttachContractCommerSaveHandler(
            ContractCommerEditDialog dialog,
            bool isCreateMode,
            Action<TableDataRow> setSavedRow)
        {
            var createMode = isCreateMode;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    var savedAsCreate = createMode;
                    var savePlan = dialog.BuildSavePlan(_userService.CurrentUser?.ProfileId);
                    if (!savePlan.HasChanges)
                    {
                        dialog.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    var saveResult = await _contractCommerSaveWorkflow.SaveAsync(savePlan);
                    var savedId = saveResult.ContractId;
                    if (createMode)
                    {
                        dialog.AcceptCreatedContractIdentity(
                            savedId,
                            saveResult.ContractMutationRow?.GetValue("list_key")?.ToString());
                        createMode = false;
                    }

                    ShowSuccessNotification(
                        savedAsCreate ? "Контракт создан" : "Контракт сохранен",
                        savedAsCreate ? "Новый контракт сохранен." : "Изменения контракта сохранены.");
                    var editRow = await _contractWorkflowFactory.ReloadContractEditRowAsync(savedId);
                    dialog.ReloadAsEdit(editRow);
                    setSavedRow(editRow);
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
            return IsContractCommerEditAllowedForCurrentUser();
        }

        private bool IsContractCommerEditAllowedForCurrentUser()
        {
            var user = _userService.CurrentUser;
            return user?.DepartmentId == CommersDepartmentId;
        }

        private static TableDataRow BuildNewContractRow()
        {
            return new TableDataRow
            {
                Values = new Dictionary<string, JsonElement>
                {
                    ["year"] = JsonSerializer.SerializeToElement(DateTime.Now.Year),
                    ["contract_responsibles"] = JsonSerializer.SerializeToElement(Array.Empty<object>())
                }
            };
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
