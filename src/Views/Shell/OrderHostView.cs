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
        private Microsoft.UI.Xaml.Controls.Button? _createPositionButton;
        private Microsoft.UI.Xaml.Controls.Button? _editPositionButton;

        public OrderHostView()
        {
            _workflowFactory = App.Services.GetRequiredService<OrderWorkflowFactory>();
            _editWorkflow = App.Services.GetRequiredService<OrderEditWorkflow>();
            _stageOrderEditWorkflow = App.Services.GetRequiredService<StageOrderEditWorkflow>();
            _positionsStore = App.Services.GetRequiredService<OrderPositionsStore>();
            _positionsView = new OrderPositionsTableView(_positionsStore);
            _positionsStore.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(OrderPositionsStore.SelectedRow))
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
            _createPositionButton = CreateHeaderIconButton("\uE8FA", "Добавить позицию заказа");
            _createPositionButton.Click += async (_, _) => await ShowPositionDialogAsync(null);
            _editPositionButton = CreateHeaderIconButton("\uE70F", "Редактировать позицию заказа");
            _editPositionButton.Click += async (_, _) => await ShowPositionDialogAsync(_positionsStore.SelectedRow);
            UpdateButtons();
            return [_createButton, _editButton, _createPositionButton, _editPositionButton];
        }

        protected override int PrimaryHeaderActionCount => 4;

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
            ApplyCreateButtonState(_createButton, Store.CanCreateRows);
            ApplyEditButtonState(_editButton, Store.HasSelectedRow && Store.CanEditRows);
            ApplyCreateButtonState(_createPositionButton, Store.HasSelectedRow && Store.CanEditRows);
            ApplyEditButtonState(_editPositionButton, _positionsStore.SelectedRow is not null && Store.CanEditRows);
        }

        private async Task ShowPositionDialogAsync(TableDataRow? sourceRow)
        {
            var orderId = Store.SelectedRow is null ? null : TryGetSelectedRowId(Store.SelectedRow);
            if (orderId is null) return;
            System.Text.Json.JsonElement? source = sourceRow is null
                ? null
                : System.Text.Json.JsonSerializer.SerializeToElement(sourceRow.Values);
            var saved = await _stageOrderEditWorkflow.ShowAsync(new StageOrderEditWorkflowRequest { XamlRoot = XamlRoot, OrderId = orderId.Value, SourceRow = source });
            if (saved) await LoadSelectedOrderPositionsAsync(Store.SelectedRow);
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
