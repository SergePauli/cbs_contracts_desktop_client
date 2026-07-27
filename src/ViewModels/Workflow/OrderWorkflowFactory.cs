using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public sealed class OrderWorkflowFactory
    {
        private readonly IDataQueryService _dataQueryService;

        public OrderWorkflowFactory(IDataQueryService dataQueryService)
        {
            _dataQueryService = dataQueryService;
        }

        public async Task<TableDataRow> LoadOrderCardAsync(long orderId, CancellationToken cancellationToken = default)
        {
            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = "Order",
                    Preset = "card",
                    Filters = new Dictionary<string, object?> { ["id"] = orderId },
                    Limit = 1
                },
                cancellationToken);

            return rows.SingleOrDefault(static row => !row.IsPlaceholder)
                ?? throw new InvalidOperationException($"Order.card не вернул заказ ID {orderId}.");
        }
    }
}
