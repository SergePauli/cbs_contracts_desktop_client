using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageEditStateTests
{
    [Fact]
    public void GetSectionTitle_ReturnsSimpleTitleForSingleStageContract()
    {
        var stage = StageEditState.FromRow(CreateStageRow(
            ("id", 10L),
            ("priority", 0),
            ("task_kind.name", "Поставка"),
            ("cost", 1200m)));
        var contract = ContractEditState.FromRow(CreateContractRow(
            ("id", 20L),
            ("name", "12/26/001"),
            ("status_id", 0L),
            ("status.name", "Draft"),
            ("task_kind.name", "Договор"),
            ("multyStage", false)));

        Assert.Equal("Этап", stage.GetSectionTitle(contract));
        Assert.Null(stage.GetSectionTitleAmount(contract));
    }

    [Fact]
    public void GetSectionTitle_ReturnsNumberAndTaskWithAmountForMultistageContract()
    {
        var stage = StageEditState.FromRow(CreateStageRow(
            ("id", 10L),
            ("priority", 1),
            ("task_kind.name", "Поставка"),
            ("cost", 1200m)));
        var contract = ContractEditState.FromRow(CreateContractRow(
            ("id", 20L),
            ("name", "12/26/001"),
            ("status_id", 0L),
            ("status.name", "Draft"),
            ("task_kind.name", "Договор"),
            ("multyStage", true)));

        Assert.Equal("Этап 01 Поставка", stage.GetSectionTitle(contract));
        Assert.Contains("1", stage.GetSectionTitleAmount(contract));
        Assert.Contains("руб", stage.GetSectionTitleAmount(contract));
    }

    private static TableDataRow CreateStageRow(params (string Key, object? Value)[] values)
    {
        return CreateRow(values);
    }

    private static TableDataRow CreateContractRow(params (string Key, object? Value)[] values)
    {
        return CreateRow([.. values, ("contract_responsibles", Array.Empty<object>())]);
    }

    private static TableDataRow CreateRow(params (string Key, object? Value)[] values)
    {
        return new TableDataRow
        {
            Values = values.ToDictionary(
                static value => value.Key,
                static value => JsonSerializer.SerializeToElement(value.Value))
        };
    }
}
