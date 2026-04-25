using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContentHostViewTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

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
        Assert.Contains("RowDoubleTapped=\"ReferenceTableView_RowDoubleTapped\"", xaml);
    }

    [Fact]
    public void ContentHostView_DefinesEmployeeDetailFooterBelowTable()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);
        var detailXamlPath = Path.Combine(ProjectRoot, "src", "Views", "References", "EmployeeDetailView.xaml");
        var detailXaml = File.ReadAllText(detailXamlPath);

        Assert.Contains("xmlns:references=\"using:CbsContractsDesktopClient.Views.References\"", xaml);
        Assert.Contains("<references:EmployeeDetailView", xaml);
        Assert.Contains("Grid.Row=\"1\"", xaml);
        Assert.Contains("Row=\"{Binding SelectedRow}\"", xaml);
        Assert.Contains("Visibility=\"{Binding ShowEmployeeDetailView, Converter={StaticResource BoolVisibilityConverter}}\"", xaml);
        Assert.Contains("Height=\"130\"", detailXaml);
        Assert.Contains("Background=\"{StaticResource ShellTableHeaderBackgroundBrush}\"", detailXaml);
        Assert.Contains("<ColumnDefinition Width=\"15*\" />", detailXaml);
        Assert.Contains("<ColumnDefinition Width=\"85*\" />", detailXaml);
        Assert.Contains("HorizontalAlignment=\"Right\"", detailXaml);
        Assert.Contains("HorizontalAlignment=\"Left\"", detailXaml);
        Assert.Contains("x:Name=\"EmployeeDismissedStatusTextBlock\"", detailXaml);
        Assert.Contains("Text=\"Должность\"", detailXaml);
        Assert.Contains("Text=\"Контакты\"", detailXaml);
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

    [Fact]
    public void ContentHostView_OpensEditOnRowDoubleClick_ExceptInternRole()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("private async void ReferenceTableView_RowDoubleTapped(object sender, CbsTableRowDoubleTappedEventArgs e)", codeBehind);
        Assert.Contains("_viewModel.SelectedRow = e.Row;", codeBehind);
        Assert.Contains("if (IsInternEditBlocked())", codeBehind);
        Assert.Contains("await ShowReferenceEditDialogAsync(isCreateMode: false);", codeBehind);
        Assert.Contains("private bool IsInternEditBlocked()", codeBehind);
        Assert.Contains("string.Equals(role, \"intern\", System.StringComparison.OrdinalIgnoreCase)", codeBehind);
    }

    [Fact]
    public void ContentHostView_DefinesHolidayRecalcActionButton()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("x:Name=\"HolidayRecalcButton\"", xaml);
        Assert.Contains("Click=\"HolidayRecalcButton_Click\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"Пересчитать сроки этапов\"", xaml);
        Assert.Contains("private async void HolidayRecalcButton_Click(object sender, RoutedEventArgs e)", codeBehind);
        Assert.Contains("await RecalculateHolidayStagesAsync();", codeBehind);
        Assert.Contains("private async Task RecalculateHolidayStagesAsync()", codeBehind);
        Assert.Contains("LoadAffectedStagesAsync", codeBehind);
        Assert.Contains("LoadHolidayCalendarAsync", codeBehind);
        Assert.Contains("BuildStagePatch", codeBehind);
    }
}
