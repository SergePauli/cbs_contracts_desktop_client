using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Data;

namespace CbsContractsDesktopClient.Services.Orders;

public static class OrderCompositionPolicy
{
    public static bool CanModifyPositions(TableDataRow order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var statusId = JsonDataReader.TryGetLong(order.GetValue("status.id"))
            ?? throw new InvalidOperationException("Order.list не содержит обязательный status.id.");
        return statusId == 0;
    }

    public static void EnsureCanModifyPositions(TableDataRow order)
    {
        if (!CanModifyPositions(order))
        {
            throw new InvalidOperationException(
                "Состав заказа нельзя изменить при текущем статусе заказа.");
        }
    }

    public static bool CanModifyPosition(TableDataRow position)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (JsonDataReader.TryGetLong(position.GetValue("order.id")) is null)
        {
            return true;
        }

        var statusId = JsonDataReader.TryGetLong(position.GetValue("order.status.id"))
            ?? throw new InvalidOperationException("StageOrder.stage order не содержит обязательный status.id.");
        return statusId == 0;
    }

    public static void EnsureCanModifyPosition(TableDataRow position)
    {
        if (!CanModifyPosition(position))
        {
            throw new InvalidOperationException(
                "Позицию нельзя изменить при текущем статусе заказа.");
        }
    }

}
