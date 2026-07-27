using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Shared.Data;

namespace CbsContractsDesktopClient.ViewModels.Workflow.EditStates
{
    public sealed class StageSupplyEditState
    {
        public required bool IsCreateMode { get; init; }
        public required long StageId { get; init; }
        public long? Id { get; init; }
        public required string ListKey { get; init; }
        public long? ToolId { get; init; }
        public decimal? PriceCost { get; init; }
        public decimal? Amount { get; init; }
        public decimal? Cost { get; init; }
        public int? Severity { get; init; }
        public string OrderNumber { get; init; } = string.Empty;

        public bool CanEdit => string.IsNullOrWhiteSpace(OrderNumber);

        public static StageSupplyEditState Create(long stageId) => new()
        {
            IsCreateMode = true,
            StageId = stageId,
            ListKey = Guid.NewGuid().ToString()
        };

        public static StageSupplyEditState FromRow(long stageId, JsonElement row)
        {
            var order = JsonDataReader.TryGetObject(row, "order");
            return new StageSupplyEditState
            {
                IsCreateMode = false,
                StageId = stageId,
                Id = JsonDataReader.TryGetLong(row, "id"),
                ListKey = JsonDataReader.TryGetString(row, "list_key")
                    ?? throw new InvalidOperationException("StageOrder.stage не содержит обязательный list_key."),
                ToolId = JsonDataReader.TryGetLong(JsonDataReader.TryGetObject(row, "isecurity_tool")?.GetProperty("id")),
                PriceCost = ParseDecimal(row, "price_cost"),
                Amount = ParseDecimal(row, "amount"),
                Cost = ParseDecimal(row, "cost"),
                Severity = JsonDataReader.TryGetInt(row, "severity"),
                OrderNumber = order is null
                    ? string.Empty
                    : JsonDataReader.TryGetString(order.Value, "order_number") ?? string.Empty
            };
        }

        private static decimal? ParseDecimal(JsonElement row, string key)
        {
            var text = JsonDataReader.TryGetString(row, key);
            return string.IsNullOrWhiteSpace(text)
                ? null
                : decimal.Parse(text, CultureInfo.InvariantCulture);
        }
    }
}
