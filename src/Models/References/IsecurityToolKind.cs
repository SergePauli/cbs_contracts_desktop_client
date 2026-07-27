namespace CbsContractsDesktopClient.Models.References
{
    public enum IsecurityToolKind
    {
        Hardware = 0,
        Software = 1,
        Other = 2
    }

    public static class IsecurityToolKindText
    {
        public static string GetLabel(IsecurityToolKind kind)
        {
            return kind switch
            {
                IsecurityToolKind.Hardware => "Оборудование",
                IsecurityToolKind.Software => "ПО",
                IsecurityToolKind.Other => "Прочее",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный тип товара СЗИ.")
            };
        }

        public static IsecurityToolKind Parse(long value)
        {
            return value switch
            {
                (long)IsecurityToolKind.Hardware => IsecurityToolKind.Hardware,
                (long)IsecurityToolKind.Software => IsecurityToolKind.Software,
                (long)IsecurityToolKind.Other => IsecurityToolKind.Other,
                _ => throw new InvalidOperationException($"IsecurityTool.kind содержит недопустимое значение {value}.")
            };
        }
    }
}
