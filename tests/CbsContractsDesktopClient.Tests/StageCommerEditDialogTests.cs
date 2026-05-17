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
        Assert.Contains("IsLastOpenStageInContract()", code);
        Assert.Contains("public IReadOnlyDictionary<string, object?> BuildContractClosePayload()", code);
        Assert.Contains("StageCommerEditPayloadBuilder.BuildContractClosePayload(contractId, _closedAtEditor.Date)", code);
    }
}
