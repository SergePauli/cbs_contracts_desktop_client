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

    [Fact]
    public void FromRow_ReadsContractResponsibleEmployeeFullName()
    {
        var row = CreateRow(
            ("id", 20L),
            ("status_id", 1L),
            ("status.name", "Подписан"),
            ("contract_responsibles", new object[]
            {
                new
                {
                    id = 41L,
                    list_key = "responsible-41",
                    employee_id = 101L,
                    employee = new { full_name = "Иванов Иван Иванович" }
                }
            }));

        var state = ContractEditState.FromRow(row)!;

        var responsible = Assert.Single(state.ContractResponsibles);
        Assert.Equal(41L, responsible.Id);
        Assert.Equal("responsible-41", responsible.ListKey);
        Assert.Equal(101L, responsible.EmployeeId);
        Assert.Equal("Иванов Иван Иванович", responsible.FullName);
    }

    [Fact]
    public void FromRow_RejectsContractResponsibleWithoutEmployeeFullName()
    {
        var row = CreateRow(
            ("id", 20L),
            ("status_id", 1L),
            ("status.name", "Подписан"),
            ("contract_responsibles", new object[]
            {
                new
                {
                    id = 41L,
                    employee_id = 101L,
                    employee = new { name = "Иванов И.И." }
                }
            }));

        var exception = Assert.Throws<InvalidOperationException>(() => ContractEditState.FromRow(row));

        Assert.Equal("Contract responsible edit row must contain employee.full_name.", exception.Message);
    }

    private static TableDataRow CreateRow(params (string Key, object? Value)[] values)
    {
        var rowValues = values.ToDictionary(
            static value => value.Key,
            static value => JsonSerializer.SerializeToElement(value.Value));
        rowValues.TryAdd("contract_responsibles", JsonSerializer.SerializeToElement(Array.Empty<object>()));
        return new TableDataRow
        {
            Values = rowValues
        };
    }
}
