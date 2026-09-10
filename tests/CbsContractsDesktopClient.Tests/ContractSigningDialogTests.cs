using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContractSigningDialogTests
{
    [Fact]
    public void SignedDate_AppliesStartAndStatusRulesToEveryNonDestroyedStage()
    {
        var code = File.ReadAllText(TestProjectPaths.FromRepositoryRoot(
            "src",
            "Views",
            "Functional",
            "ContractCommerEditDialog.cs"));

        Assert.Contains("ApplyContractSignedDateToEmptyStageStarts();", code);
        Assert.Contains("ApplyContractSignedStatusToEmptyStageStatuses();", code);
        Assert.Contains("stage.ApplyStatusAfterContractSigned();", code);
        Assert.Contains("StageDeadlineBusinessRules.ResolveStartAfterContractSigned", code);
        Assert.Contains("BuildContractSignedAutomationDetail", code);
        Assert.Contains("SetAutomationCauseAudit", code);
    }
}
