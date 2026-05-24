using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class AppDialogLayoutTests
{
    private static readonly string AppDialogLayoutPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Shared",
        "Dialogs",
        "AppDialogLayout.cs");

    [Fact]
    public void AppDialogLayout_DefinesCenteredDialogSectionTitleWithAccentPart()
    {
        var code = File.ReadAllText(AppDialogLayoutPath);

        Assert.Contains("public static UIElement BuildDialogSectionTitle(string title, string? accentText)", code);
        Assert.Contains("TextAlignment = TextAlignment.Center", code);
        Assert.Contains("FontSize = 14", code);
        Assert.Contains("textBlock.Inlines.Add(new Run { Text = title + \" \" });", code);
        Assert.Contains("Foreground = Application.Current.Resources[\"ShellAccentBrush\"] as Brush", code);
    }

    [Fact]
    public void AppDialogLayout_DefinesAccentSummaryLineForMoneyValues()
    {
        var code = File.ReadAllText(AppDialogLayoutPath);

        Assert.Contains("public static UIElement BuildAccentSummaryLine(string label, string value)", code);
        Assert.Contains("Foreground = Application.Current.Resources[\"ShellAccentBrush\"] as Brush", code);
    }

    [Fact]
    public void AppDialogLayout_DefinesReusableDynamicSummaryText()
    {
        var code = File.ReadAllText(AppDialogLayoutPath);

        Assert.Contains("public static TextBlock BuildDynamicSummaryText()", code);
        Assert.Contains("FontWeight = Microsoft.UI.Text.FontWeights.SemiBold", code);
        Assert.Contains("TextWrapping = TextWrapping.Wrap", code);
    }
}
