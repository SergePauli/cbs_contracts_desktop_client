using CbsContractsDesktopClient.Models.Orders;

namespace CbsContractsDesktopClient.Stores.Orders
{
    public static class StageOrderNeedsPayloadBuilder
    {
        public static IReadOnlyDictionary<string, object?> BuildForNewOrder(
            CreateOrderFromNeedsInput input)
        {
            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["contragent_id"] = input.ContragentId,
                ["order_status_id"] = 0L
            };
            if (!string.IsNullOrWhiteSpace(input.Description))
            {
                payload["description"] = input.Description.Trim();
            }

            return payload;
        }

        public static IReadOnlyDictionary<string, object?> BuildStageOrderUpdate(
            StageOrderNeedSelection position,
            long orderId)
        {
            return StageOrderLinkPayloadBuilder.Build(position.Id, position.ListKey, orderId);
        }
    }
}
