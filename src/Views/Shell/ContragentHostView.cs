// Hosts the Contragent table, detail footer, and contragent-specific edit workflow.
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
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.References;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed class ContragentHostView : ComplexHostViewBase
    {
        private const string AddressModel = "Address";
        private readonly IDataQueryService _dataQueryService;
        private readonly IContragentFnsWorkflow _fnsWorkflow;
        private readonly IEmployeeEditWorkflow _employeeEditWorkflow;
        private readonly IModelMutationService _modelMutationService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private readonly ContragentDetailView _detailView = new();
        private CancellationTokenSource? _detailCts;
        private Button? _createButton;
        private Button? _editButton;
        private Button? _deleteButton;
        private Button? _createEmployeeButton;
        private Button? _fnsCompareButton;
        private Button? _fnsMenuButton;
        private Button? _copyButton;
        private bool _isFnsWorkflowInProgress;

        public ContragentHostView()
        {
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _fnsWorkflow = App.Services.GetRequiredService<IContragentFnsWorkflow>();
            _employeeEditWorkflow = App.Services.GetRequiredService<IEmployeeEditWorkflow>();
            _modelMutationService = App.Services.GetRequiredService<IModelMutationService>();
            _referenceDefinitionService = App.Services.GetRequiredService<IReferenceDefinitionService>();
            _referenceLookupCacheService = App.Services.GetRequiredService<IReferenceLookupCacheService>();
            _detailView.EmployeeEditRequested += DetailView_EmployeeEditRequested;
            SetDetailContent(_detailView, isVisible: false);
        }

        protected override IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            _editButton = CreateHeaderIconButton("\uE70F", "Редактировать контрагента");
            _editButton.Click += async (_, _) => await ShowContragentEditDialogAsync(isCreateMode: false);

            _deleteButton = CreateHeaderIconButton("\uE74D", "Удалить контрагента");
            _deleteButton.Click += async (_, _) => await DeleteSelectedContragentAsync();

            _createButton = CreateHeaderIconButton("\uF8AA", "Добавить контрагента");
            _createButton.Click += async (_, _) => await ShowContragentEditDialogAsync(isCreateMode: true);

            _createEmployeeButton = CreateHeaderIconButton("\uE8FA", "Добавить сотрудника");
            _createEmployeeButton.Click += async (_, _) => await CreateEmployeeForSelectedContragentAsync();

            _fnsCompareButton = CreateHeaderIconButton("\uE895", "Сверить данные с ФНС");
            _fnsCompareButton.Click += async (_, _) => await RunFnsWorkflowAsync(
                request => _fnsWorkflow.CompareSelectedAsync(request));

            _fnsMenuButton = CreateHeaderIconButton("\uE8B8", "ФНС и юр.лицо");
            _fnsMenuButton.Flyout = CreateFnsMenuFlyout();

            _copyButton = CreateHeaderIconButton("\uE8C8", "Скопировать реквизиты");
            _copyButton.Click += (_, _) => CopyContragentDetails();

            UpdateActionButtonState();
            return [_createButton, _editButton, _deleteButton, _createEmployeeButton, _fnsCompareButton, _fnsMenuButton, _copyButton];
        }

        protected override int PrimaryHeaderActionCount => 3;

        protected override Task OnRouteLoaded(TablePageDefinition definition)
        {
            UpdateActionButtonState();
            UpdateDetailView();
            SetDetailContentVisible(true);
            return Task.CompletedTask;
        }

        protected override Task OnRowSelected(TableDataRow? row)
        {
            UpdateActionButtonState();
            UpdateDetailView();
            _ = RefreshDetailContractsAsync();
            return Task.CompletedTask;
        }

        protected override async Task OpenEditDialogAsync(TableDataRow? row)
        {
            if (row is not null)
            {
                await ShowContragentEditDialogAsync(isCreateMode: false);
            }
        }

        protected override string BuildSelectedFooterText(TableDataRow row)
        {
            var name =
                row.GetValue("requisites.organization.name")?.ToString()
                ?? row.GetValue("name")?.ToString()
                ?? row.GetValue("full_name")?.ToString();
            var id = TryGetSelectedRowId(row);

            if (!string.IsNullOrWhiteSpace(name) && id is long value)
            {
                return $"{name} (ID: {value})";
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return id is long idValue
                ? $"ID: {idValue}"
                : string.Empty;
        }

        private void UpdateActionButtonState()
        {
            var hasSelectedRow = Store.SelectedRow is not null && !Store.SelectedRow.IsPlaceholder;

            ApplyEditButtonState(_editButton, hasSelectedRow && Store.CanEditRows);
            ApplyDeleteButtonState(_deleteButton, hasSelectedRow && Store.CanDeleteRows);
            ApplyCreateButtonState(_createButton, Store.CanCreateRows);
            ApplyCreateButtonState(_createEmployeeButton, hasSelectedRow && !_isFnsWorkflowInProgress);
            ApplyDefaultActionButtonState(_fnsCompareButton, hasSelectedRow && !_isFnsWorkflowInProgress);
            ApplyDefaultActionButtonState(_fnsMenuButton, !_isFnsWorkflowInProgress);
            ApplyDefaultActionButtonState(_copyButton, hasSelectedRow);
        }

        private void UpdateDetailView()
        {
            _detailView.Row = Store.SelectedRow;
            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                _detailView.ContractsRow = null;
            }
        }

        private async Task RefreshDetailContractsAsync()
        {
            _detailCts?.Cancel();
            _detailView.ContractsRow = null;

            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                return;
            }

            var id = TryGetSelectedRowId(Store.SelectedRow);
            if (id is null)
            {
                return;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _detailCts = cancellationTokenSource;

            try
            {
                var row = await LoadContragentEditRowAsync(id.Value, cancellationTokenSource.Token);
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                if (Store.SelectedRow is null || TryGetSelectedRowId(Store.SelectedRow) != id)
                {
                    return;
                }

                _detailView.ContractsRow = row;
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    _detailView.ContractsRow = null;
                }
            }
        }

        private MenuFlyout CreateFnsMenuFlyout()
        {
            var flyout = new MenuFlyout();

            var importItem = new MenuFlyoutItem { Text = "Импорт из ФНС" };
            importItem.Click += async (_, _) => await RunFnsWorkflowAsync(
                request => _fnsWorkflow.ImportAsync(request));
            flyout.Items.Add(importItem);

            var legalEntityChangeItem = new MenuFlyoutSubItem { Text = "Смена юр.лица" };

            var importLegalEntityFnsItem = new MenuFlyoutItem { Text = "Заполнить из ФНС" };
            importLegalEntityFnsItem.Click += async (_, _) => await RunFnsWorkflowAsync(
                request => _fnsWorkflow.ChangeLegalEntityFromFnsAsync(request));
            legalEntityChangeItem.Items.Add(importLegalEntityFnsItem);

            var manualLegalEntityItem = new MenuFlyoutItem { Text = "Ввести вручную" };
            manualLegalEntityItem.Click += async (_, _) => await RunFnsWorkflowAsync(
                request => _fnsWorkflow.ChangeLegalEntityManuallyAsync(request));
            legalEntityChangeItem.Items.Add(manualLegalEntityItem);

            flyout.Items.Add(legalEntityChangeItem);
            return flyout;
        }

        private async Task RunFnsWorkflowAsync(
            Func<ContragentFnsWorkflowRequest, Task<ContragentFnsWorkflowResult?>> workflow)
        {
            var reference = ResolveContragentReference();
            if (reference is null)
            {
                return;
            }

            _isFnsWorkflowInProgress = true;
            UpdateActionButtonState();

            try
            {
                var selectedContragentId = Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder
                    ? null
                    : TryGetSelectedRowId(Store.SelectedRow);
                var result = await workflow(
                    new ContragentFnsWorkflowRequest
                    {
                        XamlRoot = XamlRoot,
                        Definition = reference,
                        SelectedContragentId = selectedContragentId
                    });

                if (result is null)
                {
                    return;
                }

                await RefreshTableRowAfterSaveAsync(result.IsCreateMode, result.SavedRow);
                ShowSuccessNotification(
                    result.SuccessTitle,
                    BuildReferenceNotificationMessage(result.Definition.Title, TryGetSelectedRowId(result.SavedRow)));
            }
            finally
            {
                _isFnsWorkflowInProgress = false;
                UpdateActionButtonState();
            }
        }

        private async Task ShowContragentEditDialogAsync(bool isCreateMode)
        {
            var reference = ResolveContragentReference();
            if (reference is null)
            {
                return;
            }

            TableDataRow? sourceRow = null;
            if (!isCreateMode)
            {
                sourceRow = await LoadSelectedContragentEditRowAsync();
                if (sourceRow is null)
                {
                    await ShowErrorDialogAsync(
                        "Не удалось открыть контрагента.",
                        "Не удалось загрузить свежую карточку контрагента.");
                    return;
                }
            }

            var ownershipOptions = await LoadSimpleReferenceOptionsAsync("Ownership", "card");
            var regionOptions = await LoadSimpleReferenceOptionsAsync("Area", "item");
            var state = ContragentEditStateFactory.Create(
                reference,
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

            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    viewModel.ClearErrorInfo();
                    await EnsureContragentAddressAsync(viewModel);

                    var payload = isCreateMode
                        ? ContragentEditPayloadBuilder.BuildForCreate(viewModel)
                        : ContragentEditPayloadBuilder.BuildForUpdate(viewModel);

                    if (!isCreateMode && payload.Count <= 1)
                    {
                        viewModel.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    savedRow = isCreateMode
                        ? await _modelMutationService.CreateAsync(reference.Model, payload)
                        : await _modelMutationService.UpdateAsync(reference.Model, payload);
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

            _referenceLookupCacheService.Invalidate(reference.Model);
            await RefreshTableRowAfterSaveAsync(isCreateMode, savedRow);
            ShowSuccessNotification(
                isCreateMode ? "Контрагент создан" : "Изменения контрагента сохранены",
                BuildReferenceNotificationMessage(reference.Title, TryGetSelectedRowId(savedRow)));
        }

        private async Task DeleteSelectedContragentAsync()
        {
            var reference = ResolveContragentReference();
            if (reference is null || Store.SelectedRow is null)
            {
                return;
            }

            var id = TryGetSelectedRowId(Store.SelectedRow);
            if (id is null)
            {
                await ShowErrorDialogAsync(
                    "Не удалось удалить контрагента.",
                    "У выбранной записи отсутствует корректный ID.");
                return;
            }

            if (!await ConfirmDialogAsync(
                    "Удаление контрагента",
                    "Удалить выбранного контрагента?",
                    "Удалить",
                    defaultButton: ContentDialogButton.Close))
            {
                return;
            }

            try
            {
                await _modelMutationService.DeleteAsync(reference.Model, id.Value);
                _referenceLookupCacheService.Invalidate(reference.Model);
                ApplyDeletedRowUpdate(id.Value);
                ShowSuccessNotification(
                    "Контрагент удален",
                    BuildReferenceNotificationMessage(reference.Title, id.Value));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось удалить контрагента.", ex.Message);
            }
        }

        private void CopyContragentDetails()
        {
            var text = _detailView.BuildClipboardText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
        }

        private async Task CreateEmployeeForSelectedContragentAsync()
        {
            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
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

            var contragentId = TryGetSelectedRowId(Store.SelectedRow);
            var contragentName = GetContragentName(Store.SelectedRow);
            if (contragentId is null || string.IsNullOrWhiteSpace(contragentName))
            {
                await ShowErrorDialogAsync(
                    "Не удалось создать сотрудника.",
                    "У выбранного контрагента отсутствует корректный ID или название.");
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

            await RefreshSelectedContragentRowAsync(contragentId.Value);
            ShowSuccessNotification(
                "Сотрудник создан",
                BuildReferenceNotificationMessage(result.Definition.Title, TryGetSelectedRowId(result.SavedRow)));
        }

        private async Task RefreshSelectedContragentRowAsync(long contragentId)
        {
            await RefreshTableRowByIdAsync(contragentId);
        }

        private async void DetailView_EmployeeEditRequested(object? sender, EmployeeBoxEditRequestedEventArgs e)
        {
            if (e.Employee.Id is not long employeeId)
            {
                return;
            }

            try
            {
                var contragentId = Store.SelectedRow is null
                    ? null
                    : TryGetSelectedRowId(Store.SelectedRow);
                var result = await _employeeEditWorkflow.ShowAsync(
                    new EmployeeEditWorkflowRequest
                    {
                        XamlRoot = XamlRoot,
                        IsCreateMode = false,
                        EmployeeId = employeeId
                    });

                if (result is null || contragentId is null)
                {
                    return;
                }

                await RefreshSelectedContragentRowAsync(contragentId.Value);
                ShowSuccessNotification(
                    "Изменения сотрудника сохранены",
                    BuildReferenceNotificationMessage(result.Definition.Title, TryGetSelectedRowId(result.SavedRow)));
            }
            catch (InvalidOperationException ex)
            {
                await ShowErrorDialogAsync("Не удалось открыть сотрудника.", ex.Message);
            }
        }

        private async Task<TableDataRow?> LoadSelectedContragentEditRowAsync(CancellationToken cancellationToken = default)
        {
            if (Store.SelectedRow is null)
            {
                return null;
            }

            var id = TryGetSelectedRowId(Store.SelectedRow);
            return id is null
                ? null
                : await LoadContragentEditRowAsync(id.Value, cancellationToken);
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

        protected override async Task OnTableRowRefreshedAfterSaveAsync(TableDataRow freshRow)
        {
            UpdateDetailView();
            await RefreshDetailContractsAsync();
        }

        protected override async Task OnTableReloadedAfterSaveAsync()
        {
            UpdateDetailView();
            await RefreshDetailContractsAsync();
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

        private ReferenceDefinition? ResolveContragentReference()
        {
            return _referenceDefinitionService.TryGetByRoute("/contragents", out var reference)
                ? reference
                : null;
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

        private static string BuildReferenceNotificationMessage(string title, long? id)
        {
            return id.HasValue
                ? $"{title}, ID {id.Value}"
                : title;
        }

        private static string? GetContragentName(TableDataRow row)
        {
            return row.GetValue("requisites.organization.name")?.ToString()
                ?? row.GetValue("requisites.organization.full_name")?.ToString()
                ?? row.GetValue("name")?.ToString()
                ?? row.GetValue("full_name")?.ToString();
        }
    }
}
