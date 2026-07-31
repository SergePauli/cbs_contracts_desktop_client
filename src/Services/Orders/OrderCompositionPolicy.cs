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
        if (!order.Values.ContainsKey("order_number"))
        {
            throw new InvalidOperationException("Order.list не содержит обязательное поле order_number.");
        }

        var orderNumber = order.GetValue("order_number");
        return statusId == 0 && orderNumber is null;
    }

    public static void EnsureCanModifyPositions(TableDataRow order)
    {
        if (!CanModifyPositions(order))
        {
            throw new InvalidOperationException(
                "Состав заказа нельзя изменить: заказ уже получил статус или номер счета.");
        }
    }

    public static StageOrderEditAccessMode GetPositionEditAccessMode(TableDataRow order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var statusId = JsonDataReader.TryGetLong(order.GetValue("status.id"))
            ?? throw new InvalidOperationException("Order.list не содержит обязательный status.id.");
        return statusId == 0
            ? StageOrderEditAccessMode.Full
            : StageOrderEditAccessMode.ControlFieldsOnly;
    }
}
