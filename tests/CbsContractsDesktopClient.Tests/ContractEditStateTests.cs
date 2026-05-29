using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContractEditStateTests
{
    [Fact]
    public void FromRow_RequiresContractStatusId()
    {
        var row = CreateRow(
            ("id", 20L),
            ("name", "12/26/001"),
            ("status.name", "Draft"),
            ("task_kind.name", "Task"));

        var exception = Assert.Throws<InvalidOperationException>(() => ContractEditState.FromRow(row));

        Assert.Equal("Contract edit row must contain status_id or status.id.", exception.Message);
    }

    [Fact]
    public void FromRow_RequiresContractStatusName()
    {
        var row = CreateRow(
            ("id", 20L),
            ("name", "12/26/001"),
            ("status_id", 0L),
            ("task_kind.name", "Договор"));

        var exception = Assert.Throws<InvalidOperationException>(() => ContractEditState.FromRow(row));

        Assert.Equal("Contract edit row must contain status.name.", exception.Message);
    }

    [Fact]
    public void FromRow_KeepsRequiredContractStatusForDirectDialogDisplay()
    {
        var row = CreateRow(
            ("id", 20L),
            ("name", "12/26/001"),
            ("status_id", 0L),
            ("status.name", "В проекте"),
            ("task_kind.name", "Договор"));

        var state = ContractEditState.FromRow(row);

        Assert.NotNull(state);
        Assert.Equal(0L, state!.Status.Id);
        Assert.Equal("В проекте", state.Status.Name);
    }

    [Fact]
    public void GetSectionTitle_UsesContractNameAndTaskKindName()
    {
        var row = CreateRow(
            ("id", 20L),
            ("name", "12/26/001"),
            ("status_id", 0L),
            ("status.name", "Draft"),
            ("task_kind.name", "Task"));

        var state = ContractEditState.FromRow(row);

        Assert.NotNull(state);
        var title = state!.GetSectionTitle();
        Assert.Contains("12/26/001", title);
        Assert.Contains("Task", title);
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
