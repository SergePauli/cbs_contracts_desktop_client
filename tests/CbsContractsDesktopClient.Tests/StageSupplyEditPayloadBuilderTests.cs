using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageSupplyEditPayloadBuilderTests
{
    [Fact]
    public void Build_Create_UsesToolDefaultCostAndCalculatesCost()
    {
        var state = StageSupplyEditState.Create(42);
        var viewModel = new StageSupplyEditViewModel(
            state,
            [new IsecurityToolCatalogItem(7, "СЗИ", 1250m)]);

        Assert.True(viewModel.TrySelectTool("СЗИ"));
        viewModel.SelectedSeverity = viewModel.SeverityOptions.Single(option => option.Value == 0);
        viewModel.Amount = "2.5";

        var payload = StageSupplyEditPayloadBuilder.Build(viewModel);

        Assert.Equal(42L, payload["stage_id"]);
        Assert.Equal(state.ListKey, payload["list_key"]);
        Assert.Equal(7L, payload["isecurity_tool_id"]);
        Assert.Equal(1250m, payload["price_cost"]);
        Assert.Equal(2.5m, payload["amount"]);
        Assert.Equal(3125m, payload["cost"]);
        Assert.Equal(0, payload["severity"]);
    }

    [Fact]
    public void Build_Update_SerializesOnlyChangedAmountAndCalculatedCost()
    {
        var state = new StageSupplyEditState
        {
            IsCreateMode = false,
            StageId = 42,
            Id = 15,
            ListKey = "stage-order-key",
            ToolId = 7,
            PriceCost = 100m,
            Amount = 2m,
            Cost = 200m,
            Severity = 1
        };
        var viewModel = new StageSupplyEditViewModel(
            state,
            [new IsecurityToolCatalogItem(7, "СЗИ", 1250m)]);

        viewModel.Amount = "3";

        var payload = StageSupplyEditPayloadBuilder.Build(viewModel);

        Assert.Equal(4, payload.Count);
        Assert.Equal(15L, payload["id"]);
        Assert.Equal("stage-order-key", payload["list_key"]);
        Assert.Equal(3m, payload["amount"]);
        Assert.Equal(300m, payload["cost"]);
    }
}
