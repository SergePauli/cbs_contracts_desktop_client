using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow;
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

    [Fact]
    public void ApplyStatusAfterContractSigned_FillsEmptyStatusForNonPaymentStage()
    {
        var stage = StageEditState.FromRow(CreateStageRow(
            ("id", 101L),
            ("status_id", null),
            ("deadline_kind", StageDeadlineBusinessRules.DeadlineWorkingDays)));

        var changed = stage.ApplyStatusAfterContractSigned();

        Assert.True(changed);
        Assert.Equal(WorkflowStatusIds.InProgress, stage.Status.Id);
        Assert.Equal("В работе", stage.Status.Name);
        Assert.Empty(stage.Comment);
    }

    [Fact]
    public void ApplyStatusAfterContractSigned_PreservesExistingStatus()
    {
        var stage = StageEditState.FromRow(CreateStageRow(
            ("id", 102L),
            ("status_id", WorkflowStatusIds.Frozen),
            ("status.name", "Заморожен"),
            ("deadline_kind", StageDeadlineBusinessRules.DeadlineWorkingDays)));

        var changed = stage.ApplyStatusAfterContractSigned();

        Assert.False(changed);
        Assert.Equal(WorkflowStatusIds.Frozen, stage.Status.Id);
        Assert.Equal("Заморожен", stage.Status.Name);
        Assert.Empty(stage.Comment);
    }

    [Theory]
    [InlineData(StageDeadlineBusinessRules.DeadlineCalendarPrepayment)]
    [InlineData(StageDeadlineBusinessRules.DeadlineWorkingPrepayment)]
    public void ApplyStatusAfterContractSigned_PreservesEmptyStatusForPrepaymentMode(string deadlineKind)
    {
        var stage = StageEditState.FromRow(CreateStageRow(
            ("id", 103L),
            ("status_id", null),
            ("deadline_kind", deadlineKind)));

        var changed = stage.ApplyStatusAfterContractSigned();

        Assert.False(changed);
        Assert.Null(stage.Status.Id);
        Assert.Empty(stage.Comment);
    }

    [Fact]
    public void ApplyStatusAfterContractSigned_ChangesDraftStatusToSigned()
    {
        var stage = StageEditState.FromRow(CreateStageRow(
            ("id", 104L),
            ("status_id", WorkflowStatusIds.Draft),
            ("status.name", "В проекте"),
            ("deadline_kind", StageDeadlineBusinessRules.DeadlineWorkingDays)));

        var changed = stage.ApplyStatusAfterContractSigned();

        Assert.True(changed);
        Assert.Equal(WorkflowStatusIds.Signed, stage.Status.Id);
        Assert.Equal("Подписан", stage.Status.Name);
        Assert.Empty(stage.Comment);
    }

    [Fact]
    public void SetAutomationCauseAudit_ReplacesEntryForSameCauseField()
    {
        var stage = StageEditState.FromRow(CreateStageRow(("id", 105L)));

        stage.SetAutomationCauseAudit("payment_at", "Первое изменение", "-", "01.09.2026");
        stage.SetAutomationCauseAudit("payment_at", "Изменены статус и сроки этапа", "-", "02.09.2026");

        var audit = Assert.Single(stage.PendingAuditEntries);
        Assert.Equal("Stage", audit.AuditableType);
        Assert.Equal(105L, audit.AuditableId);
        Assert.Equal("payment_at", audit.AuditableField);
        Assert.Equal("updated", audit.Action);
        Assert.Equal("Изменены статус и сроки этапа", audit.Detail);
        Assert.Equal("-", audit.Before);
        Assert.Equal("02.09.2026", audit.After);
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
