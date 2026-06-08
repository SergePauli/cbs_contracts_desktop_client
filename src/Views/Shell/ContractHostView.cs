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
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.Views.Functional;
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
        private const string ContractModel = "Contract";
        private const string ProfileModel = "Profile";

        private static readonly IReadOnlySet<long> ContractStatusIds = new HashSet<long> { 0, 1, 5, 6, 7 };

        private readonly IDataQueryService _dataQueryService;
        private readonly IModelMutationService _modelMutationService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly IEmployeeEditWorkflow _employeeEditWorkflow;
        private readonly ILocalUserSettingsService _localUserSettingsService;
        private readonly IUserService _userService;
        private readonly ContractWorkflowStore _contractWorkflowStore;
        private readonly ContractTableRowDetailStrategy _rowDetailStrategy = new();
        private readonly ContractDetailView _detailView = new();
        private CancellationTokenSource? _detailCts;
        private bool _showContractCostFraction;
        private Button? _editButton;
        private Button? _copyButton;
        private Button? _commentButton;
        private Button? _createEmployeeButton;
        private Button? _saveFiltersButton;
        private ToggleButton? _showCostFractionButton;

        public ContractHostView()
        {
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _modelMutationService = App.Services.GetRequiredService<IModelMutationService>();
            _referenceLookupCacheService = App.Services.GetRequiredService<IReferenceLookupCacheService>();
            _referenceDefinitionService = App.Services.GetRequiredService<IReferenceDefinitionService>();
            _employeeEditWorkflow = App.Services.GetRequiredService<IEmployeeEditWorkflow>();
            _localUserSettingsService = App.Services.GetRequiredService<ILocalUserSettingsService>();
            _userService = App.Services.GetRequiredService<IUserService>();
            _contractWorkflowStore = App.Services.GetRequiredService<ContractWorkflowStore>();
            _showContractCostFraction = _localUserSettingsService.Get().ShowContractCostFraction;
            SetDetailContent(_detailView);
            ClearDetailView();
        }

        protected override IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            _editButton = CreateHeaderIconButton("\uE70F", "Редактировать контракт");
            _editButton.Click += async (_, _) => await ShowEditNotImplementedAsync();

            _copyButton = CreateHeaderIconButton("\uE8C8", "Скопировать контракт");
            _copyButton.Click += (_, _) => CopyContractInfo();

            _commentButton = CreateHeaderIconButton("\uE90A", "Добавить комментарий к контракту");
            _commentButton.Click += CommentContractButton_Click;

            _createEmployeeButton = CreateHeaderIconButton("\uE77B", "Добавить сотрудника");
            _createEmployeeButton.Click += async (_, _) => await CreateEmployeeForSelectedContractAsync();

            _showCostFractionButton = CreateContractCostFractionButton();
            _showCostFractionButton.Click += async (_, _) => await ToggleContractCostFractionAsync();

            _saveFiltersButton = CreateHeaderIconButton("\uE74E", "Сохранить текущие фильтры контрактов");
            _saveFiltersButton.Click += async (_, _) => await SaveContractFiltersAsync();

            ApplyContractCostFractionMode();
            UpdateActionButtonState();
            return
            [
                _editButton,
                _copyButton,
                _commentButton,
                _createEmployeeButton,
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
                await ShowEditNotImplementedAsync();
            }
        }

        protected override string BuildSelectedFooterText(TableDataRow row)
        {
            var name = JsonDataReader.TryGetText(row, "name", "contract.name") ?? "Контракт";
            var id = TryGetSelectedRowId(row);
            var taskKind = JsonDataReader.TryGetText(row, "task_kind.name", "stage.task_kind.name");
            var text = id is long idValue ? $"{name} (ID: {idValue})" : name;
            return string.IsNullOrWhiteSpace(taskKind)
                ? text
                : $"{text} | {taskKind}";
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
            var statusOptions = await _referenceLookupCacheService.GetOptionsAsync("Status");
            return statusOptions
                .Where(static option => JsonDataReader.TryGetLong(option.Value) is long id && ContractStatusIds.Contains(id))
                .OrderBy(static option => JsonDataReader.TryGetLong(option.Value))
                .ToList();
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

            if (_editButton is not null)
            {
                _editButton.IsEnabled = hasSelectedRow && Store.CanEditRows;
            }

            if (_copyButton is not null)
            {
                _copyButton.IsEnabled = hasSelectedRow;
            }

            if (_commentButton is not null)
            {
                _commentButton.IsEnabled = hasSelectedRow && _userService.CurrentUser?.ProfileId is not null;
            }

            if (_createEmployeeButton is not null)
            {
                _createEmployeeButton.IsEnabled = hasSelectedRow;
            }

            if (_saveFiltersButton is not null)
            {
                _saveFiltersButton.IsEnabled = Store.HasActiveReference;
            }
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
                        "Сохранить",
                        applyChrome: true))
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

        private async Task ShowEditNotImplementedAsync()
        {
            await ShowInfoDialogAsync(
                "Редактирование контракта",
                "Перенос диалогов редактирования контрактов будет выполнен отдельным этапом.");
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
