using System.ComponentModel;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services.Orders;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.Stores.Orders;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.Views.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed class StageOrderNeedsHostView : ComplexHostViewBase
    {
        private readonly StageOrderNeedsStore _needsStore;
        private readonly IContragentLookupService _contragents;
        private readonly IReferenceLookupCacheService _referenceLookups;
        private readonly StageStatusFilterOptionsProvider _stageStatusFilterOptionsProvider;
        private readonly StageOrderEditWorkflow _stageOrderEditWorkflow;
        private Button? _createOrderButton;
        private Button? _addToOrderButton;
        private Button? _editPositionButton;
        private Button? _deletePositionButton;
        private ToggleButton? _showOrdersButton;
        private bool _isSyncingOrderMode;
        private static readonly string[] OrderColumnKeys = ["order_number", "supplier", "order_status"];

        public StageOrderNeedsHostView()
        {
            _needsStore = App.Services.GetRequiredService<StageOrderNeedsStore>();
            _contragents = App.Services.GetRequiredService<IContragentLookupService>();
            _referenceLookups = App.Services.GetRequiredService<IReferenceLookupCacheService>();
            _stageStatusFilterOptionsProvider = App.Services.GetRequiredService<StageStatusFilterOptionsProvider>();
            _stageOrderEditWorkflow = App.Services.GetRequiredService<StageOrderEditWorkflow>();
            _needsStore.PropertyChanged += OnNeedsStorePropertyChanged;
            Unloaded += (_, _) => _needsStore.ClearSelection();
        }

        protected override IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            _createOrderButton = CreateHeaderIconButton("\uE8F4", "Создать заказ из выбранных позиций");
            _createOrderButton.Click += async (_, _) => await CreateOrderAsync();
            _addToOrderButton = CreateHeaderIconButton("\uE710", "Добавить выбранные позиции в заказ");
            _addToOrderButton.Click += async (_, _) => await AddToOrderAsync();
            _editPositionButton = CreateHeaderIconButton("\uE932", "Редактировать последнюю выбранную позицию");
            _editPositionButton.Click += async (_, _) => await EditLastSelectedPositionAsync();
            _deletePositionButton = CreateHeaderIconButton("\uE74D", "Удалить последнюю выбранную позицию");
            _deletePositionButton.Click += async (_, _) => await DeleteLastSelectedPositionAsync(_deletePositionButton);
            _showOrdersButton = CreateShowOrdersButton();
            _showOrdersButton.Click += async (_, _) => await ApplyShowOrdersModeAsync();
            UpdateButtons();
            return
            [
                _createOrderButton,
                _addToOrderButton,
                _editPositionButton,
                _deletePositionButton,
                _showOrdersButton
            ];
        }

        protected override int PrimaryHeaderActionCount => 5;

        protected override async Task OnRouteLoaded(TablePageDefinition definition)
        {
            OptionsRegistry.Set("StageStatus", await _stageStatusFilterOptionsProvider.LoadAsync());
            OptionsRegistry.Set("OrderStatus", await _referenceLookups.GetOptionsAsync("OrderStatus"));
            TableView.SetFilterOptionsSources(OptionsRegistry.Snapshot());
            _needsStore.ClearSelection();
            TableView.SupportsMultipleRowSelection = true;
            TableView.CanSelectRow = row => JsonDataReader.TryGetLong(row.GetValue("order.id")) is null;
            SyncOrderModeFromFilters();
            UpdateButtons();
        }

        protected override void OnRowSelectionChanged(TableDataRow? row, bool isSelected)
        {
            if (row is null)
            {
                return;
            }

            try
            {
                _needsStore.SetSelected(row, isSelected);
            }
            catch (Exception ex)
            {
                TableView.SetSelectedRow(null);
                _needsStore.ClearSelection();
                _ = ShowErrorDialogAsync("Невозможно выбрать позицию.", ex.Message);
            }
        }

        protected override void OnTableQueryChanged()
        {
            TableView.SetSelectedRow(null);
            _needsStore.ClearSelection();
        }

        protected override void OnTableQueryApplied()
        {
            SyncOrderModeFromFilters();
        }

        protected override Task OnRowDoubleTapped(TableDataRow row)
        {
            _needsStore.ClearSelection();
            _needsStore.SetSelected(row, true);
            return Task.CompletedTask;
        }

        protected override string BuildSelectedFooterText(TableDataRow row)
        {
            return $"Выбрано позиций: {_needsStore.SelectedCount}";
        }

        private async Task CreateOrderAsync()
        {
            var viewModel = new StageOrderNeedsCreateViewModel(_contragents.LoadOptionsAsync);
            var dialog = new StageOrderNeedsCreateDialog(viewModel, _needsStore.SelectedCount)
            {
                XamlRoot = XamlRoot
            };
            long? orderId = null;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    CreateOrderFromNeedsInput input = viewModel.BuildInput();
                    orderId = await _needsStore.CreateOrderFromSelectionAsync(input);
                }
                catch (Exception ex)
                {
                    dialog.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };
            await dialog.ShowAsync();
            if (!dialog.WasSaved || orderId is null)
            {
                return;
            }

            TableView.SetSelectedRow(null);
            ShowSuccessNotification($"Заказ ID {orderId} создан", "Потребность");
        }

        private async Task AddToOrderAsync()
        {
            IReadOnlyList<CbsTableFilterOptionDefinition> orders;
            try
            {
                orders = await _needsStore.LoadOrderOptionsAsync();
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось загрузить заказы.", ex.Message);
                return;
            }

            var dialog = new StageOrderNeedsAddDialog(orders, _needsStore.SelectedCount)
            {
                XamlRoot = XamlRoot
            };
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    await _needsStore.AddSelectionToOrderAsync(dialog.OrderId);
                }
                catch (Exception ex)
                {
                    dialog.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };
            await dialog.ShowAsync();
            if (!dialog.WasSaved)
            {
                return;
            }

            TableView.SetSelectedRow(null);
            ShowSuccessNotification($"Позиции добавлены в заказ ID {dialog.OrderId}", "Потребность");
        }

        private void OnNeedsStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(StageOrderNeedsStore.HasSelection)
                or nameof(StageOrderNeedsStore.SelectedCount)
                or nameof(StageOrderNeedsStore.LastSelectedRow))
            {
                UpdateButtons();
                RefreshSelectedFooterText();
            }
        }

        private void UpdateButtons()
        {
            ApplyCreateButtonState(_createOrderButton, _needsStore.HasSelection);
            ApplyCreateButtonState(_addToOrderButton, _needsStore.HasSelection);
            ApplyEditButtonState(_editPositionButton, _needsStore.LastSelectedRow is not null);
            ApplyDeleteButtonState(
                _deletePositionButton,
                _needsStore.LastSelectedRow is { } row
                && JsonDataReader.TryGetLong(row.GetValue("stage.id")) is null);
        }

        private async Task EditLastSelectedPositionAsync()
        {
            var row = _needsStore.LastSelectedRow;
            if (row is null)
            {
                return;
            }

            try
            {
                var saved = await _stageOrderEditWorkflow.ShowAsync(new StageOrderEditWorkflowRequest
                    {
                        XamlRoot = XamlRoot,
                        SourceRow = System.Text.Json.JsonSerializer.SerializeToElement(row.Values),
                        AccessMode = StageOrderEditAccessMode.Full,
                        Trace = Store.AppendUiTrace
                    });
                if (saved)
                {
                    await Store.ReloadCurrentReferenceAsync();
                    ShowSuccessNotification("Позиция изменена", "Поставка");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось открыть позицию.", ex.Message);
            }
        }

        private async Task DeleteLastSelectedPositionAsync(FrameworkElement anchor)
        {
            var row = _needsStore.LastSelectedRow;
            if (row is null)
            {
                return;
            }

            if (!await OrderConfirmationFlyout.ShowAsync(
                    anchor,
                    "Удаление позиции",
                    "Удалить выбранную позицию без привязки к этапу?",
                    "Удалить"))
            {
                return;
            }

            try
            {
                await _needsStore.DeletePositionAsync(row);
                ShowSuccessNotification("Позиция удалена", "Поставка");
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось удалить позицию.", ex.Message);
            }
        }

        private async Task ApplyShowOrdersModeAsync()
        {
            if (_isSyncingOrderMode || _showOrdersButton is null)
            {
                return;
            }

            TableView.SetSelectedRow(null);
            _needsStore.ClearSelection();
            var showOrders = _showOrdersButton.IsChecked == true;
            var isApplied = await Store.ApplyFilterAsync(
                "order_id",
                DataFilterMatchMode.In,
                showOrders ? null : new object?[] { null });
            if (!isApplied)
            {
                return;
            }

            if (ApplyOrderColumnVisibility(showOrders))
            {
                RefreshCurrentTableDefinitionView();
            }
        }

        private void SyncOrderModeFromFilters()
        {
            var showOrders = !Store.CurrentFilters.Any(filter =>
                string.Equals(filter.FieldKey, "order_id", StringComparison.OrdinalIgnoreCase));
            _isSyncingOrderMode = true;
            try
            {
                if (_showOrdersButton is not null)
                {
                    _showOrdersButton.IsChecked = showOrders;
                }
            }
            finally
            {
                _isSyncingOrderMode = false;
            }

            if (ApplyOrderColumnVisibility(showOrders))
            {
                RefreshCurrentTableDefinitionView();
            }
        }

        private bool ApplyOrderColumnVisibility(bool isVisible)
        {
            if (CurrentDefinition is null)
            {
                return false;
            }

            var hasChanges = false;
            foreach (var column in CurrentDefinition.Columns.Where(column =>
                         OrderColumnKeys.Contains(column.FieldKey, StringComparer.OrdinalIgnoreCase)))
            {
                if (column.IsVisible == isVisible)
                {
                    continue;
                }

                column.IsVisible = isVisible;
                hasChanges = true;
            }

            return hasChanges;
        }

        private static ToggleButton CreateShowOrdersButton()
        {
            var size = (double)Application.Current.Resources["ShellActionButtonSize"];
            var button = new ToggleButton
            {
                Height = size,
                MinWidth = 76,
                Padding = new Thickness(10, 2, 10, 2),
                Content = "Заказы",
                FontSize = 12,
                Foreground = Application.Current.Resources["ShellSecondaryTextBrush"] as Brush
            };
            ToolTipService.SetToolTip(button, "Отображать заказы");
            return button;
        }
    }
}
