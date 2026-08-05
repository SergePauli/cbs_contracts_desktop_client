using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class DialogChromeTests
{
    private static readonly string DialogChromePath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Controls",
        "DialogChrome.cs");

    [Fact]
    public void DialogChrome_DoesNotStyleContentDialogFooterButtons()
    {
        var code = File.ReadAllText(DialogChromePath);

        Assert.DoesNotContain("PrimaryButtonStyle", code);
        Assert.DoesNotContain("SecondaryButtonStyle", code);
        Assert.DoesNotContain("CloseButtonStyle", code);
        Assert.DoesNotContain("ApplyFooterButtonStyles", code);
    }

    [Fact]
    public void DialogChrome_OnlyAppliesChromeAndWrapsContent()
    {
        var code = File.ReadAllText(DialogChromePath);

        Assert.Contains("ApplyCompactResources(dialog);", code);
        Assert.Contains("EnsureContentMargin(dialog);", code);
        Assert.Contains("dialog.Title = BuildTitle(dialog, title);", code);
        Assert.Contains("private static void WrapContent(ContentDialog dialog)", code);
        Assert.Contains("private static UIElement BuildTitle(ContentDialog dialog, string title)", code);
    }

    [Fact]
    public void DialogChrome_BindsStaticTitleLayoutToEditDialogTitleState()
    {
        var code = File.ReadAllText(DialogChromePath);

        Assert.Contains("editDialog.DialogTitle = title;", code);
        Assert.Contains("titleBlock.SetBinding(", code);
        Assert.Contains("Path = new PropertyPath(nameof(AppEditDialog.DialogTitle))", code);
        Assert.Contains("Mode = BindingMode.OneWay", code);
        Assert.Contains("Padding = new Thickness(12,4,4,4)", code);
    }

    [Fact]
    public void DialogChrome_RemovesWhiteContentBorderLayer()
    {
        var code = File.ReadAllText(DialogChromePath);

        Assert.Contains("ContentDialogTopOverlay", code);
        Assert.Contains("ContentDialogCornerRadius", code);
        Assert.Contains("Margin = new Thickness(0)", code);
        Assert.Contains("Padding = new Thickness(6, 4, 6, 4)", code);
        Assert.Contains("ResolveContentBackground(dialog)", code);
    }
}
