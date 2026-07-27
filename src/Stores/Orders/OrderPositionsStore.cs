using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;

namespace CbsContractsDesktopClient.Stores.Orders
{
    public partial class OrderPositionsStore(IDataQueryService queries) : ObservableObject
    {
        private long? _orderId;
        [ObservableProperty] public partial IReadOnlyList<TableDataRow> Rows { get; private set; } = [];
        [ObservableProperty] public partial TableDataRow? SelectedRow { get; set; }
        [ObservableProperty] public partial bool IsLoading { get; private set; }

        public async Task LoadAsync(long orderId, CancellationToken cancellationToken = default)
        {
            _orderId = orderId;
            SelectedRow = null;
            Rows = [];
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
            IsLoading = false;
        }
    }
}
