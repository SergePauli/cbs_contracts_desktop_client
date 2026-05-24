using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageCommerEditDialogTests
{
    private static readonly string DialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "StageCommerEditDialog.cs");

    [Fact]
    public void StageCommerEditDialog_LocksAutoCalculatedDeadlineFields()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("public sealed class StageCommerEditDialog : AppEditDialog", code);
        Assert.Contains("return BuildEditContent(root);", code);
        Assert.Contains("private readonly TextBox _durationBox = BuildNumberTextBox();", code);
        Assert.Contains("private readonly TextBox _paymentDurationBox = BuildNumberTextBox();", code);
        Assert.DoesNotContain("private static TextBox BuildNumberTextBox()", code);
        Assert.Contains("IsDeadlineManualMode(GetSelectedDeadlineKind())", code);
        Assert.Contains("IsPaymentDeadlineManualMode(GetSelectedPaymentDeadlineKind())", code);
        Assert.Contains("_deadlineAtEditor.IsReadOnly = !deadlineManual;", code);
        Assert.Contains("_paymentDeadlineAtEditor.IsReadOnly = !paymentDeadlineManual;", code);
    }

    [Fact]
    public void StageCommerEditDialog_ValidatesManualDeadlineDates()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("_deadlineAtEditedManually", code);
        Assert.Contains("_paymentDeadlineAtEditedManually", code);
        Assert.Contains("deadlineAt.Date < startAt.Date", code);
        Assert.Contains("paymentDeadlineAt.Date < fundedAt.Value.Date", code);
    }

    [Fact]
    public void StageCommerEditDialog_AutoClosesContractWhenLastOpenStageIsClosed()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("public bool ShouldCloseContract()", code);
        Assert.Contains("_stage.ShouldCloseContract(_contract, StatusClosed)", code);
        Assert.Contains("public IReadOnlyDictionary<string, object?> BuildContractClosePayload()", code);
        Assert.Contains("StageCommerEditPayloadBuilder.BuildContractClosePayload(RequireContract().Id, _closedAtEditor.Date)", code);
    }

    [Fact]
    public void StageCommerEditDialog_UsesCenteredSectionTitlesAndAccentMoney()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("BuildDialogSectionTitle(RequireContract().GetSectionTitle())", code);
        Assert.Contains("BuildDialogSectionTitle(", code);
        Assert.Contains("_stage.GetSectionTitleAmount(_contract)", code);
        Assert.Contains("BuildAccentSummaryLine(\"Стоимость\", FormatMoney(_contract?.Cost))", code);
        Assert.DoesNotContain("BuildSummaryLine(\"Стоимость\", FormatMoney(_stage.Cost))", code);
    }

    [Fact]
    public void StageCommerEditDialog_ReadsRequiredContractStatusDirectly()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("RequireContract().Status.Name!", code);
        Assert.Contains("RequireContract().Status.Id", code);
        Assert.DoesNotContain("ResolveContractStatusName", code);
        Assert.DoesNotContain("ResolveContractStatusId", code);
        Assert.DoesNotContain("FindStatusLabel(_statusOptions", code);
    }
}
