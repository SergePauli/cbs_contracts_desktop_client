using System.Text.Json;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.Orders;
using CbsContractsDesktopClient.Stores.Orders;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class OrderCostTests
{
    [Fact]
    public void Calculate_SumsPositionCostsWithNullAsZero()
    {
        var rows = new[] { Position("12.34"), Position("null"), Position("0.66"), Position("7") };
        Assert.Equal(20m, OrderCostCalculator.Calculate(rows));
        Assert.Equal(0m, OrderCostCalculator.Calculate([]));
        Assert.Equal(0m, OrderCostCalculator.Calculate([Position("null")]));
    }

    [Fact]
    public void Calculate_RejectsMissingOrMalformedCost()
    {
        Assert.Throws<KeyNotFoundException>(() => OrderCostCalculator.Calculate([new TableDataRow()]));
        Assert.Throws<FormatException>(() => OrderCostCalculator.Calculate([Position("\"invalid\"")]));
    }

    [Fact]
    public void NewOrderPayload_IncludesCalculatedCostWithoutReadModelFields()
    {
        var cost = OrderCostCalculator.Calculate([Position("10.25"), Position("null"), Position("2.75")]);
        var payload = StageOrderNeedsPayloadBuilder.BuildForNewOrder(new CreateOrderFromNeedsInput(5, " note "), cost);
        Assert.Equal(13m, payload["cost"]);
        Assert.Equal(5L, payload["contragent_id"]);
        Assert.Equal(0L, payload["order_status_id"]);
        Assert.Equal("note", payload["description"]);
        Assert.Equal(4, payload.Count);
    }

    [Theory]
    [InlineData("12.5", "2.5")]
    [InlineData("8,25", "-1.75")]
    [InlineData("10", "0")]
    [InlineData("", null)]
    public void Difference_UsesEnteredCostAndCorrectionDoesNotMutateOriginalState(string input, string? expected)
    {
        var state = new OrderEditState { IsCreateMode = false, Cost = 7m };
        var vm = new OrderEditViewModel(state, [], (_, _) => Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>([]))
        {
            CalculatedCost = 10m,
            CostText = input
        };
        Assert.Equal(expected is null ? (decimal?)null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), vm.GetCostDifference());
        vm.ApplyCalculatedCost();
        Assert.Equal("10", vm.CostText);
        Assert.Equal(0m, vm.GetCostDifference());
        Assert.Equal(7m, state.Cost);
    }

    private static TableDataRow Position(string cost) =>
        JsonSerializer.Deserialize<TableDataRow>("{\"cost\":" + cost + "}")!;
}
