using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services.Orders;
using CbsContractsDesktopClient.Stores.Orders;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.Views.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed class OrderHostView : ComplexHostViewBase
    {
        private readonly OrderWorkflowFactory _workflowFactory;
        private readonly OrderEditWorkflow _editWorkflow;
        private readonly StageOrderEditWorkflow _stageOrderEditWorkflow;
        private readonly OrderPositionsStore _positionsStore;
        private readonly OrderPositionsTableView _positionsView;
        private CancellationTokenSource? _detailCts;
        private Microsoft.UI.Xaml.Controls.Button? _createButton;
        private Microsoft.UI.Xaml.Controls.Button? _editButton;
        private Microsoft.UI.Xaml.Controls.Button? _deleteButton;
        private Microsoft.UI.Xaml.Controls.Button? _createPositionButton;
        private Microsoft.UI.Xaml.Controls.Button? _editPositionButton;
        private Microsoft.UI.Xaml.Controls.Button? _unlinkPositionButton;
        private Microsoft.UI.Xaml.Controls.Button? _supplierRequestButton;

        public OrderHostView()
        {
            _workflowFactory = App.Services.GetRequiredService<OrderWorkflowFactory>();
            _editWorkflow = App.Services.GetRequiredService<OrderEditWorkflow>();
            _stageOrderEditWorkflow = App.Services.GetRequiredService<StageOrderEditWorkflow>();
            _positionsStore = App.Services.GetRequiredService<OrderPositionsStore>();
            _positionsView = new OrderPositionsTableView(_positionsStore);
            _positionsStore.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(OrderPositionsStore.SelectedRow)
                    or nameof(OrderPositionsStore.Rows)
                    or nameof(OrderPositionsStore.IsLoading)
                    or nameof(OrderPositionsStore.IsLoaded))
                {
                    UpdateButtons();
                }
            };
            _positionsView.PositionEditRequested += async (_, args) => await ShowPositionDialogAsync(args.Row);
            Unloaded += (_, _) => _detailCts?.Cancel();
            SetDetailContent(_positionsView, isVisible: false);
        }

        protected override IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            _editButton = CreateHeaderIconButton("\uE70F", "Редактировать заказ");
            _editButton.Click += async (_, _) => await ShowEditDialogAsync(false);
            _createButton = CreateHeaderIconButton("\uF8AA", "Создать заказ");
            _createButton.Click += async (_, _) => await ShowEditDialogAsync(true);
            _deleteButton = CreateHeaderIconButton("\uE74D", "Удалить заказ");
            _deleteButton.Click += async (_, _) => await DeleteOrderAsync(_deleteButton);
            _createPositionButton = CreateHeaderIconButton("\uE7BF", "Добавить позицию заказа");
            _createPositionButton.Click += async (_, _) => await ShowPositionDialogAsync(null);
            _editPositionButton = CreateHeaderIconButton("\uE932", "Редактировать позицию заказа");
            _editPositionButton.Click += async (_, _) => await ShowPositionDialogAsync(_positionsStore.SelectedRow);
            _unlinkPositionButton = CreateHeaderIconButton("\uE711", "Отвязать позицию от заказа");
            _unlinkPositionButton.Click += async (_, _) => await UnlinkPositionAsync(_unlinkPositionButton);
            _supplierRequestButton = CreateHeaderIconButton("\uE8A4", "Сводный запрос поставщику");
            _supplierRequestButton.Click += ShowSupplierRequest;
            UpdateButtons();
            return
            [
                _createButton,
                _editButton,
                _deleteButton,
                _createPositionButton,
                _editPositionButton,
                _unlinkPositionButton,
                _supplierRequestButton
            ];
        }

        protected override int PrimaryHeaderActionCount => 7;

        protected override Task OnRouteLoaded(TablePageDefinition definition)
        {
            _positionsStore.Clear();
            UpdateButtons();
            SetDetailContentVisible(true);
            return Task.CompletedTask;
        }

        protected override Task OnRowSelected(TableDataRow? row)
        {
            _ = LoadSelectedOrderPositionsAsync(row);
            UpdateButtons();
            return Task.CompletedTask;
        }

        protected override string BuildSelectedFooterText(TableDataRow row)
        {
            return row.GetValue("name")?.ToString()
                ?? row.GetValue("order_number")?.ToString()
                ?? string.Empty;
        }

        private async Task LoadSelectedOrderPositionsAsync(TableDataRow? row)
        {
            _detailCts?.Cancel();
            _positionsStore.Clear();
            if (row is null || row.IsPlaceholder || TryGetSelectedRowId(row) is not long orderId)
            {
                return;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _detailCts = cancellationTokenSource;
            try
            {
                await _positionsStore.LoadAsync(orderId, cancellationTokenSource.Token);
                if (cancellationTokenSource.IsCancellationRequested
                    || Store.SelectedRow is null
                    || TryGetSelectedRowId(Store.SelectedRow) != orderId)
                {
                    return;
                }

                UpdateButtons();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось загрузить позиции заказа.", ex.Message);
            }
        }

        private void UpdateButtons()
        {
            var canModifyPositions = Store.SelectedRow is not null
                && OrderCompositionPolicy.CanModifyPositions(Store.SelectedRow);
            ApplyCreateButtonState(_createButton, Store.CanCreateRows);
            ApplyEditButtonState(_editButton, Store.HasSelectedRow && Store.CanEditRows);
            ApplyDeleteButtonState(
                _deleteButton,
                Store.HasSelectedRow
                && Store.CanDeleteRows
                && !_positionsStore.IsLoading
                && _positionsStore.IsLoaded);
            ApplyCreateButtonState(
                _createPositionButton,
                canModifyPositions && Store.CanEditRows);
            ApplyEditButtonState(
                _editPositionButton,
                _positionsStore.SelectedRow is not null && Store.CanEditRows);
            ApplyDeleteButtonState(
                _unlinkPositionButton,
                canModifyPositions && _positionsStore.SelectedRow is not null && Store.CanEditRows);
            ApplyDefaultActionButtonState(
                _supplierRequestButton,
                Store.HasSelectedRow && !_positionsStore.IsLoading && _positionsStore.Rows.Count > 0);
        }

        private async Task UnlinkPositionAsync(FrameworkElement anchor)
        {
            if (Store.SelectedRow is not { } order || _positionsStore.SelectedRow is not { } position)
            {
                return;
            }

            if (!await OrderConfirmationFlyout.ShowAsync(
                    anchor,
                    "Отвязка позиции",
                    "Вернуть выбранную позицию в список потребностей?",
                    "Отвязать"))
            {
                return;
            }

            try
            {
                await _positionsStore.UnlinkPositionAsync(order, position);
                UpdateButtons();
                ShowSuccessNotification(
                    "Позиция отвязана",
                    "Позиция возвращена в список потребностей.");
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось отвязать позицию.", ex.Message);
            }
        }

        private async Task DeleteOrderAsync(FrameworkElement anchor)
        {
            if (Store.SelectedRow is not { } order)
            {
                return;
            }

            if (!await OrderConfirmationFlyout.ShowAsync(
                    anchor,
                    "Удаление заказа",
                    "Отвязать все позиции и удалить выбранный заказ?",
                    "Удалить"))
            {
                return;
            }

            try
            {
                var orderId = await _positionsStore.DeleteOrderAsync(order);
                ApplyDeletedRowUpdate(orderId);
                UpdateButtons();
                ShowSuccessNotification("Заказ удален", $"Заказ ID {orderId}");
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось удалить заказ.", ex.Message);
            }
        }

        private async void ShowSupplierRequest(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement anchor)
            {
                return;
            }

            try
            {
                var lines = OrderSupplierRequestFormatter.BuildLines(_positionsStore.Rows);
                var flyout = new OrderSupplierRequestFlyout(lines);
                flyout.Copied += (_, _) => ShowSuccessNotification(
                    "Список скопирован",
                    "Заготовка запроса поставщику помещена в буфер обмена.");
                flyout.ShowAt(anchor);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось сформировать запрос поставщику.", ex.Message);
            }
        }

        private async Task ShowPositionDialogAsync(TableDataRow? sourceRow)
        {
            var mode = sourceRow is null ? "create" : "edit";
            var orderId = Store.SelectedRow is null ? null : TryGetSelectedRowId(Store.SelectedRow);
            var positionId = sourceRow is null ? null : TryGetSelectedRowId(sourceRow);
            Store.AppendUiTrace(
                $"ORDER POSITION OPEN ENTER mode={mode} order={orderId?.ToString() ?? "<null>"} position={positionId?.ToString() ?? "<null>"}");
            if (orderId is null)
            {
                Store.AppendUiTrace($"ORDER POSITION OPEN SKIP mode={mode} reason=selected-order-id-null");
                return;
            }

            try
            {
                var accessMode = sourceRow is null
                    ? StageOrderEditAccessMode.Full
                    : OrderCompositionPolicy.GetPositionEditAccessMode(Store.SelectedRow!);
                if (sourceRow is null)
                {
                    OrderCompositionPolicy.EnsureCanModifyPositions(Store.SelectedRow!);
                }

                System.Text.Json.JsonElement? source = sourceRow is null
                    ? null
                    : System.Text.Json.JsonSerializer.SerializeToElement(sourceRow.Values);
                Store.AppendUiTrace(
                    $"ORDER POSITION OPEN WORKFLOW mode={mode} access={accessMode} order={orderId.Value}");
                var saved = await _stageOrderEditWorkflow.ShowAsync(new StageOrderEditWorkflowRequest
                {
                    XamlRoot = XamlRoot,
                    OrderId = orderId.Value,
                    SourceRow = source,
                    AccessMode = accessMode,
                    Trace = Store.AppendUiTrace
                });
                Store.AppendUiTrace($"ORDER POSITION OPEN RESULT mode={mode} order={orderId.Value} saved={saved}");
                if (saved)
                {
                    await LoadSelectedOrderPositionsAsync(Store.SelectedRow);
                    Store.AppendUiTrace($"ORDER POSITION OPEN REFRESHED mode={mode} order={orderId.Value}");
                }
            }
            catch (Exception ex)
            {
                Store.AppendUiTrace($"ORDER POSITION OPEN FAILED mode={mode} order={orderId.Value} exception={ex}");
                await ShowErrorDialogAsync("Не удалось открыть позицию заказа.", ex.Message);
            }
        }

        private async Task ShowEditDialogAsync(bool create)
        {
            var card = create || Store.SelectedRow is null
                ? null
                : await _workflowFactory.LoadOrderCardAsync(TryGetSelectedRowId(Store.SelectedRow)!.Value);
            var saved = await _editWorkflow.ShowAsync(create, card, XamlRoot);
            if (saved is null) return;
            await RefreshTableRowAfterSaveAsync(create, saved);
            if (!create) await LoadSelectedOrderPositionsAsync(Store.SelectedRow);
            ShowSuccessNotification(create ? "Заказ создан" : "Изменения заказа сохранены", "Заказы");
        }

    }
}
