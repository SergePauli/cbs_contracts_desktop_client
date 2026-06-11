using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContentHostDialogCoordinatorTests
{
    private static readonly string CoordinatorPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Shell",
        "ContentHostDialogCoordinator.cs");

    [Fact]
    public void Coordinator_UsesInlineFooterInsteadOfContentDialogCommandArea()
    {
        var code = File.ReadAllText(CoordinatorPath);

        Assert.Contains("BuildMessageContent(", code);
        Assert.Contains("BuildFooterButton(", code);
        Assert.Contains("PrimaryButtonText = string.Empty", code);
        Assert.Contains("CloseButtonText = string.Empty", code);
        Assert.Contains("DefaultButton = ContentDialogButton.None", code);
        Assert.DoesNotContain("ContentDialogResult.Primary", code);
    }
}
