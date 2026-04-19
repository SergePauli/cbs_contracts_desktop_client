using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class CbsTableViewTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

    private static readonly string CbsTableViewPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Controls",
        "CbsTableView.xaml.cs");

    [Fact]
    public void CbsTableView_BuildsMultiSelectFilterFlyoutWithSearchAndCheckboxList()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("CreateMultiSelectFilterButton", code);
        Assert.Contains("new Flyout", code);
        Assert.Contains("new TextBox", code);
        Assert.Contains("PlaceholderText = \"Поиск\"", code);
        Assert.Contains("new ScrollViewer", code);
        Assert.Contains("new StackPanel", code);
        Assert.Contains("new CheckBox", code);
        Assert.Contains("MultiSelectFilterFlyoutMaxHeight = 180d", code);
        Assert.Contains("MinHeight = 20", code);
        Assert.Contains("Margin = new Thickness(4, 1, 4, 1)", code);
        Assert.Contains("Margin = new Thickness(2, 0, 0, 0)", code);
        Assert.Contains("OnMultiSelectSearchTextChanged", code);
        Assert.Contains("OnMultiSelectOptionChanged", code);
    }

    [Fact]
    public void CbsTableView_TracksMultiSelectInternalState()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("internal sealed class MultiSelectFilterUiState", code);
        Assert.Contains("AvailableOptions", code);
        Assert.Contains("SelectedOptions", code);
        Assert.Contains("SelectedValues", code);
        Assert.Contains("SearchText", code);
    }

    [Fact]
    public void CbsTableView_UsesOptionsSourceLookupForMultiSelectOptions()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("MultiSelectOptionsSourcesProperty", code);
        Assert.Contains("nameof(MultiSelectOptionsSources)", code);
        Assert.Contains("GetMultiSelectOptions", code);
        Assert.Contains("column.Filter.OptionsSourceKey", code);
        Assert.Contains("MultiSelectOptionsSources.TryGetValue", code);
        Assert.Contains("RefreshMultiSelectFilterStates", code);
        Assert.Contains("state.SelectedOptions = GetMultiSelectOptions(state.Column)", code);
    }

    [Fact]
    public void CbsTableView_UsesCompactMultiSelectSummaryText()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("? column.Filter.EmptySelectionText", code);
        Assert.Contains("selectedCount", code);
        Assert.Contains("Glyph = \"\\uE70D\"", code);
        Assert.Contains("HorizontalContentAlignment = HorizontalAlignment.Stretch", code);
        Assert.DoesNotContain("selectedOptions[0]", code);
    }
}
