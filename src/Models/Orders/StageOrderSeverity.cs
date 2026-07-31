namespace CbsContractsDesktopClient.Models.Orders
{
    public enum StageOrderSeverity
    {
        Need = 0,
        InStock = 1,
        OnControl = 2,
        NotApproved = 3,
        Delivered = 4
    }

    public static class StageOrderSeverityText
    {
        public static string GetLabel(StageOrderSeverity severity)
        {
            return severity switch
            {
                StageOrderSeverity.Need => "Потребность",
                StageOrderSeverity.InStock => "В наличии",
                StageOrderSeverity.OnControl => "На контроле",
                StageOrderSeverity.NotApproved => "Не согласовано",
                StageOrderSeverity.Delivered => "Поставлено",
                _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Неизвестная важность позиции заказа.")
            };
        }

        public static StageOrderSeverity Parse(int value)
        {
            return value is >= 0 and <= 4
                ? (StageOrderSeverity)value
                : throw new InvalidOperationException($"StageOrder.severity содержит недопустимое значение {value}.");
        }
    }
}
