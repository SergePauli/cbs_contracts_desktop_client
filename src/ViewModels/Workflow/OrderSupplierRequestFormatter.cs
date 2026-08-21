using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Data;

namespace CbsContractsDesktopClient.ViewModels.Workflow;

public static class OrderSupplierRequestFormatter
{
    public static IReadOnlyList<OrderSupplierRequestLine> BuildLines(IReadOnlyList<TableDataRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var aggregates = new Dictionary<long, OrderSupplierRequestLine>();
        foreach (var row in rows)
        {
            var toolId = JsonDataReader.TryGetLong(row.GetValue("isecurity_tool.id"))
                ?? throw new InvalidOperationException("В позиции заказа отсутствует isecurity_tool.id.");
            var name = JsonDataReader.TryGetText(row, "isecurity_tool.name")
                ?? throw new InvalidOperationException($"У СЗИ ID {toolId} отсутствует isecurity_tool.name.");
            var unit = JsonDataReader.TryGetText(row, "isecurity_tool.unit")
                ?? throw new InvalidOperationException($"У СЗИ ID {toolId} отсутствует isecurity_tool.unit.");
            var amount = ReadAmount(row.GetValue("amount"), toolId);

            if (aggregates.TryGetValue(toolId, out var existing))
            {
                if (!string.Equals(existing.Name, name, StringComparison.Ordinal)
                    || !string.Equals(existing.Unit, unit, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Позиции СЗИ ID {toolId} содержат разные названия или единицы измерения.");
                }

                aggregates[toolId] = existing with { Amount = existing.Amount + amount };
                continue;
            }

            aggregates.Add(toolId, new OrderSupplierRequestLine(toolId, name, unit, amount));
        }

        return aggregates.Values
            .OrderBy(static line => line.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static string BuildClipboardText(IReadOnlyList<OrderSupplierRequestLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return string.Join(
            Environment.NewLine,
            lines.Select(static line =>
                $"{line.Name} — {line.Amount.ToString("0.##", CultureInfo.CurrentCulture)} {line.Unit}"));
    }

    private static decimal ReadAmount(object? value, long toolId)
    {
        var amount = value switch
        {
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetDecimal(out var number) => number,
            JsonElement { ValueKind: JsonValueKind.String } element
                when decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) => number,
            decimal number => number,
            long number => number,
            int number => number,
            double number => Convert.ToDecimal(number, CultureInfo.InvariantCulture),
            string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) => number,
            _ => throw new InvalidOperationException($"У позиции СЗИ ID {toolId} отсутствует корректное amount.")
        };

        return amount;
    }
}

public sealed record OrderSupplierRequestLine(long ToolId, string Name, string Unit, decimal Amount);
