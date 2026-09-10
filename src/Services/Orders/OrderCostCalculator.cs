using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.Orders;

public static class OrderCostCalculator
{
    public static decimal Calculate(IEnumerable<TableDataRow> positions) => positions.Sum(position =>
    {
        var cost = position.Values["cost"];
        return cost.ValueKind == JsonValueKind.Null
            ? 0m
            : decimal.Parse(cost.ToString(), CultureInfo.InvariantCulture);
    });
}
