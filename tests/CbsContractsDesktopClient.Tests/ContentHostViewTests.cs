using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContentHostViewTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

    private static readonly string ContentHostViewXamlPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ContentHostView.xaml");

    private static readonly string ContentHostViewCodeBehindPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ContentHostView.xaml.cs");

    [Fact]
    public void ContentHostView_SettingsButton_ContainsTableSettingsTooltipAndMenuFlyout()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);

        Assert.Contains("x:Name=\"HeaderSettingsButton\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"Настройки таблицы\"", xaml);
        Assert.Contains("<Button.Flyout>", xaml);
        Assert.Contains("<MenuFlyout>", xaml);
    }

    [Fact]
    public void ContentHostView_SettingsMenu_DefinesThreeResetCommands()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);

        Assert.Contains("Text=\"Сбросить ширину\"", xaml);
        Assert.Contains("Click=\"ResetColumnWidthsMenuItem_Click\"", xaml);

        Assert.Contains("Text=\"Сбросить фильтры\"", xaml);
        Assert.Contains("Click=\"ResetFiltersMenuItem_Click\"", xaml);

        Assert.Contains("Text=\"Сбросить сортировку\"", xaml);
        Assert.Contains("Click=\"ResetSortingMenuItem_Click\"", xaml);
    }

    [Fact]
    public void ContentHostView_CodeBehind_ImplementsThreeSettingsMenuHandlers()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("private async void ResetColumnWidthsMenuItem_Click", codeBehind);
        Assert.Contains("private async void ResetFiltersMenuItem_Click", codeBehind);
        Assert.Contains("private async void ResetSortingMenuItem_Click", codeBehind);
    }
}
