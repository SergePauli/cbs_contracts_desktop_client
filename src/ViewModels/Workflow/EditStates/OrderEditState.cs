using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Data;

namespace CbsContractsDesktopClient.ViewModels.Workflow.EditStates
{
    public sealed class OrderEditState
    {
        public required bool IsCreateMode { get; init; }
        public long? Id { get; init; }
        public string OrderNumber { get; init; } = string.Empty;
        public long? ContragentId { get; init; }
        public string ContragentName { get; init; } = string.Empty;
        public long? OrderStatusId { get; init; }
        public string OrderStatusName { get; init; } = string.Empty;
        public decimal? Cost { get; init; }
        public DateTimeOffset? RequestedAt { get; init; }
        public DateTimeOffset? OrderedAt { get; init; }
        public DateTimeOffset? PaymentAt { get; init; }
        public DateTimeOffset? ReceivedAt { get; init; }
        public string Description { get; init; } = string.Empty;

        public static OrderEditState CreateNew() => new() { IsCreateMode = true };

        public static OrderEditState FromCard(TableDataRow row)
        {
            return new OrderEditState
            {
                IsCreateMode = false,
                Id = JsonDataReader.TryGetLong(row.GetValue("id")),
                OrderNumber = JsonDataReader.GetText(row, "order_number"),
                ContragentId = JsonDataReader.TryGetLong(row.GetValue("supplier.id")),
                ContragentName = JsonDataReader.GetText(row, "supplier.name"),
                OrderStatusId = JsonDataReader.TryGetLong(row.GetValue("status.id")),
                OrderStatusName = JsonDataReader.GetText(row, "status.name"),
                Cost = ParseDecimal(row.GetValue("cost")),
                RequestedAt = ParseDate(row.GetValue("requested_at")),
                OrderedAt = ParseDate(row.GetValue("ordered_at")),
                PaymentAt = ParseDate(row.GetValue("payment_at")),
                ReceivedAt = ParseDate(row.GetValue("received_at")),
                Description = JsonDataReader.GetText(row, "description")
            };
        }

        private static decimal? ParseDecimal(object? value) => value is null ? null : Convert.ToDecimal(value);
        private static DateTimeOffset? ParseDate(object? value) => value is null ? null : DateTimeOffset.Parse(value.ToString()!);
    }
}
