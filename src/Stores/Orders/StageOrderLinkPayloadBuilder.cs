namespace CbsContractsDesktopClient.Stores.Orders;

public static class StageOrderLinkPayloadBuilder
{
    public static IReadOnlyDictionary<string, object?> Build(
        long id,
        string listKey,
        long? orderId)
    {
        if (string.IsNullOrWhiteSpace(listKey))
        {
            throw new InvalidOperationException($"StageOrder ID {id} не содержит обязательный list_key.");
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = id,
            ["list_key"] = listKey,
            ["order_id"] = orderId
        };
    }
}
