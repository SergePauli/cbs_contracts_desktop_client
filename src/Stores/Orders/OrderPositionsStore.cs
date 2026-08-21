using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.Orders;
using CbsContractsDesktopClient.Shared.Data;

namespace CbsContractsDesktopClient.Stores.Orders
{
    public partial class OrderPositionsStore(
        IDataQueryService queries,
        IModelMutationService mutations) : ObservableObject
    {
        private long? _orderId;
        [ObservableProperty] public partial IReadOnlyList<TableDataRow> Rows { get; private set; } = [];
        [ObservableProperty] public partial TableDataRow? SelectedRow { get; set; }
        [ObservableProperty] public partial bool IsLoading { get; private set; }
        [ObservableProperty] public partial bool IsLoaded { get; private set; }

        public async Task LoadAsync(long orderId, CancellationToken cancellationToken = default)
        {
            _orderId = orderId;
            SelectedRow = null;
            Rows = [];
            IsLoaded = false;
            IsLoading = true;
            try
            {
                var rows = await queries.GetDataAsync<TableDataRow>(new DataQueryRequest
                {
                    Model = "StageOrder",
                    Preset = "order",
                    Filters = new Dictionary<string, object?> { ["order_id"] = orderId },
                    Sorts = ["priority asc"],
                    Limit = 1000
                }, cancellationToken);
                if (_orderId == orderId)
                {
                    Rows = rows;
                    IsLoaded = true;
                }
            }
            finally
            {
                if (_orderId == orderId)
                {
                    IsLoading = false;
                }
            }
        }

        public void Clear()
        {
            _orderId = null;
            SelectedRow = null;
            Rows = [];
            IsLoaded = false;
            IsLoading = false;
        }

        public async Task UnlinkPositionAsync(
            TableDataRow order,
            TableDataRow position,
            CancellationToken cancellationToken = default)
        {
            OrderCompositionPolicy.EnsureCanModifyPositions(order);
            var orderId = RequireOrderId(order);
            EnsureLoadedOrder(orderId);
            await UnlinkAsync(position, cancellationToken);
            await LoadAsync(orderId, cancellationToken);
        }

        public async Task<long> DeleteOrderAsync(
            TableDataRow order,
            CancellationToken cancellationToken = default)
        {
            var orderId = RequireOrderId(order);
            EnsureLoadedOrder(orderId);
            foreach (var position in Rows)
            {
                await UnlinkAsync(position, cancellationToken);
            }

            await mutations.DeleteAsync("Order", orderId, cancellationToken);
            Clear();
            return orderId;
        }

        private async Task UnlinkAsync(TableDataRow position, CancellationToken cancellationToken)
        {
            var id = JsonDataReader.TryGetLong(position.GetValue("id"))
                ?? throw new InvalidOperationException("StageOrder.order не содержит обязательный id.");
            var listKey = JsonDataReader.TryGetText(position, "list_key")
                ?? throw new InvalidOperationException($"StageOrder.order ID {id} не содержит обязательный list_key.");
            var payload = StageOrderLinkPayloadBuilder.Build(id, listKey, orderId: null);
            await mutations.UpdateAsync("StageOrder", payload, cancellationToken);
        }

        private static long RequireOrderId(TableDataRow order)
        {
            return JsonDataReader.TryGetLong(order.GetValue("id"))
                ?? throw new InvalidOperationException("Order.list не содержит обязательный id.");
        }

        private void EnsureLoadedOrder(long orderId)
        {
            if (_orderId != orderId || !IsLoaded)
            {
                throw new InvalidOperationException("Позиции выбранного заказа еще не загружены.");
            }
        }
    }
}
