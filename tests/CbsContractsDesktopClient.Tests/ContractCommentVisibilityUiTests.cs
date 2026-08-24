using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContractCommentVisibilityUiTests
{
    private static readonly string ContractDetailXamlPath = TestProjectPaths.FromRepositoryRoot(
        "src", "Views", "Functional", "ContractDetailView.xaml");
    private static readonly string ContractDetailCodePath = TestProjectPaths.FromRepositoryRoot(
        "src", "Views", "Functional", "ContractDetailView.xaml.cs");
    private static readonly string StageHostPath = TestProjectPaths.FromRepositoryRoot(
        "src", "Views", "Shell", "StageHostView.cs");

    [Fact]
    public void ContractCommentToggle_IsAvailableOnlyWhenStageHostEnablesIt()
    {
        var xaml = File.ReadAllText(ContractDetailXamlPath);
        var detailCode = File.ReadAllText(ContractDetailCodePath);
        var stageHost = File.ReadAllText(StageHostPath);

        Assert.Contains("x:Name=\"ContractCommentsToggleButton\"", xaml);
        Assert.Contains("Visibility=\"Collapsed\"", xaml);
        Assert.Contains("public bool AllowContractCommentsToggle", detailCode);
        Assert.Contains("AllowContractCommentsToggle = true", stageHost);
    }

    [Fact]
    public void ContractCommentToggle_UsesWindows10LeaveChatGlyph()
    {
        var xaml = File.ReadAllText(ContractDetailXamlPath);

        Assert.Contains("Content=\"&#xE89B;\"", xaml);
        Assert.Contains("FontFamily=\"Segoe MDL2 Assets\"", xaml);
        Assert.Contains("Width=\"{StaticResource ShellActionButtonSize}\"", xaml);
        Assert.Contains("Height=\"{StaticResource ShellActionButtonSize}\"", xaml);
        Assert.Contains("FontSize=\"16\"", xaml);
        Assert.Contains("IsChecked=\"True\"", xaml);
    }

    [Fact]
    public void ContractCommentToggle_IsDisabledUntilDetailRenderCompletes()
    {
        var xaml = File.ReadAllText(ContractDetailXamlPath);
        var detailCode = File.ReadAllText(ContractDetailCodePath);

        Assert.Contains("IsEnabled=\"False\"", xaml);
        Assert.Contains("ContractCommentsToggleButton.IsEnabled = false;", detailCode);
        Assert.Contains("RenderComments();", detailCode);
        Assert.Contains("ContractCommentsToggleButton.IsEnabled = true;", detailCode);
        Assert.True(
            detailCode.IndexOf("ContractCommentsToggleButton.IsEnabled = false;", StringComparison.Ordinal)
            < detailCode.IndexOf("RenderComments();", StringComparison.Ordinal));
        Assert.True(
            detailCode.IndexOf("RenderComments();", StringComparison.Ordinal)
            < detailCode.IndexOf("ContractCommentsToggleButton.IsEnabled = true;", StringComparison.Ordinal));
    }

    [Fact]
    public void ContractCommentToggle_FiltersOnlyContractCommentsWithoutChangingStore()
    {
        var detailCode = File.ReadAllText(ContractDetailCodePath);

        Assert.Contains("_showContractComments = ContractCommentsToggleButton.IsChecked == true;", detailCode);
        Assert.Contains("comment.GetValue(\"commentable_type\")", detailCode);
        Assert.Contains("\"Contract\"", detailCode);
        Assert.Contains("CommentsBox.Comments = _showContractComments", detailCode);
        Assert.DoesNotContain("_contractWorkflowStore.Comments =", detailCode);
    }
}
