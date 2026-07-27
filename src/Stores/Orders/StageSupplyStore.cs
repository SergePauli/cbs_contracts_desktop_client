using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Shared.Data;

namespace CbsContractsDesktopClient.Stores.Orders
{
    public partial class StageSupplyStore(IDataQueryService queries, long stageId) : ObservableObject
    {
        [ObservableProperty] public partial IReadOnlyList<TableDataRow> Rows { get; private set; } = [];
        [ObservableProperty] public partial TableDataRow? SelectedRow { get; set; }
        [ObservableProperty] public partial bool IsLoading { get; private set; }

        public long StageId { get; } = stageId;

        public async Task ReloadAsync(long? expectedRowId = null, CancellationToken cancellationToken = default)
        {
            IsLoading = true;
            try
            {
                var cards = await queries.GetDataAsync<TableDataRow>(new DataQueryRequest
                {
                    Model = "Stage",
                    Preset = "card",
                    Filters = new Dictionary<string, object?> { ["id__eq"] = StageId },
                    Limit = 1
                }, cancellationToken);
                var card = cards.SingleOrDefault(static row => !row.IsPlaceholder)
                    ?? throw new InvalidOperationException($"Stage.card не вернул этап ID {StageId}.");
                var stageOrders = JsonDataReader.TryGetArray(card, "stage_orders");
                Rows = stageOrders is null
                    ? []
                    : stageOrders.Value.EnumerateArray()
                    .Select(static item => item.Deserialize<TableDataRow>()
                        ?? throw new InvalidOperationException("StageOrder.stage содержит некорректную строку."))
                    .ToList();
                if (expectedRowId is not null
                    && !Rows.Any(row => JsonDataReader.TryGetLong(row.GetValue("id")) == expectedRowId))
                {
                    throw new InvalidOperationException(
                        $"Stage.card.stage_orders не вернул сохраненную позицию StageOrder ID {expectedRowId}.");
                }
                SelectedRow = null;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
