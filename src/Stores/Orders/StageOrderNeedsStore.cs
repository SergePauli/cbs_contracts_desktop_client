using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.Orders;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.Stores.Table;

namespace CbsContractsDesktopClient.Stores.Orders
{
    public partial class StageOrderNeedsStore(
        IModelMutationService mutations,
        IDataQueryService queries,
        TablePageStore tablePageStore) : ObservableObject
    {
        private readonly Dictionary<long, StageOrderNeedSelection> _selection = [];
        private readonly Dictionary<long, TableDataRow> _selectedRows = [];
        private readonly List<long> _selectionOrder = [];

        public int SelectedCount => _selection.Count;

        public bool HasSelection => _selection.Count > 0;

        public TableDataRow? LastSelectedRow =>
            _selectionOrder.Count == 0
                ? null
                : _selectedRows[_selectionOrder[^1]];

        public async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadOrderOptionsAsync(
            CancellationToken cancellationToken = default)
        {
            var rows = await queries.GetDataAsync<TableDataRow>(new DataQueryRequest
            {
                Model = "Order",
                Preset = "item",
                Filters = new Dictionary<string, object?>
                {
                    ["order_status_id"] = 0L
                },
                Sorts = ["id desc"],
                Limit = 1000
            }, cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(static row =>
                {
                    var id = JsonDataReader.TryGetLong(row.GetValue("id"))
                        ?? throw new InvalidOperationException("Order.item не содержит обязательный id.");
                    var name = row.GetValue("name")?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        throw new InvalidOperationException($"Order.item ID {id} не содержит обязательное имя.");
                    }

                    return new CbsTableFilterOptionDefinition
                    {
                        Value = id,
                        Label = name
                    };
                })
                .ToList();
        }

        public void SetSelected(TableDataRow row, bool isSelected)
        {
            var id = JsonDataReader.TryGetLong(row.GetValue("id"))
                ?? throw new InvalidOperationException("StageOrder.card не содержит обязательный id.");
            var listKey = row.GetValue("list_key")?.ToString();
            if (string.IsNullOrWhiteSpace(listKey))
            {
                throw new InvalidOperationException($"StageOrder.card ID {id} не содержит обязательный list_key.");
            }

            if (isSelected)
            {
                _selection[id] = new StageOrderNeedSelection(id, listKey);
                _selectedRows[id] = row;
                _selectionOrder.Remove(id);
                _selectionOrder.Add(id);
            }
            else
            {
                _selection.Remove(id);
                _selectedRows.Remove(id);
                _selectionOrder.Remove(id);
            }

            NotifySelectionChanged();
        }

        public void ClearSelection()
        {
            if (_selection.Count == 0)
            {
                return;
            }

            _selection.Clear();
            _selectedRows.Clear();
            _selectionOrder.Clear();
            NotifySelectionChanged();
        }

        public async Task DeletePositionAsync(
            TableDataRow row,
            CancellationToken cancellationToken = default)
        {
            if (JsonDataReader.TryGetLong(row.GetValue("stage.id")) is not null)
            {
                throw new InvalidOperationException("Позицию, привязанную к этапу, удалить нельзя.");
            }

            var id = JsonDataReader.TryGetLong(row.GetValue("id"))
                ?? throw new InvalidOperationException("StageOrder.card не содержит обязательный id.");
            await mutations.DeleteAsync("StageOrder", id, cancellationToken);
            ClearSelection();
            await tablePageStore.ReloadCurrentReferenceAsync(cancellationToken);
        }

        public async Task<long> CreateOrderFromSelectionAsync(
            CreateOrderFromNeedsInput input,
            CancellationToken cancellationToken = default)
        {
            var selection = GetSelection();
            var payload = StageOrderNeedsPayloadBuilder.BuildForNewOrder(input);
            var saved = await mutations.CreateAsync("Order", payload, cancellationToken);
            var orderId = JsonDataReader.TryGetLong(saved.GetValue("id"))
                ?? throw new InvalidOperationException("Mutation Order не вернула идентификатор созданного заказа.");
            await AttachSelectionAsync(selection, orderId, cancellationToken);
            await ReloadNeedsAsync(cancellationToken);
            return orderId;
        }

        public async Task AddSelectionToOrderAsync(
            long orderId,
            CancellationToken cancellationToken = default)
        {
            await EnsureOrderAcceptsPositionsAsync(orderId, cancellationToken);
            await AttachSelectionAsync(GetSelection(), orderId, cancellationToken);
            await ReloadNeedsAsync(cancellationToken);
        }

        private async Task EnsureOrderAcceptsPositionsAsync(
            long orderId,
            CancellationToken cancellationToken)
        {
            var rows = await queries.GetDataAsync<TableDataRow>(new DataQueryRequest
            {
                Model = "Order",
                Preset = "list",
                Filters = new Dictionary<string, object?> { ["id"] = orderId },
                Limit = 1
            }, cancellationToken);
            var order = rows.SingleOrDefault()
                ?? throw new InvalidOperationException($"Order.list не вернул заказ ID {orderId}.");
            OrderCompositionPolicy.EnsureCanModifyPositions(order);
        }

        private IReadOnlyCollection<StageOrderNeedSelection> GetSelection()
        {
            if (_selection.Count == 0)
            {
                throw new InvalidOperationException("Не выбраны позиции потребности.");
            }

            return _selection.Values.OrderBy(static position => position.Id).ToList();
        }

        private async Task AttachSelectionAsync(
            IReadOnlyCollection<StageOrderNeedSelection> selection,
            long orderId,
            CancellationToken cancellationToken)
        {
            foreach (var position in selection)
            {
                var payload = StageOrderNeedsPayloadBuilder.BuildStageOrderUpdate(position, orderId);
                await mutations.UpdateAsync("StageOrder", payload, cancellationToken);
            }
        }

        private async Task ReloadNeedsAsync(CancellationToken cancellationToken)
        {
            ClearSelection();
            await tablePageStore.ReloadCurrentReferenceAsync(cancellationToken);
        }

        private void NotifySelectionChanged()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(LastSelectedRow));
        }
    }
}
