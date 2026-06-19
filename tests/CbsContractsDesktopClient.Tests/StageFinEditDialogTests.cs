using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageFinEditDialogTests
{
    private static readonly string DialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "StageFinEditDialog.cs");

    [Fact]
    public void StageFinEditDialog_UsesCenteredSectionTitlesAndAccentMoney()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("public sealed class StageFinEditDialog : AppEditDialog", code);
        Assert.Contains("return BuildEditContent(body);", code);
        Assert.Contains("BuildDialogSectionTitle(RequireContract().GetSectionTitle())", code);
        Assert.Contains("BuildDialogSectionTitle(", code);
        Assert.Contains("_stage.GetSectionTitleAmount(_contract)", code);
        Assert.Contains("BuildAccentSummaryLine(\"Стоимость\", FormatMoney(_contract?.Cost))", code);
        Assert.DoesNotContain("BuildSummaryLine(\"Стоимость\", FormatMoney(_stage.Cost))", code);
    }

    [Fact]
    public void StageFinEditDialog_ReadsRequiredContractStatusDirectlyAndUsesSharedDynamicSummaryText()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("RequireContract().Status.Name!", code);
        Assert.Contains("RequireContract().Status.Id", code);
        Assert.Contains("private readonly TextBlock _startAtText = BuildDynamicSummaryText();", code);
        Assert.DoesNotContain("ResolveContractStatusName", code);
        Assert.DoesNotContain("ResolveContractStatusId", code);
        Assert.DoesNotContain("ResolveStageStatusName", code);
        Assert.DoesNotContain("ResolveStageStatusId", code);
        Assert.DoesNotContain("ResolveContractExternalNumber", code);
        Assert.Contains("_externalNumberBox.Text = _contract?.ExternalNumber ?? string.Empty;", code);
        Assert.Contains("_contract.BuildExternalNumberPayload(_externalNumberBox.Text)", code);
        Assert.DoesNotContain("private static TextBlock BuildDynamicSummaryText()", code);
    }

    [Fact]
    public void StageFinEditDialog_UsesSharedStageNavigationControls()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("StageEditDialogNavigationState? navigationState = null", code);
        Assert.Contains("Func<StageEditDialogNavigationDirection, Task<StageEditDialogNavigationResult?>>? navigateAsync = null", code);
        Assert.Contains("StageEditDialogNavigationControls.BuildTitle(", code);
        Assert.DoesNotContain("StageEditDialogNavigationControls.Build(", code);
        Assert.Contains("private async void RequestNavigation(StageEditDialogNavigationDirection direction)", code);
        Assert.Contains("Content = BuildContent();", code);
    }
}
