using System.Globalization;
using CbsContractsDesktopClient.Shared.Data;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public static class OrderEditPayloadBuilder
    {
        public static IReadOnlyDictionary<string, object?> BuildForCreate(OrderEditViewModel viewModel) => Build(viewModel, true);
        public static IReadOnlyDictionary<string, object?> BuildForUpdate(OrderEditViewModel viewModel) => Build(viewModel, false);

        private static IReadOnlyDictionary<string, object?> Build(OrderEditViewModel vm, bool create)
        {
            var number = vm.OrderNumber.Trim();
            var contragentId = JsonDataReader.TryGetLong(vm.SelectedContragent?.Value);
            var statusId = JsonDataReader.TryGetLong(vm.SelectedStatus?.Value);
            if (string.IsNullOrWhiteSpace(number) || contragentId is null || statusId is null)
                throw new InvalidOperationException("Номер, поставщик и статус обязательны.");

            var state = vm.State;
            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (!create) payload["id"] = state.Id ?? throw new InvalidOperationException("Order.id отсутствует.");
            Add(payload, "order_number", number, state.OrderNumber, create);
            Add(payload, "contragent_id", contragentId, state.ContragentId, create);
            Add(payload, "order_status_id", statusId, state.OrderStatusId, create);
            Add(payload, "cost", ParseCost(vm.CostText), state.Cost, create);
            Add(payload, "requested_at", FormatDate(vm.RequestedAt), FormatDate(state.RequestedAt), create);
            Add(payload, "ordered_at", FormatDate(vm.OrderedAt), FormatDate(state.OrderedAt), create);
            Add(payload, "payment_at", FormatDate(vm.PaymentAt), FormatDate(state.PaymentAt), create);
            Add(payload, "received_at", FormatDate(vm.ReceivedAt), FormatDate(state.ReceivedAt), create);
            Add(payload, "description", NullIfEmpty(vm.Description), NullIfEmpty(state.Description), create);
            return payload;
        }

        private static void Add(Dictionary<string, object?> payload, string key, object? value, object? original, bool create)
        {
            if ((create && value is not null) || (!create && !Equals(value, original))) payload[key] = value;
        }
        private static decimal? ParseCost(string value) => string.IsNullOrWhiteSpace(value) ? null : decimal.Parse(value.Replace(',', '.'), CultureInfo.InvariantCulture);
        private static string? FormatDate(DateTimeOffset? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
