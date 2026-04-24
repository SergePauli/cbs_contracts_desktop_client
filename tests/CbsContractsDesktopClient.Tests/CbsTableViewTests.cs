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

    [Fact]
    public void CbsTableView_BuildsTriStateBooleanFilterCheckBox()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("CreateBooleanFilterCheckBox", code);
        Assert.Contains("IsThreeState = true", code);
        Assert.Contains("OnBooleanFilterCheckBoxChanged", code);
        Assert.Contains("checkBox.IsChecked", code);
        Assert.Contains("DataFilterMatchMode.Equals", code);
        Assert.Contains("CbsTableFilterEditorKind.Boolean", code);
        Assert.Contains("MinWidth = 24", code);
        Assert.Contains("ToolTipService.SetToolTip(checkBox, \"Фильтр: все / да / нет\")", code);
    }

    [Fact]
    public void CbsTableView_UsesExtendedFilterModesForDateTimeColumns()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("column.Filter.Mode == DataFilterMode.DateTime", code);
        Assert.Contains("DataFilterMatchMode.GreaterThanOrEqual", code);
        Assert.Contains("DataFilterMatchMode.LessThanOrEqual", code);
        Assert.Contains("DataFilterMatchMode.Contains", code);
        Assert.Contains("\"Позже чем\"", code);
        Assert.Contains("\"Не ранее чем\"", code);
        Assert.Contains("\"Ранее чем\"", code);
        Assert.Contains("\"Не позже чем\"", code);
    }

    [Fact]
    public void CbsTableView_UsesIsoDateTimeTextFilter()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("GetDateTimePlaceholder(column)", code);
        Assert.Contains("BuildIsoDateTimePlaceholderPattern()", code);
        Assert.Contains("\"ГГГГ-ММ-ДД ЧЧ:ММ:СС\"", code);
        Assert.Contains("OnDateTimeFilterTextBoxBeforeTextChanging", code);
        Assert.Contains("IsMaskedDateTimeMode(column)", code);
        Assert.Contains("NormalizeDateTimeFilterValue", code);
        Assert.Contains("NormalizeIsoDateTimeTextFragment", code);
        Assert.Contains("TryExtractCompleteDateTimeValue", code);
    }

    [Fact]
    public void CbsTableView_UsesDatePickerForComparativeDateTimeModes()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("CreateDateTimeFilterHost", code);
        Assert.Contains("new CalendarDatePicker", code);
        Assert.Contains("CreateDateTimeFilterClearButton", code);
        Assert.Contains("DateChanged += OnDateTimeFilterDateChanged", code);
        Assert.Contains("OnDateTimeFilterClearButtonClick", code);
        Assert.Contains("Glyph = \"\\uE711\"", code);
        Assert.Contains("ToolTipService.SetToolTip(button, \"РћС‡РёСЃС‚РёС‚СЊ С„РёР»СЊС‚СЂ РґР°С‚С‹\")", code);
        Assert.Contains("PlaceholderText = string.Empty", code);
        Assert.Contains("state.ClearButton.Visibility = state.DatePicker.Date.HasValue", code);
        Assert.Contains("state.TextBox.Visibility = Visibility.Visible", code);
        Assert.Contains("state.DatePicker.Visibility = Visibility.Collapsed", code);
        Assert.Contains("state.TextBox.Visibility = Visibility.Collapsed", code);
        Assert.Contains("state.DatePicker.Visibility = Visibility.Visible", code);
        Assert.Contains("return dateTimeState.DatePicker.Date;", code);
    }

    [Fact]
    public void CbsTableView_EmitsRowDoubleTappedEventAndKeepsSelectionInSync()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("public event EventHandler<CbsTableRowDoubleTappedEventArgs>? RowDoubleTapped;", code);
        Assert.Contains("rowView.DoubleTapped += OnRowDoubleTapped;", code);
        Assert.Contains("private void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)", code);
        Assert.Contains("SelectedItem = rowView.Row;", code);
        Assert.Contains("RowDoubleTapped?.Invoke(this, new CbsTableRowDoubleTappedEventArgs(rowView.Row!, rowIndex));", code);
        Assert.Contains("public sealed class CbsTableRowDoubleTappedEventArgs : EventArgs", code);
    }
}
