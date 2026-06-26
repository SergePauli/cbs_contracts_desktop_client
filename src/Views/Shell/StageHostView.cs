// Hosts the Stages table, department-specific stage dialogs, and contract detail footer.
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
using CbsContractsDesktopClient.Stores.Table;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using CbsContractsDesktopClient.Views.Functional;
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
    public sealed class StageHostView : ComplexHostViewBase
    {
        private const int OziDepartmentId = 1;
        private const int CommersDepartmentId = 2;
        private const int FinDepartmentId = 3;
        private const string ContractModel = "Contract";
        private const string ProfileModel = "Profile";

        private readonly IDataQueryService _dataQueryService;
        private readonly IModelMutationService _modelMutationService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly IEmployeeEditWorkflow _employeeEditWorkflow;
        private readonly IContragentLookupService _contragentLookupService;
        private readonly ILocalUserSettingsService _localUserSettingsService;
        private readonly IUserService _userService;
        private readonly ContractWorkflowStore _contractWorkflowStore;
        private readonly ContractWorkflowFactory _contractWorkflowFactory;
        private readonly StageRowDetailStrategy _rowDetailStrategy = new();
        private readonly ContractDetailView _detailView = new();
        private CancellationTokenSource? _detailCts;
        private bool _showStageCostFraction;
        private Button? _editButton;
        private Button? _infoButton;
        private Button? _copyButton;
        private Button? _commentButton;
        private Button? _createEmployeeButton;
        private Button? _saveFiltersButton;
        private ToggleButton? _showCostFractionButton;

        public StageHostView()
        {
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _modelMutationService = App.Services.GetRequiredService<IModelMutationService>();
            _referenceLookupCacheService = App.Services.GetRequiredService<IReferenceLookupCacheService>();
            _referenceDefinitionService = App.Services.GetRequiredService<IReferenceDefinitionService>();
            _employeeEditWorkflow = App.Services.GetRequiredService<IEmployeeEditWorkflow>();
            _contragentLookupService = App.Services.GetRequiredService<IContragentLookupService>();
            _localUserSettingsService = App.Services.GetRequiredService<ILocalUserSettingsService>();
            _userService = App.Services.GetRequiredService<IUserService>();
            _contractWorkflowStore = App.Services.GetRequiredService<ContractWorkflowStore>();
            _contractWorkflowFactory = App.Services.GetRequiredService<ContractWorkflowFactory>();
            _showStageCostFraction = _localUserSettingsService.Get().ShowStageCostFraction;
            _detailView.EmployeeEditRequested += DetailView_EmployeeEditRequested;
            SetDetailContent(_detailView, isVisible: false);
        }

        protected override IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            _editButton = CreateHeaderIconButton("\uE70F", "Редактировать этап");
            _editButton.Click += async (_, _) => await ShowStageEditDialogAsync();

            _infoButton = CreateHeaderIconButton("\uE946", "Информация о контракте");
            _infoButton.Click += async (_, _) => await ShowContractInfoDialogAsync();

            _copyButton = CreateHeaderIconButton("\uE8C8", "Скопировать этап");
            _copyButton.Click += (_, _) => CopyStageInfo();

            _commentButton = CreateHeaderIconButton("\uE90A", "Добавить комментарий к этапу");
            _commentButton.Click += CommentStageButton_Click;

            _createEmployeeButton = CreateHeaderIconButton("\uE77B", "Добавить сотрудника");
            _createEmployeeButton.Click += async (_, _) => await CreateEmployeeForSelectedStageAsync();

            _showCostFractionButton = CreateStageCostFractionButton();
            _showCostFractionButton.Click += async (_, _) => await ToggleStageCostFractionAsync();

            _saveFiltersButton = CreateHeaderIconButton("\uE74E", "Сохранить текущие фильтры этапов");
            _saveFiltersButton.Click += async (_, _) => await SaveStageFiltersAsync();

            ApplyStageCostFractionMode();
            UpdateActionButtonState();
            return
            [
                _editButton,
                _infoButton,
                _copyButton,
                _commentButton,
                _createEmployeeButton,
                _showCostFractionButton,
                _saveFiltersButton
            ];
        }

        protected override async Task OnRouteLoaded(TablePageDefinition definition)
        {
            await LoadStageOptionsSourcesAsync();
            ApplyStageCostFractionMode();
            UpdateActionButtonState();
            if (Store.SelectedRow is not null && !Store.SelectedRow.IsPlaceholder)
            {
                UpdateDetailView(Store.SelectedRow);
                _ = RefreshDetailAsync();
            }
        }

        private async Task LoadStageOptionsSourcesAsync()
        {
            OptionsRegistry.Set("StageStatus", await LoadStageStatusOptionsAsync());
            OptionsRegistry.Set("TaskKind", await LoadStageTaskKindOptionsAsync());
            TableView.SetFilterOptionsSources(OptionsRegistry.Snapshot());
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

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadStageTaskKindOptionsAsync()
        {
            var items = await _referenceLookupCacheService.GetItemsAsync("TaskKind");
            return items
                .Select(static item => new CbsTableFilterOptionDefinition
                {
                    Value = item.Id,
                    Label = FormatTaskKindOptionLabel(item.Code, item.DisplayName)
                })
                .Where(static option => option.Value is not null && !string.IsNullOrWhiteSpace(option.Label))
                .DistinctBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static IReadOnlyList<CbsTableFilterOptionDefinition> BuildContractTaskKindOptions(
            IReadOnlyList<ReferenceLookupItem> items)
        {
            return items
                .Where(static item => !string.IsNullOrWhiteSpace(item.Code))
                .Select(static item => new CbsTableFilterOptionDefinition
                {
                    Value = item.Code,
                    Label = FormatTaskKindOptionLabel(item.Code, item.DisplayName)
                })
                .DistinctBy(static option => option.Value?.ToString(), StringComparer.OrdinalIgnoreCase)
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
                await ShowStageEditDialogAsync();
            }
        }

        protected override string BuildSelectedFooterText(TableDataRow row)
        {
            return _contractWorkflowStore.SelectedFooterText;
        }

        private void UpdateActionButtonState()
        {
            var hasSelectedRow = Store.SelectedRow is not null && !Store.SelectedRow.IsPlaceholder;

            ApplyEditButtonState(_editButton, hasSelectedRow && Store.CanEditRows);
            ApplyDefaultActionButtonState(_infoButton, HasContractInfoSelection());
            ApplyDefaultActionButtonState(_copyButton, hasSelectedRow);
            ApplyDefaultActionButtonState(_commentButton, hasSelectedRow && _userService.CurrentUser?.ProfileId is not null);
            ApplyCreateButtonState(_createEmployeeButton, hasSelectedRow);
            ApplyDefaultActionButtonState(_saveFiltersButton, Store.HasActiveReference);
        }

        private bool HasContractInfoSelection()
        {
            return Store.SelectedRow is { IsPlaceholder: false } row
                && TryGetSelectedRowId(row) == _contractWorkflowStore.SelectedStageEditState?.Id
                && _contractWorkflowStore.SelectedContractEditState is not null;
        }

        private void UpdateDetailView(TableDataRow? row)
        {
            if (row is null || row.IsPlaceholder)
            {
                return;
            }

            SetDetailContentVisible(true);
            _detailView.Visibility = Visibility.Visible;
            _detailView.RevisionRow = row;
        }

        private async Task RefreshDetailAsync()
        {
            _detailCts?.Cancel();

            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                UpdateActionButtonState();
                return;
            }

            _detailView.ContractRow = null;
            _detailView.ContragentRow = null;
            _contractWorkflowStore.ClearRowDetailSelection();
            RefreshSelectedFooterText();

            var cancellationTokenSource = new CancellationTokenSource();
            _detailCts = cancellationTokenSource;

            try
            {
                var context = await _contractWorkflowFactory.CreateFromStageRowAsync(
                    Store.SelectedRow,
                    cancellationTokenSource.Token);
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                if (!ApplyStageWorkflowContextIfCurrent(context))
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    UpdateActionButtonState();
                }
            }
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

        private void ClearDetailView()
        {
            _detailCts?.Cancel();
            _detailView.RevisionRow = null;
            _detailView.ContractRow = null;
            _detailView.ContragentRow = null;
            _detailView.Visibility = Visibility.Collapsed;
            SetDetailContentVisible(false);
            _contractWorkflowStore.ClearRowDetailSelection();
            RefreshSelectedFooterText();
        }

        private async Task ShowStageEditDialogAsync()
        {
            if (Store.SelectedRow is null)
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
                await ShowStageContractCommerEditDialogAsync();
                return;
            }

            if (_userService.CurrentUser?.DepartmentId == FinDepartmentId)
            {
                await ShowStageFinEditDialogAsync();
                return;
            }

            await ShowContractInfoDialogAsync();
        }

        private async Task ShowStageOziEditDialogAsync(TableDataRow? sourceRowOverride = null)
        {
            var sourceRow = sourceRowOverride ?? Store.SelectedRow;
            if (sourceRow is null || sourceRow.IsPlaceholder)
            {
                await ShowErrorDialogAsync("Редактирование этапа", "Не выбран этап для редактирования.");
                return;
            }

            if (!await PrepareStageEditContextAsync(sourceRow))
            {
                return;
            }

            var statusOptions = OptionsRegistry.Get("StageStatus");
            var employeeItems = await LoadOziEmployeeItemsAsync();
            StageOziEditDialog dialog;
            try
            {
                var selectedStageEditState = _contractWorkflowStore.SelectedStageEditState
                    ?? throw new InvalidOperationException("StageHostView.ShowStageOziEditDialogAsync: SelectedStageEditState is not set.");
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

            TableDataRow? savedRow = null;
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

                    savedRow = await SaveStagePayloadAsync(stagePayload);

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
            if (!dialog.WasSaved || savedRow is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(GetCurrentTableModel());
            await RefreshTableRowAfterSaveAsync(false, savedRow);
            ShowSuccessNotification(
                "Этап сохранен",
                BuildReferenceNotificationMessage("Этап", TryGetSelectedRowId(savedRow)));
        }

        private async Task ShowContractInfoDialogAsync(TableDataRow? sourceRowOverride = null)
        {
            var sourceRow = sourceRowOverride ?? Store.SelectedRow;
            if (sourceRow is null || sourceRow.IsPlaceholder)
            {
                await ShowErrorDialogAsync("Информация о контракте", "Не выбран этап для просмотра.");
                return;
            }

            if (!await PrepareStageEditContextAsync(sourceRow))
            {
                return;
            }

            ContractInfoDialog dialog;
            try
            {
                var contract = _contractWorkflowStore.SelectedContractEditState
                    ?? throw new InvalidOperationException("StageHostView.ShowContractInfoDialogAsync: SelectedContractEditState is not set.");
                var auditSummary = await ContractInfoAuditLoader.LoadAsync(
                    _dataQueryService,
                    contract.Id,
                    contract.Status.Id == WorkflowStatusIds.Closed);
                var stage = _contractWorkflowStore.SelectedStageEditState
                    ?? throw new InvalidOperationException("StageHostView.ShowContractInfoDialogAsync: SelectedStageEditState is not set.");
                dialog = new ContractInfoDialog(
                    stage,
                    contract,
                    _contractWorkflowStore.GetContractDocumentRevisionEditState(),
                    auditSummary,
                    BuildStageNavigationState(stage),
                    NavigateStageEditDialogAsync)
                {
                    XamlRoot = XamlRoot
                };
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось открыть информацию о контракте.", ex.Message);
                return;
            }

            await dialog.ShowAsync();
        }

        private async Task ShowStageFinEditDialogAsync(TableDataRow? sourceRowOverride = null)
        {
            var sourceRow = sourceRowOverride ?? Store.SelectedRow;
            if (sourceRow is null || sourceRow.IsPlaceholder)
            {
                await ShowErrorDialogAsync("Редактирование этапа", "Не выбран этап для редактирования.");
                return;
            }

            if (!await PrepareStageEditContextAsync(sourceRow))
            {
                return;
            }

            var statusOptions = OptionsRegistry.Get("StageStatus");
            StageFinEditDialog dialog;
            try
            {
                var selectedStageEditState = _contractWorkflowStore.SelectedStageEditState
                    ?? throw new InvalidOperationException("StageHostView.ShowStageFinEditDialogAsync: SelectedStageEditState is not set.");
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

            TableDataRow? savedRow = null;
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
                        savedRow = await SaveStagePayloadAsync(stagePayload);
                    }

                    if (hasContractChanges)
                    {
                        await _modelMutationService.UpdateAsync(
                            ContractModel,
                            dialog.BuildContractExternalNumberPayload());
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
            await RefreshTableRowAfterSaveAsync(false, savedRow);
            ShowSuccessNotification(
                "Этап сохранен",
                BuildReferenceNotificationMessage("Этап", TryGetSelectedRowId(savedRow)));
        }

        private void CopyStageInfo()
        {
            var text = StageClipboardFormatter.BuildClipboardText(Store.SelectedRow);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            ShowSuccessNotification(
                "Данные скопированы",
                "Этап скопирован в буфер обмена.");
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
                PlaceholderText = "Введите комментарий + Enter"
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

            if (Store.SelectedRow is null || TryGetSelectedRowId(Store.SelectedRow) is not long stageId)
            {
                await ShowErrorDialogAsync(
                    "Комментарий к этапу",
                    "Не удалось определить выбранный этап.");
                return;
            }

            if (_userService.CurrentUser?.ProfileId is not int profileId)
            {
                await ShowErrorDialogAsync(
                    "Комментарий к этапу",
                    "Не удалось определить profile_id пользователя.");
                return;
            }

            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = stageId
            };
            var listKey = Store.SelectedRow.GetValue("list_key")?.ToString();
            if (!string.IsNullOrWhiteSpace(listKey))
            {
                payload["list_key"] = listKey;
            }

            StageEditPayloadBuilderHelpers.AppendCommentAttributes(payload, normalizedComment, profileId);

            try
            {
                await SaveStagePayloadAsync(payload);
                flyout.Hide();
                ShowSuccessNotification(
                    "Комментарий сохранен",
                    "Комментарий к этапу добавлен.");
                await RefreshDetailAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось сохранить комментарий", ex.Message);
            }
        }

        private async Task CreateEmployeeForSelectedStageAsync()
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
                TryGetLongValue(Store.SelectedRow, "contract.contragent.id")
                ?? TryGetLongValue(Store.SelectedRow, "contract.contragent_id")
                ?? TryGetLongValue(Store.SelectedRow, "contragent.id")
                ?? TryGetLongValue(Store.SelectedRow, "contragent_id");
            var contragentName =
                JsonDataReader.TryGetText(Store.SelectedRow, "contract.contragent.name")
                ?? JsonDataReader.TryGetText(Store.SelectedRow, "contract.contragent.org.name")
                ?? JsonDataReader.TryGetText(Store.SelectedRow, "contract.contragent.org.full_name")
                ?? JsonDataReader.TryGetText(Store.SelectedRow, "contragent.name");

            if (contragentId is null || string.IsNullOrWhiteSpace(contragentName))
            {
                await ShowErrorDialogAsync(
                    "Не удалось создать сотрудника.",
                    "В выбранном этапе отсутствует контрагент.");
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

        private async Task SaveStageFiltersAsync()
        {
            var user = _userService.CurrentUser;
            try
            {
                var settingsPayload = StageTableFilterSettingsPayloadBuilder.Build(
                    user?.ProfileId,
                    user?.Statuses,
                    user?.ContractsTypes,
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
                        "Сохранить текущие фильтры этапов как начальные установки фильтрации?",
                        "Сохранить"))
                {
                    return;
                }

                DiagnosticsFileLogger.AppendBlock(
                    "STAGE FILTER SETTINGS UPDATE REQUEST",
                    $"payload={JsonSerializer.Serialize(settingsPayload.Payload)}");
                await _modelMutationService.UpdateAsync(ProfileModel, settingsPayload.Payload);

                if (user is not null)
                {
                    user.Statuses = settingsPayload.StatusesJson;
                    user.ContractsTypes = settingsPayload.ContractsTypesJson;
                }

                Store.AppendUiTrace("STAGE FILTER SETTINGS SAVED");
                ShowSuccessNotification(
                    "Настройки сохранены",
                    "Начальные установки фильтрации этапов обновлены.");
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось сохранить фильтры", ex.Message);
            }
        }

        private async Task ToggleStageCostFractionAsync()
        {
            _showStageCostFraction = _showCostFractionButton?.IsChecked == true;
            ApplyStageCostFractionMode();

            var settings = await _localUserSettingsService.GetAsync();
            settings.ShowStageCostFraction = _showStageCostFraction;
            await _localUserSettingsService.SaveAsync(settings);
        }

        private void ApplyStageCostFractionMode()
        {
            TableView.ShowStageCostFraction = _showStageCostFraction;
            if (_showCostFractionButton is not null)
            {
                _showCostFractionButton.IsChecked = _showStageCostFraction;
                _showCostFractionButton.Foreground = _showStageCostFraction
                    ? GetBrush("ShellPrimaryTextBrush")
                    : GetBrush("ShellSecondaryTextBrush");
            }
        }

        private bool ApplyStageWorkflowContextIfCurrent(ContractWorkflowContext context)
        {
            if (!context.Matches(Store.SelectedRow))
            {
                return false;
            }

            context.ApplyTo(_contractWorkflowStore, _rowDetailStrategy);
            _detailView.ContractRow = context.Contract;
            _detailView.ContragentRow = context.Contragent;
            RefreshSelectedFooterText();
            UpdateActionButtonState();
            return true;
        }

        private async Task ShowStageContractCommerEditDialogAsync(TableDataRow? sourceRowOverride = null)
        {
            var sourceRow = sourceRowOverride ?? Store.SelectedRow;
            if (sourceRow is null || sourceRow.IsPlaceholder)
            {
                await ShowErrorDialogAsync("Редактирование этапа", "Не выбран этап для редактирования.");
                return;
            }

            if (!await PrepareStageEditContextAsync(sourceRow))
            {
                return;
            }

            var contract = _contractWorkflowStore.Contract
                ?? throw new InvalidOperationException("StageHostView.ShowStageContractCommerEditDialogAsync: ContractWorkflowStore.Contract is not set.");

            ContractCommerEditDialog dialog;
            try
            {
                var allStatusOptions = await _contractWorkflowStore.GetAllStatusOptionsAsync(_referenceLookupCacheService);
                var taskKindItems = await _referenceLookupCacheService.GetItemsAsync("TaskKind");
                var taskKindOptions = BuildContractTaskKindOptions(taskKindItems);
                if (!string.IsNullOrWhiteSpace(_contractWorkflowStore.ContractEditGraphRepairMessage))
                {
                    Store.AppendUiTrace($"CONTRACT EDIT GRAPH REPAIRED contract={TryGetLongValue(contract, "id")} message={_contractWorkflowStore.ContractEditGraphRepairMessage}");
                }

                dialog = new ContractCommerEditDialog(
                    _contractWorkflowStore,
                    contract,
                    taskKindOptions,
                    taskKindItems,
                    allStatusOptions,
                    allStatusOptions,
                    _contragentLookupService.LoadOptionsAsync,
                    openStagesTabOnLoad: true)
                {
                    XamlRoot = XamlRoot
                };
            }
            catch (Exception ex)
            {
                Store.AppendUiTrace($"CONTRACT EDIT OPEN FAILED contract={TryGetLongValue(contract, "id")} exception={ex}");
                await ShowErrorDialogAsync("Редактирование этапа", ex.Message);
                return;
            }

            TableDataRow? savedContractRow = null;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    var payload = dialog.BuildPayload(_userService.CurrentUser?.ProfileId);
                    if (!HasUpdatePayloadChanges(payload))
                    {
                        dialog.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    savedContractRow = await _modelMutationService.UpdateAsync(ContractModel, payload);
                }
                catch (Exception ex)
                {
                    dialog.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };

            await dialog.ShowAsync();
            if (!dialog.WasSaved || savedContractRow is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(GetCurrentTableModel());
            _referenceLookupCacheService.Invalidate(ContractModel);
            await RefreshTableRowAfterSaveAsync(false, sourceRow);

            await RefreshDetailAsync();
            ShowSuccessNotification(
                "Контракт сохранен",
                "Изменения этапов контракта сохранены.");
        }

        private async Task<bool> PrepareStageEditContextAsync(TableDataRow sourceRow)
        {
            try
            {
                var context = await _contractWorkflowFactory.CreateFromStageRowAsync(sourceRow);
                if (!ApplyStageWorkflowContextIfCurrent(context))
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(
                    "Не удалось открыть этап.",
                    FormatStageEditNavigationError("StageHostView.PrepareStageEditContextAsync", ex));
                return false;
            }
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
            try
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
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    FormatStageEditNavigationError("StageHostView.NavigateStageEditDialogAsync", ex),
                    ex);
            }
        }

        private static string FormatStageEditNavigationError(string location, Exception ex)
        {
            return $"{location}: {ex.Message}";
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

        private Task<TableDataRow> SaveStagePayloadAsync(IReadOnlyDictionary<string, object?> payload)
        {
            return _modelMutationService.UpdateAsync(GetCurrentTableModel(), payload);
        }

        private string GetCurrentTableModel()
        {
            return CurrentDefinition?.Model
                ?? Store.CurrentTablePage?.Model
                ?? "Stage";
        }

        private static ToggleButton CreateStageCostFractionButton()
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
            ToolTipService.SetToolTip(button, "Показывать долю стоимости этапа");
            return button;
        }

        private static bool ContainsNestedAttributes(IReadOnlyDictionary<string, object?> payload)
        {
            return payload.Keys.Any(static key => key.EndsWith("_attributes", StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasUpdatePayloadChanges(IReadOnlyDictionary<string, object?> payload)
        {
            return payload.Keys.Any(static key =>
                !string.Equals(key, "id", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "list_key", StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildReferenceNotificationMessage(string referenceTitle, long? id)
        {
            return id.HasValue
                ? $"{referenceTitle}, ID {id.Value}"
                : referenceTitle;
        }

        private static string BuildSingleStageFooterText(string? contractName, long? id)
        {
            var mainText = contractName ?? string.Empty;
            return id is long idValue
                ? $"{mainText} (ID: {idValue})"
                : mainText;
        }

        private bool IsSingleStageContract(TableDataRow row, int? priority)
        {
            if (priority <= 0)
            {
                return true;
            }

            var stageCount = TryGetContractStageCount(row);
            if (stageCount is not null)
            {
                return stageCount <= 1;
            }

            if (IsSameContractSelection(row))
            {
                stageCount = TryGetContractStageCount(_contractWorkflowStore.Contract);
                if (stageCount is not null)
                {
                    return stageCount <= 1;
                }

                if (_contractWorkflowStore.SelectedContractEditState is { } contractState)
                {
                    return !contractState.IsMultiStage;
                }
            }

            return false;
        }

        private bool IsSameContractSelection(TableDataRow row)
        {
            var rowContractId = _rowDetailStrategy.ResolveContractId(row);
            var storeContractId = _contractWorkflowStore.Contract is null
                ? null
                : TryGetLongValue(_contractWorkflowStore.Contract, "id");

            return rowContractId is not null && rowContractId == storeContractId;
        }

        private static int? TryGetContractStageCount(TableDataRow? row)
        {
            if (row is null || row.IsPlaceholder)
            {
                return null;
            }

            var stages =
                JsonDataReader.TryGetArray(row, "contract.stages")
                ?? JsonDataReader.TryGetArray(row, "stages");
            if (stages is not null)
            {
                return stages.Value.GetArrayLength();
            }

            var isMultiStage =
                JsonDataReader.TryGetBool(row.GetValue("contract.multyStage"))
                ?? JsonDataReader.TryGetBool(row.GetValue("contract.multiStage"))
                ?? JsonDataReader.TryGetBool(row.GetValue("contract.is_multistage"))
                ?? JsonDataReader.TryGetBool(row.GetValue("multyStage"))
                ?? JsonDataReader.TryGetBool(row.GetValue("multiStage"))
                ?? JsonDataReader.TryGetBool(row.GetValue("is_multistage"));

            return isMultiStage is null ? null : isMultiStage.Value ? 2 : 1;
        }

        private static string AppendFooterDetail(string mainText, TableDataRow row)
        {
            var parts = new List<string>();
            var taskKind = JsonDataReader.TryGetText(row, "task_kind.name", "contract.task_kind.name");
            if (!string.IsNullOrWhiteSpace(taskKind))
            {
                parts.Add(taskKind);
            }

            var performers = ReadNameList(row, "performers");
            parts.Add($"Исполнители: {(performers.Count == 0 ? "нет" : string.Join(", ", performers))}");

            var tasks = ReadNameList(row, "tasks");
            if (tasks.Count > 0)
            {
                parts.Add($"Прочие задачи: {string.Join(", ", tasks)}");
            }

            var detailText = string.Join("; ", parts);
            return string.IsNullOrWhiteSpace(detailText)
                ? mainText
                : $"{mainText} | {detailText}";
        }

        private static IReadOnlyList<string> ReadNameList(TableDataRow row, string fieldKey)
        {
            return JsonDataReader.EnumerateObjectArray(row, fieldKey)
                .Select(ReadDisplayName)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!)
                .ToList();
        }

        private static string? ReadDisplayName(JsonElement item)
        {
            return JsonDataReader.TryGetString(item, "name")
                ?? JsonDataReader.TryGetString(item, "full_name")
                ?? JsonDataReader.TryGetString(item, "head")
                ?? JsonDataReader.TryGetString(item, "description");
        }

        private static long? TryGetLongValue(TableDataRow row, string fieldKey)
        {
            return JsonDataReader.TryGetLong(row.GetValue(fieldKey));
        }

        private static Brush? GetBrush(string resourceKey)
        {
            return Application.Current.Resources[resourceKey] as Brush;
        }

    }
}
