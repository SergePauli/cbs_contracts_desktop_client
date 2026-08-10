using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class CbsTableViewTests
{
    private static readonly string CbsTableViewPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Controls",
        "CbsTableView.xaml.cs");

    private static readonly string TableRenderRequestPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Controls",
        "TableRenderRequest.cs");

    private static readonly string FilterIconFactoryPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Controls",
        "FilterIconFactory.cs");

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
        Assert.Contains("MultiSelectFilterSearchHeight = 24d", code);
        Assert.Contains("MultiSelectFilterHeaderWidthFactor = 0.9d", code);
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
    public void CbsTableView_MultiSelectFilterFlyoutHasClearAndCloseActions()
    {
        var code = File.ReadAllText(CbsTableViewPath);
        var iconFactory = File.ReadAllText(FilterIconFactoryPath);

        Assert.Contains("FilterIconFactory.BuildFilterClearIcon()", code);
        Assert.Contains("CreateMultiSelectFilterFlyoutActionButton", code);
        Assert.Contains("ToolTipService.SetToolTip(button, tooltip)", code);
        Assert.Contains("\"очистить\"", code);
        Assert.Contains("OnMultiSelectClearButtonClick", code);
        Assert.Contains("state.SelectedValues = Array.Empty<object?>();", code);
        Assert.Contains("CbsTableMultiSelectFilterValue.Create(GetMultiSelectOptions(state.Column), state.SelectedValues)", code);
        Assert.Contains("\"закрыть\"", code);
        Assert.Contains("OnMultiSelectCloseButtonClick", code);
        Assert.Contains("flyout.Hide();", code);
        Assert.Contains("Glyph = \"\\uE71C\"", iconFactory);
        Assert.Contains("Glyph = \"\\uE733\"", iconFactory);
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
        Assert.Contains("state.SelectedOptions = allOptions", code);
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

        Assert.Contains("IsDateFilterMode(column.Filter.Mode)", code);
        Assert.Contains("return mode is DataFilterMode.Date or DataFilterMode.DateTime;", code);
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
        Assert.Contains("BuildIsoDatePlaceholderPattern()", code);
        Assert.Contains("BuildIsoDateTimePlaceholderPattern()", code);
        Assert.Contains("\"ГГГГ-ММ-ДД\"", code);
        Assert.Contains("\"ГГГГ-ММ-ДД ЧЧ:ММ:СС\"", code);
        Assert.Contains("OnDateTimeFilterTextBoxBeforeTextChanging", code);
        Assert.Contains("IsMaskedDateTimeMode(column)", code);
        Assert.Contains("NormalizeDateTimeFilterValue", code);
        Assert.Contains("NormalizeIsoDateTimeTextFragment(column.Filter.Mode, text)", code);
        Assert.Contains("TryExtractCompleteDateTimeValue(column.Filter.Mode, text, out var completeValue)", code);
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
        Assert.Contains("ToolTipService.SetToolTip(button, \"Очистить фильтр даты\")", code);
        Assert.Contains("PlaceholderText = string.Empty", code);
        Assert.Contains("state.ClearButton.Visibility = state.DatePicker.Date.HasValue", code);
        Assert.Contains("state.TextBox.Visibility = Visibility.Visible", code);
        Assert.Contains("state.DatePicker.Visibility = Visibility.Collapsed", code);
        Assert.Contains("state.TextBox.Visibility = Visibility.Collapsed", code);
        Assert.Contains("state.DatePicker.Visibility = Visibility.Visible", code);
        Assert.Contains("return dateTimeState.DatePicker.Date;", code);
    }

    [Fact]
    public void CbsTableView_UsesInitialColumnFilterValues()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("FormatFilterValue(column.Filter.Value)", code);
        Assert.Contains("IsChecked = TryGetBooleanFilterValue(column.Filter.Value)", code);
        Assert.Contains("Date = TryGetDateFilterValue(column.Filter.Value)", code);
        Assert.Contains("SelectedValues = NormalizeFilterSelectedValues(column.Filter.Value)", code);
        Assert.Contains("private static IReadOnlyList<object?> NormalizeFilterSelectedValues(object? value)", code);
    }

    [Fact]
    public void CbsTableView_EmitsRowDoubleTappedEventAndKeepsSelectionInSync()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("public event EventHandler<CbsTableRowDoubleTappedEventArgs>? RowDoubleTapped;", code);
        Assert.Contains("rowView.DoubleTapped += OnRowDoubleTapped;", code);
        Assert.Contains("private void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)", code);
        Assert.Contains("SelectSingleRow(rowView.Row!, rowIndex);", code);
        Assert.Contains("RowDoubleTapped?.Invoke(this, new CbsTableRowDoubleTappedEventArgs(rowView.Row!, rowIndex));", code);
        Assert.Contains("public sealed class CbsTableRowDoubleTappedEventArgs : EventArgs", code);
    }

    [Fact]
    public void CbsTableView_TogglesSingleRowSelectionAndEmitsSelectionChanged()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("public event EventHandler<CbsTableRowSelectionChangedEventArgs>? RowSelectionChanged;", code);
        Assert.Contains("if (_selectedIndexes.Contains(rowIndex))", code);
        Assert.Contains("SelectedItem = null;", code);
        Assert.Contains("new CbsTableRowSelectionChangedEventArgs(null, rowIndex, isSelected: false)", code);
        Assert.Contains("public sealed class CbsTableRowSelectionChangedEventArgs : EventArgs", code);
    }

    [Fact]
    public void CbsTableView_HandlesUpDownKeysWhenTableHasFocus()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("IsTabStop = true;", code);
        Assert.Contains("PreviewKeyDown += OnPreviewKeyDown;", code);
        Assert.Contains("private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)", code);
        Assert.Contains("e.Key is not VirtualKey.Up and not VirtualKey.Down", code);
        Assert.Contains("e.Handled = MoveSelectionOrScroll(e.Key == VirtualKey.Down ? 1 : -1);", code);
    }

    [Fact]
    public void CbsTableView_UpDownWithoutShiftClearsCellSelectionBeforeMovingRow()
    {
        var code = File.ReadAllText(CbsTableViewPath);
        var handlerStart = code.IndexOf("private void OnPreviewKeyDown", StringComparison.Ordinal);
        var handlerEnd = code.IndexOf("private void OnCopySelectionAcceleratorInvoked", handlerStart, StringComparison.Ordinal);
        var handler = code[handlerStart..handlerEnd];

        Assert.True(handler.IndexOf("TryExtendCellSelectionVertically(e.Key)", StringComparison.Ordinal)
            < handler.IndexOf("ClearCellSelection();", StringComparison.Ordinal));
        Assert.True(handler.IndexOf("ClearCellSelection();", StringComparison.Ordinal)
            < handler.IndexOf("MoveSelectionOrScroll", StringComparison.Ordinal));
    }

    [Fact]
    public void CbsTableView_ArrowKeysMoveSelectionBeforeScrolling()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("if (SupportsRowSelection && TryMoveSelectedRow(direction))", code);
        Assert.Contains("return ScrollByRows(direction, 1);", code);
        Assert.Contains("var selectedIndex = FindRowIndex(sourceRows, SelectedItem);", code);
        Assert.Contains("var targetIndex = selectedIndex + direction;", code);
        Assert.Contains("sourceRows[targetIndex].IsPlaceholder", code);
        Assert.Contains("SelectSingleRow(sourceRows[targetIndex], targetIndex);", code);
        Assert.Contains("ScrollRowIntoView(targetIndex);", code);
    }

    [Fact]
    public void CbsTableView_KeyboardAndPointerSelectionUseSharedSelectionPath()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("private void SelectSingleRow(TableDataRow row, int rowIndex)", code);
        Assert.Contains("_selectedIndexes.Clear();", code);
        Assert.Contains("_selectedIndexes.Add(rowIndex);", code);
        Assert.Contains("SelectedItem = row;", code);
        Assert.Contains("new CbsTableRowSelectionChangedEventArgs(row, rowIndex, isSelected: true)", code);
        Assert.Contains("Focus(FocusState.Programmatic);", code);
        Assert.Contains("SelectSingleRow(rowView.Row!, rowIndex);", code);
    }

    [Fact]
    public void CbsTableView_SelectsCellRangesAndCopiesSpreadsheetText()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("SupportsCellSelectionProperty", code);
        Assert.Contains("private CbsTableCellPosition? _cellSelectionAnchor;", code);
        Assert.Contains("rowView.PointerMoved += OnRowPointerMoved;", code);
        Assert.Contains("TryExtendCellSelectionVertically", code);
        Assert.Contains("public bool CopySelectedCellRangeToClipboard()", code);
        Assert.Contains("return CopyCellSelection(includeHeaders: false);", code);
        Assert.Contains("new KeyboardAccelerator", code);
        Assert.Contains("Key = VirtualKey.C", code);
        Assert.Contains("Modifiers = VirtualKeyModifiers.Control", code);
        Assert.Contains("copySelectionAccelerator.Invoked += OnCopySelectionAcceleratorInvoked;", code);
        Assert.Contains("args.Handled = CopySelectedCellRangeToClipboard();", code);
        Assert.DoesNotContain("e.Key == VirtualKey.C", code);
        Assert.Contains("package.SetText(text.ToString());", code);
        Assert.Contains("text.AppendJoin('\\t'", code);
    }

    [Fact]
    public void CbsTableView_ContextMenuCanCopySelectionWithHeaders()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("CreateCellSelectionContextMenu", code);
        Assert.Contains("Text = \"Копировать с заголовками\"", code);
        Assert.Contains("CopyCellSelection(includeHeaders: true)", code);
        Assert.Contains("Columns[index].Header", code);
    }

    [Fact]
    public void CbsTableView_PropagatesStageCostFractionModeToRows()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("ShowStageCostFractionProperty", code);
        Assert.Contains("nameof(ShowStageCostFraction)", code);
        Assert.Contains("OnShowStageCostFractionChanged", code);
        Assert.Contains("new TableRenderRequest(TableRenderReason.ValueStyleChanged)", code);
        Assert.Contains("ShowStageCostFraction);", code);
    }

    [Fact]
    public void CbsTableView_UsesValueStyleInvalidationWithoutReloadingRows()
    {
        var tableCode = File.ReadAllText(CbsTableViewPath);
        var requestCode = File.ReadAllText(TableRenderRequestPath);

        Assert.Contains("ValueStyleChanged", requestCode);
        Assert.Contains("request.Reason == TableRenderReason.ValueStyleChanged", tableCode);
        Assert.Contains("RefreshVisibleRowsForValueStyleChange();", tableCode);
        Assert.Contains("TABLE VALUE STYLE REPAINT START", tableCode);
        Assert.Contains("ConfigureVisibleRows(sourceRows, _lastWindowStart, rowCount);", tableCode);
    }

    [Fact]
    public void CbsTableView_UsesSpecificRenderPathsForScrollAndFullRender()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("private enum RowsRenderPath", code);
        Assert.Contains("RowsRenderPath.Full", code);
        Assert.Contains("RowsRenderPath.ScrollDown", code);
        Assert.Contains("RowsRenderPath.ScrollUp", code);
        Assert.Contains("ResolveRowsRenderPath", code);
        Assert.Contains("ConfigureScrolledRowsDown", code);
        Assert.Contains("ConfigureScrolledRowsUp", code);
        Assert.Contains("MoveFirstRowViewToEnd", code);
        Assert.Contains("MoveLastRowViewToStart", code);
    }

    [Fact]
    public void CbsTableView_RepaintsRenderedPlaceholdersFromRowPoolAfterItemsArrive()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("RefreshVisibleRowsIfViewportHasPlaceholders", code);
        Assert.Contains("rowView.Row?.IsPlaceholder != true", code);
        Assert.Contains("rowView.Tag is not int absoluteIndex", code);
        Assert.Contains("sourceRows[absoluteIndex].IsPlaceholder", code);
        Assert.Contains("ConfigureRowPoolRange(sourceRows, poolIndex, absoluteIndex, 1)", code);
        Assert.Contains("TABLE ITEMS REPAINT renderedPlaceholders=", code);
        Assert.DoesNotContain("RefreshVisibleRowsIfViewportHasPlaceholders()\r\n        {\r\n            var sourceRows = GetSourceRows();\r\n            if (!TryGetCurrentWindowPlaceholderCount(sourceRows, out var rowCount, out var placeholderCount))", code);
    }

    [Fact]
    public void CbsTableView_RepaintsSingleVisibleRowByAbsoluteIndex()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("public bool RefreshVisibleRow(int absoluteIndex, TableDataRow row)", code);
        Assert.Contains("var poolIndex = absoluteIndex - _lastWindowStart;", code);
        Assert.Contains("renderedIndex != absoluteIndex", code);
        Assert.Contains("_rowPool[poolIndex].Configure(", code);
        Assert.Contains("ApplyRowSelectionState(_rowPool[poolIndex], absoluteIndex);", code);
        Assert.Contains("TABLE ROW REPAINT index=", code);
    }

    [Fact]
    public void CbsTableView_SuppressesRepeatedEmptyInitialRenderWithoutClearingNonEmptyRows()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("TrySuppressEmptyRowsRender", code);
        Assert.Contains("if (totalRows != 0 || _rowPool.Count > 0 || _lastSourceCount > 0)", code);
        Assert.Contains("UpdateSpacerHeights(0, 0, 0);", code);
        Assert.Contains("if (control._lastSourceCount == 0 && control._rowPool.Count == 0)", code);
    }

    [Fact]
    public void CbsTableView_UsesExplicitRenderRequestForFilterAndSortInvalidation()
    {
        var tableCode = File.ReadAllText(CbsTableViewPath);
        var requestCode = File.ReadAllText(TableRenderRequestPath);

        Assert.Contains("public enum TableRenderReason", requestCode);
        Assert.Contains("FilterChanged", requestCode);
        Assert.Contains("SortChanged", requestCode);
        Assert.Contains("public sealed record TableRenderRequest", requestCode);
        Assert.Contains("bool ResetScroll = false", requestCode);
        Assert.Contains("public void InvalidateRows(TableRenderRequest request)", tableCode);
        Assert.Contains("RowsScrollViewer.ChangeView(null, 0d, null, disableAnimation: true);", tableCode);
        Assert.Contains("InvalidateWindowCache();", tableCode);
        Assert.Contains("RebuildRows();", tableCode);
    }

    [Fact]
    public void CbsTableView_ClearsPendingLoadWhenRenderInvalidatesOrLoadingCompletes()
    {
        var code = File.ReadAllText(CbsTableViewPath);

        Assert.Contains("public void InvalidateRows(TableRenderRequest request)", code);
        Assert.Contains("_isLoadPending = false;", code);
        Assert.Contains("private static void OnIsLoadingChanged", code);
        Assert.Contains("if (!control.IsLoading)", code);
        Assert.Contains("control._isLoadPending = false;", code);
    }
}
