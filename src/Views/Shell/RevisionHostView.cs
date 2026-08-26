// Hosts the Revisions table, revision edit dialog, and contract detail footer.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Shell;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using CbsContractsDesktopClient.Views.Functional;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed class RevisionHostView : ComplexHostViewBase
    {
        private const int CommersDepartmentId = 2;
        private const string RevisionTitle = "Дополнительное соглашение";
        private readonly IDataQueryService _dataQueryService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private readonly IEmployeeEditWorkflow _employeeEditWorkflow;
        private readonly IUserService _userService;
        private readonly IContragentLookupService _contragentLookupService;
        private readonly ContractWorkflowStore _contractWorkflowStore;
        private readonly ContractWorkflowFactory _contractWorkflowFactory;
        private readonly ContractCommerSaveWorkflow _contractCommerSaveWorkflow;
        private readonly RevisionRowDetailStrategy _rowDetailStrategy = new();
        private readonly ContractDetailView _detailView = new();
        private Button? _editButton;
        private Button? _infoButton;
        private Button? _copyContractDataButton;
        private Button? _copyCellSelectionButton;

        public RevisionHostView()
        {
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _referenceLookupCacheService = App.Services.GetRequiredService<IReferenceLookupCacheService>();
            _employeeEditWorkflow = App.Services.GetRequiredService<IEmployeeEditWorkflow>();
            _userService = App.Services.GetRequiredService<IUserService>();
            _contragentLookupService = App.Services.GetRequiredService<IContragentLookupService>();
            _contractWorkflowStore = App.Services.GetRequiredService<ContractWorkflowStore>();
            _contractWorkflowFactory = App.Services.GetRequiredService<ContractWorkflowFactory>();
            _contractCommerSaveWorkflow = App.Services.GetRequiredService<ContractCommerSaveWorkflow>();
            _detailView.EmployeeEditRequested += DetailView_EmployeeEditRequested;
            SetDetailContent(_detailView, isVisible: false);
        }

        protected override IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            _editButton = CreateHeaderIconButton("\uE70F", "Редактировать доп. соглашение");
            _editButton.Click += async (_, _) => await ShowRevisionEditDialogAsync();

            _infoButton = CreateHeaderIconButton("\uE946", "Информация о контракте");
            _infoButton.Click += async (_, _) => await ShowContractInfoDialogAsync();

            _copyContractDataButton = CreateHeaderIconButton("\uE8F3", "Скопировать данные выбранного контракта в буфер");
            _copyContractDataButton.Click += (_, _) => CopyContractDetails();

            _copyCellSelectionButton = CreateHeaderIconButton("\uE8C8", "Скопировать выделенный диапазон");
            _copyCellSelectionButton.Click += (_, _) => TableView.CopySelectedCellRangeToClipboard();

            UpdateActionButtonState();
            return [_editButton, _infoButton, _copyContractDataButton, _copyCellSelectionButton];
        }

        protected override int PrimaryHeaderActionCount => 2;

        protected override Task OnRouteLoaded(TablePageDefinition definition)
        {
            UpdateActionButtonState();
            if (Store.SelectedRow is not null && !Store.SelectedRow.IsPlaceholder)
            {
                UpdateDetailView(Store.SelectedRow);
                _ = RefreshDetailAsync();
            }
            return Task.CompletedTask;
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
                await ShowRevisionEditDialogAsync();
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
            ApplyDefaultActionButtonState(
                _copyContractDataButton,
                hasSelectedRow && _contractWorkflowStore.Contract is { IsPlaceholder: false });
            ApplyDefaultActionButtonState(_copyCellSelectionButton, Store.HasActiveReference);
        }

        private bool HasContractInfoSelection()
        {
            return Store.SelectedRow is { IsPlaceholder: false } row
                && _rowDetailStrategy.ResolveContractId(row) == _contractWorkflowStore.SelectedContractEditState?.Id
                && _contractWorkflowStore.SelectedStageEditState is not null;
        }

        private void UpdateDetailView(TableDataRow? row)
        {
            if (row is null || row.IsPlaceholder)
            {
                return;
            }

            SetDetailContentVisible(true);
            _detailView.Visibility = Visibility.Visible;
        }

        private async Task RefreshDetailAsync()
        {
            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                UpdateActionButtonState();
                return;
            }

            var selectedRow = Store.SelectedRow;

            try
            {
                var context = await _contractWorkflowFactory.CreateFromRevisionRowAsync(selectedRow);

                if (!ApplyRevisionWorkflowContextIfCurrent(context))
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                UpdateActionButtonState();
            }
        }

        private bool ApplyRevisionWorkflowContextIfCurrent(ContractWorkflowContext context)
        {
            if (!_contractWorkflowFactory.IsLatest(context)
                || !context.Matches(Store.SelectedRow))
            {
                return false;
            }

            context.ApplyTo(_contractWorkflowStore, _rowDetailStrategy);
            RefreshSelectedFooterText();
            UpdateActionButtonState();
            return true;
        }

        private async Task<ContractWorkflowContext?> EnsureRevisionWorkflowContextForDialogAsync(string title)
        {
            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                return null;
            }

            try
            {
                var context = await _contractWorkflowFactory.CreateFromRevisionRowAsync(Store.SelectedRow);
                return ApplyRevisionWorkflowContextIfCurrent(context)
                    ? context
                    : null;
            }
            catch (Exception ex)
            {
                Store.AppendUiTrace($"REVISION WORKFLOW CONTEXT FAILED title={title} exception={ex}");
                await ShowErrorDialogAsync(title, ex.Message);
                return null;
            }
        }

        private void ClearDetailView()
        {
            _contractWorkflowFactory.CancelCurrentLoad();
            _detailView.Visibility = Visibility.Collapsed;
            SetDetailContentVisible(false);
            _contractWorkflowStore.ClearRowDetailSelection();
            RefreshSelectedFooterText();
        }

        private async Task ShowRevisionEditDialogAsync()
        {
            if (Store.SelectedRow is null)
            {
                return;
            }

            if (IsContractCommerEditAllowedForCurrentUser())
            {
                await ShowRevisionContractCommerEditDialogAsync();
                return;
            }

            await ShowContractInfoDialogAsync();
        }

        private async Task ShowRevisionContractCommerEditDialogAsync()
        {
            var context = await EnsureRevisionWorkflowContextForDialogAsync("Редактирование контракта");
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
                    _contractWorkflowFactory.LoadContragentCardRowAsync,
                    openRevisionsTabOnLoad: true)
                {
                    XamlRoot = XamlRoot
                };
            }
            catch (Exception ex)
            {
                Store.AppendUiTrace($"CONTRACT EDIT OPEN FAILED contract={TryGetLongValue(contract, "id")} exception={ex}");
                await ShowErrorDialogAsync("Редактирование контракта", ex.Message);
                return;
            }

            TableDataRow? savedContractRow = null;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    var savePlan = dialog.BuildSavePlan(_userService.CurrentUser?.ProfileId);
                    if (!savePlan.HasChanges)
                    {
                        dialog.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    var saveResult = await _contractCommerSaveWorkflow.SaveAsync(savePlan);
                    var editRow = await _contractWorkflowFactory.ReloadContractEditRowAsync(saveResult.ContractId);
                    dialog.ReloadAsEdit(editRow);
                    savedContractRow = editRow;
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
            await RefreshTableRowAfterSaveAsync(false, savedContractRow);
            await RefreshDetailAsync();
            ShowSuccessNotification(
                "Контракт сохранен",
                "Изменения контракта сохранены.");
        }

        private bool IsContractCommerEditAllowedForCurrentUser()
        {
            var user = _userService.CurrentUser;
            return user?.DepartmentId == CommersDepartmentId;
        }

        private static bool HasUpdatePayloadChanges(IReadOnlyDictionary<string, object?> payload)
        {
            return payload.Keys.Any(static key =>
                !string.Equals(key, "id", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "list_key", StringComparison.OrdinalIgnoreCase));
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

        private async Task ShowContractInfoDialogAsync()
        {
            if (await EnsureRevisionWorkflowContextForDialogAsync("Информация о контракте") is null)
            {
                return;
            }

            var stage = _contractWorkflowStore.SelectedStageEditState
                ?? throw new InvalidOperationException("RevisionHostView.ShowContractInfoDialogAsync: SelectedStageEditState is not set.");
            var contract = _contractWorkflowStore.SelectedContractEditState
                ?? throw new InvalidOperationException("RevisionHostView.ShowContractInfoDialogAsync: SelectedContractEditState is not set.");
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

        private void CopyContractDetails()
        {
            var text = _detailView.BuildClipboardText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            ShowSuccessNotification(
                "Данные скопированы",
                "Карточка контракта скопирована в буфер обмена.");
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

        private string GetCurrentTableModel()
        {
            return CurrentDefinition?.Model
                ?? Store.CurrentTablePage?.Model
                ?? "Revision";
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

        private static string BuildReferenceNotificationMessage(string title, long? id)
        {
            return id.HasValue
                ? $"{title}, ID {id.Value}"
                : title;
        }

        private static string AppendFooterDetail(string mainText, TableDataRow row)
        {
            var detailText = row.GetValue("description")?.ToString();
            return string.IsNullOrWhiteSpace(detailText)
                ? mainText
                : $"{mainText} | {detailText}";
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
    }
}
