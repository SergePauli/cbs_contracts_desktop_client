using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Services.Orders;
using CbsContractsDesktopClient.Shared.Data;

namespace CbsContractsDesktopClient.ViewModels.Workflow.EditStates
{
    public sealed class StageOrderEditState
    {
        public required bool IsCreateMode { get; init; }
        public StageOrderEditAccessMode AccessMode { get; init; }
        public long? OrderId { get; init; }
        public bool IsStageFixed { get; init; }
        public long? Id { get; init; }
        public required string ListKey { get; init; }
        public long? StageId { get; init; }
        public string StageName { get; init; } = string.Empty;
        public long? ToolId { get; init; }
        public string ToolName { get; init; } = string.Empty;
        public int? Severity { get; init; }
        public decimal? PriceCost { get; init; }
        public decimal? Amount { get; init; }
        public decimal? Cost { get; init; }
        public int? Priority { get; init; }
        public string Description { get; init; } = string.Empty;

        public static StageOrderEditState Create(long? orderId, long? stageId, bool isStageFixed) => new()
        {
            IsCreateMode = true,
            AccessMode = StageOrderEditAccessMode.Full,
            OrderId = orderId,
            StageId = stageId,
            IsStageFixed = isStageFixed,
            ListKey = Guid.NewGuid().ToString()
        };

        public static StageOrderEditState FromRow(StageOrderEditWorkflowRequest request, JsonElement row) => new()
        {
            IsCreateMode = false,
            AccessMode = request.AccessMode,
            OrderId = request.OrderId
                ?? JsonDataReader.TryGetLong(JsonDataReader.TryGetObject(row, "order") ?? default, "id"),
            IsStageFixed = request.IsStageFixed,
            Id = JsonDataReader.TryGetLong(row, "id"),
            ListKey = JsonDataReader.TryGetString(row, "list_key")
                ?? throw new InvalidOperationException("StageOrder.order не содержит обязательный list_key."),
            StageId = request.StageId
                ?? JsonDataReader.TryGetLong(JsonDataReader.TryGetObject(row, "stage") ?? default, "id"),
            StageName = JsonDataReader.TryGetString(JsonDataReader.TryGetObject(row, "stage") ?? default, "name") ?? string.Empty,
            ToolId = JsonDataReader.TryGetLong(JsonDataReader.TryGetObject(row, "isecurity_tool")?.GetProperty("id")),
            ToolName = JsonDataReader.TryGetString(JsonDataReader.TryGetObject(row, "isecurity_tool") ?? default, "name") ?? string.Empty,
            Severity = JsonDataReader.TryGetInt(row, "severity"),
            PriceCost = Decimal(row, "price_cost"), Amount = Decimal(row, "amount"), Cost = Decimal(row, "cost"),
            Priority = JsonDataReader.TryGetInt(row, "priority"),
            Description = JsonDataReader.TryGetString(row, "description") ?? string.Empty
        };

        private static decimal? Decimal(JsonElement row, string key)
        {
            var text = JsonDataReader.TryGetString(row, key);
            return string.IsNullOrWhiteSpace(text) ? null : decimal.Parse(text, CultureInfo.InvariantCulture);
        }
    }
}
