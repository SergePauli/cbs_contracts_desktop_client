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
    public void ContentHostView_BindsMultiSelectOptionsSourcesIntoReferenceTable()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);

        Assert.Contains("MultiSelectOptionsSources=\"{Binding CurrentFilterOptionsSources}\"", xaml);
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

    [Fact]
    public void ContentHostView_PreparesProfileEditStateScaffold()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("await ShowReferenceEditDialogAsync(isCreateMode: true);", codeBehind);
        Assert.Contains("await ShowReferenceEditDialogAsync(isCreateMode: false);", codeBehind);
        Assert.Contains("if (_viewModel.CurrentReference.EditorKind == ReferenceEditorKind.Profile)", codeBehind);
        Assert.Contains("await ShowProfileEditDialogAsync(isCreateMode);", codeBehind);
        Assert.Contains("var viewModel = new ProfileEditViewModel(state, LoadPositionOptionsAsync);", codeBehind);
        Assert.Contains("var dialog = new ProfileEditDialog(viewModel)", codeBehind);
        Assert.Contains("ProfileEditPayloadBuilder.BuildForCreate(viewModel)", codeBehind);
        Assert.Contains("ProfileEditPayloadBuilder.BuildForUpdate(viewModel)", codeBehind);
        Assert.Contains("await _referenceCrudService.CreateAsync(_viewModel.CurrentReference!, payload)", codeBehind);
        Assert.Contains("await _referenceCrudService.UpdateAsync(_viewModel.CurrentReference!, payload)", codeBehind);
        Assert.Contains("CreateProfileEditDialogState", codeBehind);
        Assert.Contains("ProfileEditStateFactory.Create(", codeBehind);
        Assert.Contains("private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadPositionOptionsAsync(", codeBehind);
        Assert.Contains("Model = \"Position\"", codeBehind);
        Assert.Contains("Preset = \"item\"", codeBehind);
        Assert.Contains("[\"name__cnt\"] = normalizedSearchText", codeBehind);
        Assert.Contains(".OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)", codeBehind);
    }
}
