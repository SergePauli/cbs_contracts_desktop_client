using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;

namespace CbsContractsDesktopClient.Views.Controls
{
    public sealed partial class CbsTableView : UserControl
    {
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(
                nameof(Columns),
                typeof(IReadOnlyList<CbsTableColumnDefinition>),
                typeof(CbsTableView),
                new PropertyMetadata(Array.Empty<CbsTableColumnDefinition>(), OnColumnsChanged));

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(CbsTableView),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty CurrentSortFieldProperty =
            DependencyProperty.Register(
                nameof(CurrentSortField),
                typeof(string),
                typeof(CbsTableView),
                new PropertyMetadata(null, OnSortStateChanged));

        public static readonly DependencyProperty CurrentSortDirectionProperty =
            DependencyProperty.Register(
                nameof(CurrentSortDirection),
                typeof(DataSortDirection?),
                typeof(CbsTableView),
                new PropertyMetadata(null, OnSortStateChanged));

        public static readonly DependencyProperty TableStateKeyProperty =
            DependencyProperty.Register(
                nameof(TableStateKey),
                typeof(string),
                typeof(CbsTableView),
                new PropertyMetadata(string.Empty, OnTableStateKeyChanged));

        public static readonly DependencyProperty MultiSelectOptionsSourcesProperty =
            DependencyProperty.Register(
                nameof(MultiSelectOptionsSources),
                typeof(IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>),
                typeof(CbsTableView),
                new PropertyMetadata(
                    new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase),
                    OnMultiSelectOptionsSourcesChanged));

        public static readonly DependencyProperty HasMoreItemsProperty =
            DependencyProperty.Register(
                nameof(HasMoreItems),
                typeof(bool),
                typeof(CbsTableView),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(
                nameof(IsLoading),
                typeof(bool),
                typeof(CbsTableView),
                new PropertyMetadata(false, OnIsLoadingChanged));

        public static readonly DependencyProperty LoadedCountProperty =
            DependencyProperty.Register(
                nameof(LoadedCount),
                typeof(int),
                typeof(CbsTableView),
                new PropertyMetadata(0));

        public static readonly DependencyProperty TotalCountProperty =
            DependencyProperty.Register(
                nameof(TotalCount),
                typeof(int),
                typeof(CbsTableView),
                new PropertyMetadata(0));

        public static readonly DependencyProperty RowHeightProperty =
            DependencyProperty.Register(
                nameof(RowHeight),
                typeof(double),
                typeof(CbsTableView),
                new PropertyMetadata(22d, OnRowHeightChanged));

        public static readonly DependencyProperty DensityProperty =
            DependencyProperty.Register(
                nameof(Density),
                typeof(CbsTableDensity),
                typeof(CbsTableView),
                new PropertyMetadata(CbsTableDensity.Compact, OnDensityChanged));

        public static readonly DependencyProperty RowStyleKeyProperty =
            DependencyProperty.Register(
                nameof(RowStyleKey),
                typeof(CbsTableRowStyleKey),
                typeof(CbsTableView),
                new PropertyMetadata(CbsTableRowStyleKey.None, OnRowStyleKeyChanged));

        public static readonly DependencyProperty ShowStageCostFractionProperty =
            DependencyProperty.Register(
                nameof(ShowStageCostFraction),
                typeof(bool),
                typeof(CbsTableView),
                new PropertyMetadata(false, OnShowStageCostFractionChanged));

        public static readonly DependencyProperty SupportsRowSelectionProperty =
            DependencyProperty.Register(
                nameof(SupportsRowSelection),
                typeof(bool),
                typeof(CbsTableView),
                new PropertyMetadata(false));

        public static readonly DependencyProperty SupportsMultipleRowSelectionProperty =
            DependencyProperty.Register(
                nameof(SupportsMultipleRowSelection),
                typeof(bool),
                typeof(CbsTableView),
                new PropertyMetadata(false));

        public static readonly DependencyProperty SupportsCellSelectionProperty =
            DependencyProperty.Register(
                nameof(SupportsCellSelection),
                typeof(bool),
                typeof(CbsTableView),
                new PropertyMetadata(true));

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(TableDataRow),
                typeof(CbsTableView),
                new PropertyMetadata(null));

        public static readonly DependencyProperty RetainedBufferRowsProperty =
            DependencyProperty.Register(
                nameof(RetainedBufferRows),
                typeof(int),
                typeof(CbsTableView),
                new PropertyMetadata(0));

        private bool _isLoadPending;
        private int _lastTriggeredLoadedCount = -1;
        private int _rebuildSequence;
        private int _completedLayoutSequence;
        private int _lastWindowStart = -1;
        private int _lastWindowEnd = -1;
        private int _lastSourceCount = -1;
        private int _sameViewportViewChangedCount;
        private IEnumerable? _lastItemsSourceReference;
        private readonly List<CbsTableRowView> _rowPool = [];
        private readonly HashSet<int> _selectedIndexes = [];
        private CbsTableCellPosition? _cellSelectionAnchor;
        private CbsTableCellPosition? _cellSelectionEnd;
        private bool _isDraggingCellSelection;
        private readonly Dictionary<string, string> _filterTexts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DataFilterMatchMode> _filterModes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TextBox> _filterTextBoxes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTimeFilterUiState> _filterDateTimeStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CheckBox> _filterBooleanCheckBoxes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _filterModeButtons = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _filterMultiSelectButtons = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MultiSelectFilterUiState> _filterMultiSelectStates = new(StringComparer.OrdinalIgnoreCase);
        private const int WindowBufferRows = 8;
        private const int WindowStepRows = 8;
        private const double HeaderSideBorderCompensation = 1d;
        private const double MinimumColumnWidth = 48d;
        private const double ResizeHandleWidth = 12d;
        private const double HeaderAdornmentWidth = 18d;
        private const double FilterModeButtonWidth = 22d;
        private const double FilterTextBoxHeight = 22d;
        private const double FilterDatePickerHeight = 22d;
        private const double MultiSelectFilterButtonHeight = 22d;
        private const double MultiSelectFilterFlyoutWidth = 260d;
        private const double MultiSelectFilterFlyoutMaxHeight = 180d;
        private const double MultiSelectFilterSearchHeight = 24d;
        private const double MultiSelectFilterActionButtonSize = 24d;
        private const double MultiSelectFilterHeaderWidthFactor = 0.9d;
        private enum RowsRenderPath
        {
            Full,
            ScrollDown,
            ScrollUp
        }

        private int _activeResizeColumnIndex = -1;
        private double _activeResizeStartWidth;
        private bool _suppressNextHeaderClick;
        private bool _suppressFilterNotifications;

        public CbsTableView()
        {
            InitializeComponent();
            IsTabStop = true;
            PreviewKeyDown += OnPreviewKeyDown;
            ContextFlyout = CreateCellSelectionContextMenu();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public event EventHandler<CbsTableLoadMoreRequestedEventArgs>? LoadMoreRequested;

        public event EventHandler<CbsTableSortRequestedEventArgs>? SortRequested;

        public event EventHandler<CbsTableTraceEventArgs>? TraceGenerated;

        public event EventHandler<CbsTableViewportChangedEventArgs>? ViewportChanged;

        public event EventHandler<CbsTableColumnWidthChangedEventArgs>? ColumnWidthChanged;

        public event EventHandler<CbsTableFilterRequestedEventArgs>? FilterRequested;

        public event EventHandler<CbsTableRowSelectionChangedEventArgs>? RowSelectionChanged;

        public event EventHandler<CbsTableRowDoubleTappedEventArgs>? RowDoubleTapped;

        public void InvalidateRows(TableRenderRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            AppendTrace(
                $"TABLE INVALIDATE reason={request.Reason} resetScroll={request.ResetScroll}");

            if (request.Reason == TableRenderReason.ValueStyleChanged)
            {
                RefreshVisibleRowsForValueStyleChange();
                return;
            }

            _isLoadPending = false;

            if (request.ResetScroll)
            {
                RowsScrollViewer.ChangeView(null, 0d, null, disableAnimation: true);
            }

            InvalidateWindowCache();
            RebuildRows();
        }

        public void ClearFilterInputs()
        {
            _suppressFilterNotifications = true;

            try
            {
                ClearFilterInputsCore();
            }
            finally
            {
                _suppressFilterNotifications = false;
            }
        }

        public void ApplyFilterInputs(IReadOnlyList<DataFilterCriterion> filters)
        {
            ArgumentNullException.ThrowIfNull(filters);

            _suppressFilterNotifications = true;

            try
            {
                ClearFilterInputsCore();

                var filtersByField = filters.ToDictionary(
                    static filter => filter.FieldKey,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var column in Columns.Where(static column => column.IsFilterable))
                {
                    if (!filtersByField.TryGetValue(column.FieldKey, out var filter))
                    {
                        column.Filter.Value = null;
                        continue;
                    }

                    column.Filter.MatchMode = filter.MatchMode;
                    column.Filter.Value = filter.Value;
                    _filterModes[GetFilterStateKey(column)] = filter.MatchMode;
                    ApplyFilterInput(column, filter.Value);
                }
            }
            finally
            {
                _suppressFilterNotifications = false;
            }
        }

        private void ClearFilterInputsCore()
        {
            foreach (var column in Columns.Where(static column => column.IsFilterable))
            {
                column.Filter.Value = null;

                if (column.Filter.EditorKind == CbsTableFilterEditorKind.MultiSelect)
                {
                    if (_filterMultiSelectStates.TryGetValue(column.FieldKey, out var state))
                    {
                        state.SelectedValues = Array.Empty<object?>();
                        state.SelectedOptions = [];
                        state.SearchText = string.Empty;
                        state.AvailableOptions = GetMultiSelectOptions(column).ToList();
                        UpdateMultiSelectFilterButtonContent(state.Button, column);
                        RebuildMultiSelectOptionItems(state);
                    }
                }
                else if (column.Filter.EditorKind == CbsTableFilterEditorKind.Boolean)
                {
                    _filterTexts.Remove(GetFilterStateKey(column));
                }
                else if (IsDateFilterMode(column.Filter.Mode))
                {
                    _filterTexts[GetFilterStateKey(column)] = string.Empty;
                    if (_filterDateTimeStates.TryGetValue(column.FieldKey, out var dateTimeState))
                    {
                        dateTimeState.DatePicker.Date = null;
                        RefreshDateTimeFilterTextBox(column);
                    }
                }
                else
                {
                    _filterTexts[GetFilterStateKey(column)] = string.Empty;
                }
            }

            foreach (var textBox in _filterTextBoxes.Values)
            {
                if (textBox.Tag is CbsTableColumnDefinition column && IsDateFilterMode(column.Filter.Mode))
                {
                    textBox.Text = string.Empty;
                    textBox.BorderBrush = GetFilterBorderBrush(column);
                    textBox.Background = GetFilterBackgroundBrush(column);
                    textBox.Foreground = GetFilterForegroundBrush(column);
                }
                else if (!string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.Text = string.Empty;
                    if (textBox.Tag is CbsTableColumnDefinition textColumn)
                    {
                        textBox.BorderBrush = GetFilterBorderBrush(textColumn);
                        textBox.Background = GetFilterBackgroundBrush(textColumn);
                        textBox.Foreground = GetFilterForegroundBrush(textColumn);
                    }
                }
            }

            foreach (var checkBox in _filterBooleanCheckBoxes.Values)
            {
                checkBox.IsChecked = null;
                if (checkBox.Tag is CbsTableColumnDefinition column)
                {
                    checkBox.Foreground = GetFilterForegroundBrush(column);
                    checkBox.Background = GetFilterBackgroundBrush(column);
                }
            }
        }

        private void ApplyFilterInput(CbsTableColumnDefinition column, object? value)
        {
            column.Filter.Value = value;

            if (column.Filter.EditorKind == CbsTableFilterEditorKind.MultiSelect)
            {
                if (_filterMultiSelectStates.TryGetValue(column.FieldKey, out var state))
                {
                    var selectedValues = NormalizeFilterSelectedValues(value);
                    var allOptions = GetMultiSelectOptions(column);
                    column.Filter.Value = selectedValues;
                    state.SelectedValues = selectedValues;
                    state.SelectedOptions = allOptions
                        .Where(option => selectedValues.Any(selected => AreFilterValuesEqual(selected, option.Value)))
                        .ToList();
                    state.AvailableOptions = allOptions.ToList();
                    state.SearchText = string.Empty;
                    UpdateMultiSelectFilterButtonContent(state.Button, column);
                    RebuildMultiSelectOptionItems(state);
                }

                return;
            }

            if (column.Filter.EditorKind == CbsTableFilterEditorKind.Boolean)
            {
                if (_filterBooleanCheckBoxes.TryGetValue(column.FieldKey, out var checkBox))
                {
                    checkBox.IsChecked = TryGetBooleanFilterValue(value);
                    checkBox.Foreground = GetFilterForegroundBrush(column);
                    checkBox.Background = GetFilterBackgroundBrush(column);
                }

                return;
            }

            if (IsDateFilterMode(column.Filter.Mode))
            {
                _filterTexts[GetFilterStateKey(column)] = FormatFilterValue(value);
                if (_filterDateTimeStates.TryGetValue(column.FieldKey, out var dateTimeState))
                {
                    dateTimeState.DatePicker.Date = TryGetDateFilterValue(value);
                    dateTimeState.TextBox.Text = FormatFilterValue(value);
                    RefreshDateTimeFilterTextBox(column);
                }

                return;
            }

            var text = FormatFilterValue(value);
            _filterTexts[GetFilterStateKey(column)] = text;
            if (_filterTextBoxes.TryGetValue(column.FieldKey, out var textBox))
            {
                textBox.Text = text;
                textBox.BorderBrush = GetFilterBorderBrush(column);
                textBox.Background = GetFilterBackgroundBrush(column);
                textBox.Foreground = GetFilterForegroundBrush(column);
            }
        }

        public IReadOnlyList<CbsTableColumnDefinition> Columns
        {
            get => (IReadOnlyList<CbsTableColumnDefinition>)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public IEnumerable? ItemsSource
        {
            get => (IEnumerable?)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public string? CurrentSortField
        {
            get => (string?)GetValue(CurrentSortFieldProperty);
            set => SetValue(CurrentSortFieldProperty, value);
        }

        public DataSortDirection? CurrentSortDirection
        {
            get => (DataSortDirection?)GetValue(CurrentSortDirectionProperty);
            set => SetValue(CurrentSortDirectionProperty, value);
        }

        public string TableStateKey
        {
            get => (string)GetValue(TableStateKeyProperty);
            set => SetValue(TableStateKeyProperty, value);
        }

        public IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>> MultiSelectOptionsSources
        {
            get => (IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>)GetValue(MultiSelectOptionsSourcesProperty);
            set => SetValue(MultiSelectOptionsSourcesProperty, value);
        }

        public bool HasMoreItems
        {
            get => (bool)GetValue(HasMoreItemsProperty);
            set => SetValue(HasMoreItemsProperty, value);
        }

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public int LoadedCount
        {
            get => (int)GetValue(LoadedCountProperty);
            set => SetValue(LoadedCountProperty, value);
        }

        public int TotalCount
        {
            get => (int)GetValue(TotalCountProperty);
            set => SetValue(TotalCountProperty, value);
        }

        public double RowHeight
        {
            get => (double)GetValue(RowHeightProperty);
            set => SetValue(RowHeightProperty, value);
        }

        public CbsTableDensity Density
        {
            get => (CbsTableDensity)GetValue(DensityProperty);
            set => SetValue(DensityProperty, value);
        }

        public CbsTableRowStyleKey RowStyleKey
        {
            get => (CbsTableRowStyleKey)GetValue(RowStyleKeyProperty);
            set => SetValue(RowStyleKeyProperty, value);
        }

        public bool ShowStageCostFraction
        {
            get => (bool)GetValue(ShowStageCostFractionProperty);
            set => SetValue(ShowStageCostFractionProperty, value);
        }

        public int RetainedBufferRows
        {
            get => (int)GetValue(RetainedBufferRowsProperty);
            set => SetValue(RetainedBufferRowsProperty, value);
        }

        public bool SupportsRowSelection
        {
            get => (bool)GetValue(SupportsRowSelectionProperty);
            set => SetValue(SupportsRowSelectionProperty, value);
        }

        public bool SupportsMultipleRowSelection
        {
            get => (bool)GetValue(SupportsMultipleRowSelectionProperty);
            set => SetValue(SupportsMultipleRowSelectionProperty, value);
        }

        public bool SupportsCellSelection
        {
            get => (bool)GetValue(SupportsCellSelectionProperty);
            set => SetValue(SupportsCellSelectionProperty, value);
        }

        public Func<TableDataRow, bool>? CanSelectRow { get; set; }

        public TableDataRow? SelectedItem
        {
            get => (TableDataRow?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public void ClearSelection()
        {
            _selectedIndexes.Clear();
            SelectedItem = null;
            UpdateVisibleRowSelectionStates();
        }

        public void SetSelectedItem(TableDataRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            SelectedItem = row;
            _selectedIndexes.Clear();

            var sourceRows = GetSourceRows();
            for (var index = 0; index < sourceRows.Count; index++)
            {
                if (ReferenceEquals(sourceRows[index], row))
                {
                    _selectedIndexes.Add(index);
                    break;
                }
            }

            UpdateVisibleRowSelectionStates();
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            RebuildHeader();
            RebuildRows();
            RowsScrollViewer.ViewChanged += OnScrollViewerViewChanged;
            RowsScrollViewer.SizeChanged += OnRowsScrollViewerSizeChanged;
            UpdateHeaderViewportCompensation();
            await Task.Yield();
            AppendTrace("Attached explicit table ScrollViewer.");
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            RowsScrollViewer.ViewChanged -= OnScrollViewerViewChanged;
            RowsScrollViewer.SizeChanged -= OnRowsScrollViewerSizeChanged;
        }

        private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (SupportsCellSelection && e.Key == VirtualKey.C && e.KeyStatus.IsMenuKeyDown == false
                && InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
            {
                e.Handled = CopyCellSelection(includeHeaders: false);
                return;
            }

            if (SupportsCellSelection
                && e.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down
                && TryMoveCellSelection(e.Key,
                    InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down)))
            {
                e.Handled = true;
                return;
            }

            if (e.Key is not VirtualKey.Up and not VirtualKey.Down)
            {
                return;
            }

            e.Handled = MoveSelectionOrScroll(e.Key == VirtualKey.Down ? 1 : -1);
        }

        private bool TryMoveCellSelection(VirtualKey key, bool extendSelection)
        {
            if (_cellSelectionEnd is not { } end)
            {
                return false;
            }

            var rows = GetSourceRows();
            var rowOffset = key switch
            {
                VirtualKey.Up => -1,
                VirtualKey.Down => 1,
                _ => 0
            };
            var columnOffset = key switch
            {
                VirtualKey.Left => -1,
                VirtualKey.Right => 1,
                _ => 0
            };
            var target = new CbsTableCellPosition(end.RowIndex + rowOffset, end.ColumnIndex + columnOffset);
            if (target.RowIndex < 0 || target.RowIndex >= rows.Count
                || target.ColumnIndex < 0 || target.ColumnIndex >= Columns.Count
                || rows[target.RowIndex].IsPlaceholder)
            {
                return false;
            }

            if (!extendSelection)
            {
                _cellSelectionAnchor = target;
            }

            _cellSelectionEnd = target;
            UpdateVisibleCellSelectionStates();
            ScrollRowIntoView(target.RowIndex);
            return true;
        }

        private bool MoveSelectionOrScroll(int direction)
        {
            if (direction == 0)
            {
                return false;
            }

            if (SupportsRowSelection && TryMoveSelectedRow(direction))
            {
                return true;
            }

            return ScrollByRows(direction, 1);
        }

        private bool TryMoveSelectedRow(int direction)
        {
            if (SelectedItem is null || SelectedItem.IsPlaceholder)
            {
                return false;
            }

            var sourceRows = GetSourceRows();
            var selectedIndex = FindRowIndex(sourceRows, SelectedItem);
            if (selectedIndex < 0)
            {
                return false;
            }

            var targetIndex = selectedIndex + direction;
            if (targetIndex < 0
                || targetIndex >= sourceRows.Count
                || sourceRows[targetIndex].IsPlaceholder
                || !IsRowSelectable(sourceRows[targetIndex]))
            {
                return false;
            }

            SelectSingleRow(sourceRows[targetIndex], targetIndex);
            ScrollRowIntoView(targetIndex);
            return true;
        }

        private bool ScrollByRows(int direction, int rowCount)
        {
            if (RowHeight <= 0 || RowsScrollViewer.ExtentHeight <= RowsScrollViewer.ViewportHeight)
            {
                return false;
            }

            var maxOffset = Math.Max(0, RowsScrollViewer.ExtentHeight - RowsScrollViewer.ViewportHeight);
            var targetOffset = Math.Clamp(
                RowsScrollViewer.VerticalOffset + (direction * rowCount * RowHeight),
                0,
                maxOffset);

            if (Math.Abs(targetOffset - RowsScrollViewer.VerticalOffset) < 0.1)
            {
                return false;
            }

            RowsScrollViewer.ChangeView(null, targetOffset, null, disableAnimation: true);
            return true;
        }

        private void ScrollRowIntoView(int rowIndex)
        {
            if (RowHeight <= 0)
            {
                return;
            }

            var rowTop = rowIndex * RowHeight;
            var rowBottom = rowTop + RowHeight;
            var viewportTop = RowsScrollViewer.VerticalOffset;
            var viewportBottom = viewportTop + RowsScrollViewer.ViewportHeight;

            if (rowTop < viewportTop)
            {
                RowsScrollViewer.ChangeView(null, rowTop, null, disableAnimation: true);
                return;
            }

            if (rowBottom > viewportBottom)
            {
                var targetOffset = Math.Max(0, rowBottom - RowsScrollViewer.ViewportHeight);
                RowsScrollViewer.ChangeView(null, targetOffset, null, disableAnimation: true);
            }
        }

        private void SelectSingleRow(TableDataRow row, int rowIndex)
        {
            _selectedIndexes.Clear();
            _selectedIndexes.Add(rowIndex);
            SelectedItem = row;
            UpdateVisibleRowSelectionStates();
            RowSelectionChanged?.Invoke(
                this,
                new CbsTableRowSelectionChangedEventArgs(row, rowIndex, isSelected: true));
        }

        private static int FindRowIndex(IReadOnlyList<TableDataRow> sourceRows, TableDataRow row)
        {
            for (var index = 0; index < sourceRows.Count; index++)
            {
                if (ReferenceEquals(sourceRows[index], row))
                {
                    return index;
                }
            }

            return -1;
        }

        private async void OnScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            UpdateHeaderViewportCompensation();
            if (IsCurrentViewportWindowUnchanged(out var currentWindowTrace))
            {
                _sameViewportViewChangedCount++;
                if (_sameViewportViewChangedCount == 1 || _sameViewportViewChangedCount % 10 == 0)
                {
                    AppendTrace(
                        $"TABLE VIEWCHANGED SAME count={_sameViewportViewChangedCount} intermediate={e.IsIntermediate} {currentWindowTrace}");
                }
            }
            else
            {
                if (_sameViewportViewChangedCount > 0)
                {
                    AppendTrace($"TABLE VIEWCHANGED SAME END count={_sameViewportViewChangedCount}");
                    _sameViewportViewChangedCount = 0;
                }

                RebuildRows();
            }

            if (LoadedCount != _lastTriggeredLoadedCount)
            {
                _isLoadPending = false;
            }

            if (_isLoadPending || IsLoading || !HasMoreItems || LoadedCount <= 0 || TotalCount <= 0)
            {
                return;
            }

            var loadedBoundaryOffset = LoadedCount * RowHeight;
            var viewportBottom = RowsScrollViewer.VerticalOffset + RowsScrollViewer.ViewportHeight;
            if (viewportBottom < loadedBoundaryOffset)
            {
                return;
            }

            _isLoadPending = true;
            _lastTriggeredLoadedCount = LoadedCount;
            AppendTrace($"Trigger load more at offset={RowsScrollViewer.VerticalOffset:F1}, loaded={LoadedCount}/{TotalCount}.");

            try
            {
                LoadMoreRequested?.Invoke(this, new CbsTableLoadMoreRequestedEventArgs());
                await Task.Yield();
            }
            catch
            {
                _isLoadPending = false;
                throw;
            }
        }

        private void RebuildHeader()
        {
            HeaderGrid.Children.Clear();
            HeaderGrid.ColumnDefinitions.Clear();
            HeaderGrid.RowDefinitions.Clear();
            _filterTextBoxes.Clear();
            _filterBooleanCheckBoxes.Clear();
            _filterModeButtons.Clear();
            _filterMultiSelectButtons.Clear();
            _filterMultiSelectStates.Clear();

            if (Columns.Count == 0)
            {
                UpdateHeaderViewportCompensation();
                return;
            }

            HeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            HeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (var index = 0; index < Columns.Count; index++)
            {
                EnsureFilterState(Columns[index]);
                HeaderGrid.ColumnDefinitions.Add(CreateDataColumnDefinition(Columns[index]));
                var topCell = CreateHeaderTopCell(Columns[index]);
                Grid.SetColumn(topCell, index);
                Grid.SetRow(topCell, 0);
                HeaderGrid.Children.Add(topCell);

                var filterCell = CreateHeaderFilterCell(Columns[index]);
                Grid.SetColumn(filterCell, index);
                Grid.SetRow(filterCell, 1);
                HeaderGrid.Children.Add(filterCell);

                var splitter = CreateResizeHandle(index, index == Columns.Count - 1);
                Grid.SetColumn(splitter, index);
                Grid.SetRowSpan(splitter, 2);
                HeaderGrid.Children.Add(splitter);
            }

            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var fillerTop = CreateHeaderBackgroundCell(hasBottomBorder: true);
            fillerTop.BorderThickness = new Thickness(1, 0, 0, 1);
            Grid.SetColumn(fillerTop, Columns.Count);
            Grid.SetRow(fillerTop, 0);
            HeaderGrid.Children.Add(fillerTop);

            var fillerBottom = CreateHeaderBackgroundCell(hasBottomBorder: true);
            fillerBottom.BorderThickness = new Thickness(1, 0, 0, 1);
            Grid.SetColumn(fillerBottom, Columns.Count);
            Grid.SetRow(fillerBottom, 1);
            HeaderGrid.Children.Add(fillerBottom);

            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
            var scrollbarTop = CreateHeaderBackgroundCell(hasBottomBorder: true);
            Grid.SetColumn(scrollbarTop, Columns.Count + 1);
            Grid.SetRow(scrollbarTop, 0);
            HeaderGrid.Children.Add(scrollbarTop);

            var scrollbarBottom = CreateHeaderBackgroundCell(hasBottomBorder: true);
            Grid.SetColumn(scrollbarBottom, Columns.Count + 1);
            Grid.SetRow(scrollbarBottom, 1);
            HeaderGrid.Children.Add(scrollbarBottom);

            UpdateHeaderViewportCompensation();
        }

        private void RebuildRows()
        {
            var sourceRows = GetSourceRows();
            var totalRows = sourceRows.Count;
            var window = CalculateWindow(totalRows);

            if (TrySuppressEmptyRowsRender(totalRows, window.Start, window.End))
            {
                return;
            }

            if (window.Start == _lastWindowStart
                && window.End == _lastWindowEnd
                && totalRows == _lastSourceCount
                && ReferenceEquals(ItemsSource, _lastItemsSourceReference))
            {
                AppendTrace(
                    $"TABLE REBUILD SKIP {BuildWindowTrace(sourceRows, window.Start, window.End, totalRows)}");
                UpdateSpacerHeights(totalRows, window.Start, window.End);
                return;
            }

            var sequence = ++_rebuildSequence;
            var stopwatch = Stopwatch.StartNew();
            var rowCount = Math.Max(0, window.End - window.Start);
            var renderPath = ResolveRowsRenderPath(window.Start, window.End, totalRows);
            AppendTrace(
                $"TABLE REBUILD START seq={sequence} path={renderPath} window={window.Start}..{window.End} total={totalRows}");

            UpdateSpacerHeights(totalRows, window.Start, window.End);
            if (renderPath == RowsRenderPath.ScrollDown)
            {
                ConfigureScrolledRowsDown(sourceRows, window.Start, window.End);
            }
            else if (renderPath == RowsRenderPath.ScrollUp)
            {
                ConfigureScrolledRowsUp(sourceRows, window.Start, window.End);
            }
            else
            {
                ConfigureVisibleRows(sourceRows, window.Start, rowCount);
            }

            _lastWindowStart = window.Start;
            _lastWindowEnd = window.End;
            _lastSourceCount = totalRows;
            _lastItemsSourceReference = ItemsSource;

            stopwatch.Stop();
            AppendTrace(
                $"TABLE REBUILD END seq={sequence} path={renderPath} rows={rowCount} elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms");
            TrackLayoutCompletion(sequence, rowCount);
            PublishViewportChanged(window.Start, window.End, totalRows);
        }

        private void OnHeaderButtonClick(object sender, RoutedEventArgs e)
        {
            if (_suppressNextHeaderClick)
            {
                _suppressNextHeaderClick = false;
                return;
            }

            if (sender is not Button { Tag: CbsTableColumnDefinition column } || !column.IsSortable)
            {
                return;
            }

            DataSortDirection? nextDirection;
            if (!string.Equals(CurrentSortField, column.FieldKey, StringComparison.OrdinalIgnoreCase))
            {
                nextDirection = DataSortDirection.Ascending;
            }
            else
            {
                nextDirection = CurrentSortDirection switch
                {
                    DataSortDirection.Ascending => DataSortDirection.Descending,
                    DataSortDirection.Descending => null,
                    _ => DataSortDirection.Ascending
                };
            }

            SortRequested?.Invoke(this, new CbsTableSortRequestedEventArgs(column.FieldKey, nextDirection));
        }

        private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CbsTableView)d;
            control.ClearCellSelection();
            control.InvalidateWindowCache();
            control.RebuildHeader();
            control.RebuildRows();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CbsTableView)d;
            control.ClearCellSelection();
            control.InvalidateWindowCache();
            control.RebuildRows();
            if (control._lastSourceCount == 0 && control._rowPool.Count == 0)
            {
                return;
            }

            control.AppendTrace(
                $"TABLE ITEMS REFRESH {control.BuildCurrentWindowTrace()}");
        }

        private static void OnSortStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CbsTableView)d).RebuildHeader();
        }

        private static void OnTableStateKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CbsTableView)d;
            control.RebuildHeader();
        }

        private static void OnMultiSelectOptionsSourcesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CbsTableView)d;
            control.RefreshMultiSelectFilterStates();
            control.RebuildHeader();
        }

        private static void OnRowHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CbsTableView)d;
            control.InvalidateWindowCache();
            control.RebuildHeader();
            control.RebuildRows();
        }

        private static void OnDensityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CbsTableView)d;
            control.RowHeight = control.GetRowHeightForDensity((CbsTableDensity)e.NewValue);
            control.RebuildHeader();
            control.RebuildRows();
        }

        private static void OnRowStyleKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CbsTableView)d).RebuildRows();
        }

        private static void OnShowStageCostFractionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CbsTableView)d).InvalidateRows(new TableRenderRequest(TableRenderReason.ValueStyleChanged));
        }

        private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CbsTableView)d;
            if (!control.IsLoading)
            {
                control._isLoadPending = false;
            }

            if (control._lastSourceCount == 0 && control._rowPool.Count == 0)
            {
                return;
            }

            control.AppendTrace($"TABLE SKELETON REPAINT REQUEST isLoading={control.IsLoading}");
            control.RefreshVisibleRowsWithoutViewportChange();
            control.AppendTrace(
                $"TABLE LOADING REFRESH {control.BuildCurrentWindowTrace()}");
        }

        private static ColumnDefinition CreateDataColumnDefinition(CbsTableColumnDefinition column)
        {
            var width = column.EffectiveWidth;
            if (TryParseWidth(width, out var fixedWidth))
            {
                return new ColumnDefinition { Width = new GridLength(fixedWidth) };
            }

            if (string.Equals(width, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return new ColumnDefinition { Width = GridLength.Auto };
            }

            return new ColumnDefinition { Width = new GridLength(192) };
        }

        private static bool TryParseWidth(string? width, out double pixels)
        {
            pixels = 0;
            if (string.IsNullOrWhiteSpace(width))
            {
                return false;
            }

            if (double.TryParse(width, out pixels))
            {
                return true;
            }

            if (width.EndsWith("rem", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(width[..^3], out var rem))
            {
                pixels = rem * 16;
                return true;
            }

            if (width.EndsWith("px", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(width[..^2], out var px))
            {
                pixels = px;
                return true;
            }

            return false;
        }

        private void AppendTrace(string message)
        {
            TraceGenerated?.Invoke(this, new CbsTableTraceEventArgs(message));
        }

        private void InvalidateWindowCache()
        {
            _lastWindowStart = -1;
            _lastWindowEnd = -1;
            _lastSourceCount = -1;
            _sameViewportViewChangedCount = 0;
            _lastItemsSourceReference = null;
        }

        public bool RefreshVisibleRowsIfViewportHasPlaceholders()
        {
            var sourceRows = GetSourceRows();
            if (!TryGetCurrentWindowPlaceholderCount(sourceRows, out var rowCount, out _))
            {
                AppendTrace(
                    $"TABLE ITEMS REPAINT SKIP invalid-window window={_lastWindowStart}..{_lastWindowEnd} total={sourceRows.Count}");
                return false;
            }

            var configuredCount = 0;
            var renderedPlaceholderCount = 0;
            var safeRowCount = Math.Min(rowCount, _rowPool.Count);
            for (var poolIndex = 0; poolIndex < safeRowCount; poolIndex++)
            {
                var rowView = _rowPool[poolIndex];
                if (rowView.Row?.IsPlaceholder != true)
                {
                    continue;
                }

                renderedPlaceholderCount++;
                if (rowView.Tag is not int absoluteIndex
                    || absoluteIndex < 0
                    || absoluteIndex >= sourceRows.Count
                    || sourceRows[absoluteIndex].IsPlaceholder)
                {
                    continue;
                }

                ConfigureRowPoolRange(sourceRows, poolIndex, absoluteIndex, 1);
                configuredCount++;
            }

            if (renderedPlaceholderCount <= 0)
            {
                AppendTrace(
                    $"TABLE ITEMS REPAINT SKIP no-placeholders window={_lastWindowStart}..{_lastWindowEnd} rows={rowCount}");
                return false;
            }

            AppendTrace(
                $"TABLE ITEMS REPAINT renderedPlaceholders={renderedPlaceholderCount} configured={configuredCount} window={_lastWindowStart}..{_lastWindowEnd} rows={rowCount}");
            return configuredCount > 0;
        }

        private void RefreshVisibleRowsWithoutViewportChange()
        {
            var sourceRows = GetSourceRows();
            if (!TryGetCurrentWindowPlaceholderCount(sourceRows, out var rowCount, out var placeholderCount))
            {
                AppendTrace(
                    $"TABLE SKELETON REPAINT SKIP isLoading={IsLoading} window={_lastWindowStart}..{_lastWindowEnd} total={sourceRows.Count}");
                return;
            }

            RefreshVisibleRowsWithoutViewportChange(sourceRows, rowCount, placeholderCount);
        }

        private void RefreshVisibleRowsWithoutViewportChange(
            IReadOnlyList<TableDataRow> sourceRows,
            int rowCount,
            int placeholderCount)
        {
            AppendTrace(
                $"TABLE SKELETON REPAINT START isLoading={IsLoading} window={_lastWindowStart}..{_lastWindowEnd} rows={rowCount} placeholders={placeholderCount} rowPool={_rowPool.Count}");
            ConfigureVisibleRows(sourceRows, _lastWindowStart, rowCount);
            AppendTrace(
                $"TABLE SKELETON REPAINT END isLoading={IsLoading} window={_lastWindowStart}..{_lastWindowEnd} rows={rowCount} placeholders={placeholderCount} rowPool={_rowPool.Count}");
        }

        private void RefreshVisibleRowsForValueStyleChange()
        {
            var sourceRows = GetSourceRows();
            if (!TryGetCurrentWindowPlaceholderCount(sourceRows, out var rowCount, out var placeholderCount))
            {
                AppendTrace(
                    $"TABLE VALUE STYLE REPAINT SKIP window={_lastWindowStart}..{_lastWindowEnd} total={sourceRows.Count}");
                return;
            }

            AppendTrace(
                $"TABLE VALUE STYLE REPAINT START window={_lastWindowStart}..{_lastWindowEnd} rows={rowCount} placeholders={placeholderCount} rowPool={_rowPool.Count}");
            ConfigureVisibleRows(sourceRows, _lastWindowStart, rowCount);
            AppendTrace(
                $"TABLE VALUE STYLE REPAINT END window={_lastWindowStart}..{_lastWindowEnd} rows={rowCount} placeholders={placeholderCount} rowPool={_rowPool.Count}");
        }

        private bool TryGetCurrentWindowPlaceholderCount(
            IReadOnlyList<TableDataRow> sourceRows,
            out int rowCount,
            out int placeholderCount)
        {
            rowCount = 0;
            placeholderCount = 0;
            if (_lastWindowStart < 0
                || _lastWindowEnd < _lastWindowStart
                || _lastWindowEnd > sourceRows.Count)
            {
                return false;
            }

            rowCount = _lastWindowEnd - _lastWindowStart;
            placeholderCount = CountPlaceholders(sourceRows, _lastWindowStart, rowCount);
            return true;
        }

        private bool TrySuppressEmptyRowsRender(int totalRows, int windowStart, int windowEnd)
        {
            if (totalRows != 0 || _rowPool.Count > 0 || _lastSourceCount > 0)
            {
                return false;
            }

            UpdateSpacerHeights(0, 0, 0);
            _lastWindowStart = windowStart;
            _lastWindowEnd = windowEnd;
            _lastSourceCount = 0;
            _lastItemsSourceReference = ItemsSource;
            return true;
        }

        public bool RefreshVisibleRow(int absoluteIndex, TableDataRow row)
        {
            ArgumentNullException.ThrowIfNull(row);

            if (_lastWindowStart < 0
                || absoluteIndex < _lastWindowStart
                || absoluteIndex >= _lastWindowEnd)
            {
                AppendTrace(
                    $"TABLE ROW REPAINT SKIP index={absoluteIndex} window={_lastWindowStart}..{_lastWindowEnd}");
                return false;
            }

            var poolIndex = absoluteIndex - _lastWindowStart;
            if (poolIndex < 0
                || poolIndex >= _rowPool.Count
                || _rowPool[poolIndex].Tag is not int renderedIndex
                || renderedIndex != absoluteIndex)
            {
                AppendTrace(
                    $"TABLE ROW REPAINT SKIP stale-pool index={absoluteIndex} pool={poolIndex} window={_lastWindowStart}..{_lastWindowEnd}");
                return false;
            }

            _rowPool[poolIndex].Configure(
                row,
                Columns,
                RowHeight,
                RowStyleKey,
                ShowStageCostFraction);
            _rowPool[poolIndex].Tag = absoluteIndex;
            ApplyRowSelectionState(_rowPool[poolIndex], absoluteIndex);
            AppendTrace(
                $"TABLE ROW REPAINT index={absoluteIndex} pool={poolIndex} window={_lastWindowStart}..{_lastWindowEnd}");
            return true;
        }

        private RowsRenderPath ResolveRowsRenderPath(int nextStart, int nextEnd, int totalRows)
        {
            if (_lastWindowStart < 0
                || _lastWindowEnd < _lastWindowStart
                || _lastSourceCount != totalRows
                || !ReferenceEquals(ItemsSource, _lastItemsSourceReference))
            {
                return RowsRenderPath.Full;
            }

            var previousRowCount = _lastWindowEnd - _lastWindowStart;
            var nextRowCount = nextEnd - nextStart;
            if (previousRowCount <= 0
                || nextRowCount <= 0
                || previousRowCount != nextRowCount
                || _rowPool.Count != previousRowCount)
            {
                return RowsRenderPath.Full;
            }

            if (nextStart > _lastWindowStart && nextStart < _lastWindowEnd)
            {
                return RowsRenderPath.ScrollDown;
            }

            if (nextStart < _lastWindowStart && nextEnd > _lastWindowStart)
            {
                return RowsRenderPath.ScrollUp;
            }

            return RowsRenderPath.Full;
        }

        private void ConfigureScrolledRowsDown(IReadOnlyList<TableDataRow> sourceRows, int nextStart, int nextEnd)
        {
            var shift = nextStart - _lastWindowStart;
            var rowCount = nextEnd - nextStart;
            if (shift <= 0 || shift >= rowCount)
            {
                ConfigureVisibleRows(sourceRows, nextStart, rowCount);
                return;
            }

            for (var index = 0; index < shift; index++)
            {
                MoveFirstRowViewToEnd();
            }

            var firstNewIndex = Math.Max(_lastWindowEnd, nextStart);
            var newRowCount = Math.Max(0, nextEnd - firstNewIndex);
            var poolStart = rowCount - newRowCount;
            ConfigureRowPoolRange(sourceRows, poolStart, firstNewIndex, newRowCount);
            AppendTrace(
                $"TABLE SCROLL DOWN shift={shift} preserved={rowCount - newRowCount} configured={newRowCount}");
        }

        private void ConfigureScrolledRowsUp(IReadOnlyList<TableDataRow> sourceRows, int nextStart, int nextEnd)
        {
            var shift = _lastWindowStart - nextStart;
            var rowCount = nextEnd - nextStart;
            if (shift <= 0 || shift >= rowCount)
            {
                ConfigureVisibleRows(sourceRows, nextStart, rowCount);
                return;
            }

            for (var index = 0; index < shift; index++)
            {
                MoveLastRowViewToStart();
            }

            var firstNewIndex = nextStart;
            var newRowCount = Math.Max(0, _lastWindowStart - nextStart);
            ConfigureRowPoolRange(sourceRows, 0, firstNewIndex, newRowCount);
            AppendTrace(
                $"TABLE SCROLL UP shift={shift} preserved={rowCount - newRowCount} configured={newRowCount}");
        }

        private void ConfigureVisibleRows(IReadOnlyList<TableDataRow> sourceRows, int start, int rowCount)
        {
            EnsureRowPool(rowCount);

            ConfigureRowPoolRange(sourceRows, 0, start, rowCount);
        }

        private void ConfigureRowPoolRange(
            IReadOnlyList<TableDataRow> sourceRows,
            int poolStart,
            int sourceStart,
            int rowCount)
        {
            for (var index = 0; index < rowCount; index++)
            {
                var poolIndex = poolStart + index;
                var absoluteIndex = sourceStart + index;
                _rowPool[poolIndex].Configure(
                    sourceRows[absoluteIndex],
                    Columns,
                    RowHeight,
                    RowStyleKey,
                    ShowStageCostFraction);
                _rowPool[poolIndex].Tag = absoluteIndex;
                ApplyRowSelectionState(_rowPool[poolIndex], absoluteIndex);
            }
        }

        private void MoveFirstRowViewToEnd()
        {
            if (_rowPool.Count == 0)
            {
                return;
            }

            var rowView = _rowPool[0];
            _rowPool.RemoveAt(0);
            _rowPool.Add(rowView);
            RowsHost.Children.Remove(rowView);
            RowsHost.Children.Add(rowView);
        }

        private void MoveLastRowViewToStart()
        {
            if (_rowPool.Count == 0)
            {
                return;
            }

            var lastIndex = _rowPool.Count - 1;
            var rowView = _rowPool[lastIndex];
            _rowPool.RemoveAt(lastIndex);
            _rowPool.Insert(0, rowView);
            RowsHost.Children.Remove(rowView);
            RowsHost.Children.Insert(0, rowView);
        }

        private void PublishViewportChanged(int start, int end, int totalRows)
        {
            var retainedBufferRows = GetEffectiveRetainedBufferRows(end - start);
            AppendTrace(
                $"VIEWPORT CHANGED start={start} end={end} buffer={retainedBufferRows} total={totalRows} offset={RowsScrollViewer.VerticalOffset:F1}");
            ViewportChanged?.Invoke(this, new CbsTableViewportChangedEventArgs(
                start,
                end,
                retainedBufferRows));
        }

        private static int CountPlaceholders(IReadOnlyList<TableDataRow> sourceRows, int start, int rowCount)
        {
            var count = 0;
            var end = Math.Min(sourceRows.Count, start + rowCount);
            for (var index = Math.Max(0, start); index < end; index++)
            {
                if (sourceRows[index].IsPlaceholder)
                {
                    count++;
                }
            }

            return count;
        }

        private string BuildCurrentWindowTrace()
        {
            var sourceRows = GetSourceRows();
            var totalRows = sourceRows.Count;
            var window = CalculateWindow(totalRows);
            return BuildWindowTrace(sourceRows, window.Start, window.End, totalRows);
        }

        private string BuildWindowTrace(
            IReadOnlyList<TableDataRow> sourceRows,
            int windowStart,
            int windowEnd,
            int totalRows)
        {
            var rowCount = Math.Max(0, windowEnd - windowStart);
            var placeholderCount = CountPlaceholders(sourceRows, windowStart, rowCount);
            return
                $"window={windowStart}..{windowEnd} " +
                $"totalRows={totalRows} " +
                $"placeholders={placeholderCount} " +
                $"loaded={LoadedCount}/{TotalCount} " +
                $"hasMore={HasMoreItems}";
        }

        private IReadOnlyList<TableDataRow> GetSourceRows()
        {
            if (ItemsSource is IReadOnlyList<TableDataRow> readOnlyList)
            {
                return readOnlyList;
            }

            if (ItemsSource is IList<TableDataRow> list)
            {
                return list.ToList();
            }

            return ItemsSource?.OfType<TableDataRow>().ToList() ?? [];
        }

        private bool IsCurrentViewportWindowUnchanged(out string trace)
        {
            var sourceRows = GetSourceRows();
            var totalRows = sourceRows.Count;
            var window = CalculateWindow(totalRows);
            trace =
                $"{BuildWindowTrace(sourceRows, window.Start, window.End, totalRows)} " +
                $"offset={RowsScrollViewer.VerticalOffset:F1} viewport={RowsScrollViewer.ViewportHeight:F1} extent={RowsScrollViewer.ExtentHeight:F1}";

            return window.Start == _lastWindowStart
                && window.End == _lastWindowEnd
                && totalRows == _lastSourceCount
                && ReferenceEquals(ItemsSource, _lastItemsSourceReference);
        }

        private (int Start, int End) CalculateWindow(int totalRows)
        {
            if (totalRows <= 0 || RowHeight <= 0)
            {
                return (0, 0);
            }

            var viewportHeight = RowsScrollViewer.ViewportHeight;
            if (viewportHeight <= 0)
            {
                viewportHeight = 12 * RowHeight;
            }

            var firstVisibleIndex = Math.Max(0, (int)Math.Floor(RowsScrollViewer.VerticalOffset / RowHeight));
            var visibleRowCount = Math.Max(1, (int)Math.Ceiling(viewportHeight / RowHeight));
            var windowRows = visibleRowCount + (WindowBufferRows * 2);
            var viewportBottom = RowsScrollViewer.VerticalOffset + viewportHeight;
            var totalHeight = totalRows * RowHeight;
            if (viewportBottom >= totalHeight - RowHeight)
            {
                var bottomStart = Math.Max(0, totalRows - windowRows);
                return (bottomStart, totalRows);
            }

            var rawStart = Math.Max(0, firstVisibleIndex - WindowBufferRows);
            var alignedStart = (rawStart / WindowStepRows) * WindowStepRows;
            var maxStart = Math.Max(0, totalRows - 1);
            var start = Math.Min(Math.Max(0, alignedStart), maxStart);
            var end = Math.Min(totalRows, start + windowRows);
            return (start, Math.Max(start, end));
        }

        private void UpdateSpacerHeights(int totalRows, int start, int end)
        {
            TopSpacer.Height = Math.Max(0, start * RowHeight);
            BottomSpacer.Height = Math.Max(0, (totalRows - end) * RowHeight);
        }

        private void EnsureRowPool(int rowCount)
        {
            while (_rowPool.Count > rowCount)
            {
                var index = _rowPool.Count - 1;
                RowsHost.Children.RemoveAt(index);
                _rowPool.RemoveAt(index);
            }

            while (_rowPool.Count < rowCount)
            {
                var rowView = new CbsTableRowView();
                rowView.PointerEntered += OnRowPointerEntered;
                rowView.PointerExited += OnRowPointerExited;
                rowView.PointerPressed += OnRowPointerPressed;
                rowView.PointerMoved += OnRowPointerMoved;
                rowView.PointerReleased += OnRowPointerReleased;
                rowView.Tapped += OnRowTapped;
                rowView.DoubleTapped += OnRowDoubleTapped;
                _rowPool.Add(rowView);
                RowsHost.Children.Add(rowView);
            }
        }

        private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!SupportsRowSelection || sender is not CbsTableRowView rowView)
            {
                return;
            }

            rowView.IsHovered = true;
        }

        private void OnRowPointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not CbsTableRowView rowView)
            {
                return;
            }

            rowView.IsHovered = false;
            rowView.IsPressed = false;
        }

        private void OnRowPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not CbsTableRowView rowView)
            {
                return;
            }

            if (SupportsRowSelection)
            {
                rowView.IsPressed = true;
            }

            if (!SupportsCellSelection || rowView.Tag is not int rowIndex || rowView.Row?.IsPlaceholder == true)
            {
                return;
            }

            var point = e.GetCurrentPoint(rowView);
            if (!point.Properties.IsLeftButtonPressed)
            {
                return;
            }

            var columnIndex = rowView.GetColumnIndex(point.Position);
            if (columnIndex < 0)
            {
                return;
            }

            var position = new CbsTableCellPosition(rowIndex, columnIndex);
            if (!e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift) || _cellSelectionAnchor is null)
            {
                _cellSelectionAnchor = position;
            }

            _cellSelectionEnd = position;
            _isDraggingCellSelection = true;
            rowView.CapturePointer(e.Pointer);
            Focus(FocusState.Programmatic);
            UpdateVisibleCellSelectionStates();
        }

        private void OnRowPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingCellSelection || sender is not CbsTableRowView rowView)
            {
                return;
            }

            var sourceRows = GetSourceRows();
            if (sourceRows.Count == 0 || rowView.Tag is not int originRowIndex)
            {
                return;
            }

            var point = e.GetCurrentPoint(rowView);
            var columnIndex = rowView.GetColumnIndex(point.Position);
            var rowIndex = Math.Clamp(originRowIndex + (int)Math.Floor(point.Position.Y / RowHeight), 0, sourceRows.Count - 1);
            if (columnIndex < 0 || sourceRows[rowIndex].IsPlaceholder)
            {
                return;
            }

            _cellSelectionEnd = new CbsTableCellPosition(rowIndex, columnIndex);
            UpdateVisibleCellSelectionStates();
        }

        private void OnRowPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not CbsTableRowView rowView)
            {
                return;
            }

            rowView.IsPressed = false;
            if (_isDraggingCellSelection)
            {
                _isDraggingCellSelection = false;
                rowView.ReleasePointerCapture(e.Pointer);
            }
        }

        private void OnRowTapped(object sender, TappedRoutedEventArgs e)
        {
            if (!SupportsRowSelection || sender is not CbsTableRowView rowView || rowView.Tag is not int rowIndex)
            {
                return;
            }

            if (rowView.Row?.IsPlaceholder == true)
            {
                return;
            }

            if (!IsRowSelectable(rowView.Row!))
            {
                return;
            }

            if (SupportsMultipleRowSelection)
            {
                if (!_selectedIndexes.Add(rowIndex))
                {
                    _selectedIndexes.Remove(rowIndex);
                }

                RowSelectionChanged?.Invoke(
                    this,
                    new CbsTableRowSelectionChangedEventArgs(rowView.Row, rowIndex, isSelected: _selectedIndexes.Contains(rowIndex)));
            }
            else
            {
                if (_selectedIndexes.Contains(rowIndex))
                {
                    if (SupportsCellSelection)
                    {
                        UpdateVisibleRowSelectionStates();
                        return;
                    }

                    _selectedIndexes.Clear();
                    SelectedItem = null;
                    RowSelectionChanged?.Invoke(
                        this,
                        new CbsTableRowSelectionChangedEventArgs(null, rowIndex, isSelected: false));
                    UpdateVisibleRowSelectionStates();
                    return;
                }

                Focus(FocusState.Programmatic);
                SelectSingleRow(rowView.Row!, rowIndex);
            }

            UpdateVisibleRowSelectionStates();
        }

        private void OnRowDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (!SupportsRowSelection || sender is not CbsTableRowView rowView || rowView.Tag is not int rowIndex)
            {
                return;
            }

            if (rowView.Row?.IsPlaceholder == true)
            {
                return;
            }

            if (!IsRowSelectable(rowView.Row!))
            {
                return;
            }

            Focus(FocusState.Programmatic);
            SelectSingleRow(rowView.Row!, rowIndex);

            RowDoubleTapped?.Invoke(this, new CbsTableRowDoubleTappedEventArgs(rowView.Row!, rowIndex));
        }

        private bool IsRowSelectable(TableDataRow row)
        {
            return CanSelectRow?.Invoke(row) ?? true;
        }

        private void UpdateVisibleRowSelectionStates()
        {
            foreach (var rowView in _rowPool)
            {
                if (rowView.Tag is int rowIndex)
                {
                    ApplyRowSelectionState(rowView, rowIndex);
                }
            }
        }

        private void ApplyRowSelectionState(CbsTableRowView rowView, int rowIndex)
        {
            rowView.IsSelected = _selectedIndexes.Contains(rowIndex);
            ApplyCellSelectionState(rowView, rowIndex);
        }

        private void UpdateVisibleCellSelectionStates()
        {
            foreach (var rowView in _rowPool)
            {
                if (rowView.Tag is int rowIndex)
                {
                    ApplyCellSelectionState(rowView, rowIndex);
                }
            }
        }

        private void ClearCellSelection()
        {
            _cellSelectionAnchor = null;
            _cellSelectionEnd = null;
            _isDraggingCellSelection = false;
            UpdateVisibleCellSelectionStates();
        }

        private void ApplyCellSelectionState(CbsTableRowView rowView, int rowIndex)
        {
            if (!TryGetCellSelectionBounds(out var rowStart, out var rowEnd, out var columnStart, out var columnEnd)
                || rowIndex < rowStart || rowIndex > rowEnd)
            {
                rowView.SetCellSelection(-1, -1);
                return;
            }

            rowView.SetCellSelection(columnStart, columnEnd);
        }

        private bool TryGetCellSelectionBounds(out int rowStart, out int rowEnd, out int columnStart, out int columnEnd)
        {
            rowStart = rowEnd = columnStart = columnEnd = -1;
            if (_cellSelectionAnchor is not { } anchor || _cellSelectionEnd is not { } end)
            {
                return false;
            }

            rowStart = Math.Min(anchor.RowIndex, end.RowIndex);
            rowEnd = Math.Max(anchor.RowIndex, end.RowIndex);
            columnStart = Math.Min(anchor.ColumnIndex, end.ColumnIndex);
            columnEnd = Math.Max(anchor.ColumnIndex, end.ColumnIndex);
            return true;
        }

        private MenuFlyout CreateCellSelectionContextMenu()
        {
            var menu = new MenuFlyout();
            var copyItem = new MenuFlyoutItem { Text = "Копировать" };
            copyItem.Click += (_, _) => CopyCellSelection(includeHeaders: false);
            menu.Items.Add(copyItem);
            var copyWithHeadersItem = new MenuFlyoutItem { Text = "Копировать с заголовками" };
            copyWithHeadersItem.Click += (_, _) => CopyCellSelection(includeHeaders: true);
            menu.Items.Add(copyWithHeadersItem);
            menu.Opening += (_, _) =>
            {
                var hasSelection = _cellSelectionAnchor is not null && _cellSelectionEnd is not null;
                copyItem.IsEnabled = hasSelection;
                copyWithHeadersItem.IsEnabled = hasSelection;
            };
            return menu;
        }

        private bool CopyCellSelection(bool includeHeaders)
        {
            if (!TryGetCellSelectionBounds(out var rowStart, out var rowEnd, out var columnStart, out var columnEnd))
            {
                return false;
            }

            var rows = GetSourceRows();
            var text = new StringBuilder();
            if (includeHeaders)
            {
                AppendClipboardLine(text, Enumerable.Range(columnStart, columnEnd - columnStart + 1)
                    .Select(index => Columns[index].Header));
            }

            for (var rowIndex = rowStart; rowIndex <= rowEnd; rowIndex++)
            {
                AppendClipboardLine(text, Enumerable.Range(columnStart, columnEnd - columnStart + 1)
                    .Select(columnIndex => CbsTableRowView.GetCellText(Columns[columnIndex], rows[rowIndex], ShowStageCostFraction)));
            }

            var package = new DataPackage();
            package.SetText(text.ToString());
            Clipboard.SetContent(package);
            return true;
        }

        private static void AppendClipboardLine(StringBuilder text, IEnumerable<string> values)
        {
            if (text.Length > 0)
            {
                text.AppendLine();
            }

            text.AppendJoin('\t', values.Select(static value => value.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ")));
        }

        private int GetEffectiveRetainedBufferRows(int currentWindowRows)
        {
            var minimumBuffer = Math.Max(1, currentWindowRows);
            if (RetainedBufferRows > 0)
            {
                return Math.Max(RetainedBufferRows, minimumBuffer);
            }

            var automaticBuffer = currentWindowRows + (int)Math.Ceiling(currentWindowRows * 0.5);
            return Math.Max(automaticBuffer, minimumBuffer);
        }

        private Thickness GetHeaderPadding(bool reserveFilterButton)
        {
            var padding = Density switch
            {
                CbsTableDensity.Comfortable => new Thickness(8, 4, 8, 4),
                CbsTableDensity.Standard => new Thickness(6, 3, 6, 3),
                _ => new Thickness(4, 2, 4, 2)
            };

            return reserveFilterButton
                ? new Thickness(padding.Left, padding.Top, Math.Max(2, padding.Right - 2), padding.Bottom)
                : padding;
        }

        private double GetHeaderFontSize()
        {
            return Density switch
            {
                CbsTableDensity.Comfortable => 13,
                CbsTableDensity.Standard => 12.5,
                _ => 12
            };
        }

        private double GetRowHeightForDensity(CbsTableDensity density)
        {
            return density switch
            {
                CbsTableDensity.Comfortable => 30,
                CbsTableDensity.Standard => 26,
                _ => 22
            };
        }

        private void TrackLayoutCompletion(int sequence, int rowCount)
        {
            EventHandler<object>? handler = null;
            handler = (_, _) =>
            {
                if (sequence <= _completedLayoutSequence)
                {
                    RowsHost.LayoutUpdated -= handler;
                    return;
                }

                _completedLayoutSequence = sequence;
                RowsHost.LayoutUpdated -= handler;
                AppendTrace(
                    $"TABLE LAYOUT END seq={sequence} rows={rowCount} extent={RowsScrollViewer.ExtentHeight:F1} viewport={RowsScrollViewer.ViewportHeight:F1} offset={RowsScrollViewer.VerticalOffset:F1}");
            };

            RowsHost.LayoutUpdated += handler;
        }

        private void OnRowsScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
        {
            AppendTrace(
                $"TABLE VIEWPORT SIZE old={e.PreviousSize.Width:F1}x{e.PreviousSize.Height:F1} new={e.NewSize.Width:F1}x{e.NewSize.Height:F1}");
            UpdateHeaderViewportCompensation();
        }

        private void UpdateHeaderViewportCompensation()
        {
            if (HeaderGridTransform is not null)
            {
                HeaderGridTransform.X = -RowsScrollViewer.HorizontalOffset;
            }

            if (HeaderGrid.ColumnDefinitions.Count == 0)
            {
                return;
            }

            var scrollbarWidth = GetVerticalScrollbarCompensationWidth();
            HeaderGrid.Margin = new Thickness(
                -HeaderSideBorderCompensation,
                0,
                -HeaderSideBorderCompensation,
                0);

            var scrollbarColumnIndex = HeaderGrid.ColumnDefinitions.Count - 1;
            HeaderGrid.ColumnDefinitions[scrollbarColumnIndex].Width = new GridLength(scrollbarWidth);
        }

        private double GetVerticalScrollbarCompensationWidth()
        {
            if (RowsScrollViewer.ViewportWidth <= 0 || RowsScrollViewer.ActualWidth <= 0)
            {
                return 0;
            }

            var compensation = RowsScrollViewer.ActualWidth - RowsScrollViewer.ViewportWidth;
            return Math.Max(0, Math.Ceiling(compensation));
        }

        private void OnColumnResizeStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            if (sender is not FrameworkElement { Tag: int columnIndex })
            {
                return;
            }

            _activeResizeColumnIndex = columnIndex;
            _activeResizeStartWidth = GetColumnPixelWidth(Columns[columnIndex]);
            _suppressNextHeaderClick = false;
        }

        private void OnColumnResizeDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (_activeResizeColumnIndex < 0 || _activeResizeColumnIndex >= Columns.Count)
            {
                return;
            }

            var nextWidth = Math.Max(
                MinimumColumnWidth,
                _activeResizeStartWidth + e.Cumulative.Translation.X);

            Columns[_activeResizeColumnIndex].Width = FormatPixelWidth(nextWidth);
            ApplyColumnWidth(_activeResizeColumnIndex, nextWidth);
            _suppressNextHeaderClick = true;
        }

        private void OnColumnResizeCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            if (_activeResizeColumnIndex < 0 || _activeResizeColumnIndex >= Columns.Count)
            {
                return;
            }

            var column = Columns[_activeResizeColumnIndex];
            var finalWidth = GetColumnPixelWidth(column);
            ColumnWidthChanged?.Invoke(
                this,
                new CbsTableColumnWidthChangedEventArgs(
                    column.FieldKey,
                    column.Width,
                    finalWidth));

            _activeResizeColumnIndex = -1;
            _activeResizeStartWidth = 0;
            ProtectedCursor = null;
        }

        private void ApplyColumnWidth(int columnIndex, double width)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count)
            {
                return;
            }

            if (columnIndex < HeaderGrid.ColumnDefinitions.Count)
            {
                HeaderGrid.ColumnDefinitions[columnIndex].Width = new GridLength(width);
            }

            foreach (var rowView in _rowPool)
            {
                rowView.SetColumnWidth(columnIndex, width);
            }
        }

        private static double GetColumnPixelWidth(CbsTableColumnDefinition column)
        {
            if (TryParseWidth(column.EffectiveWidth, out var width))
            {
                return width;
            }

            return 192d;
        }

        private static string FormatPixelWidth(double width)
        {
            return $"{Math.Round(width)}px";
        }

        private Border CreateResizeHandle(int columnIndex, bool isLastColumn)
        {
            var splitter = new Border
            {
                Width = ResizeHandleWidth,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Tag = columnIndex
            };

            splitter.ManipulationMode = ManipulationModes.TranslateX;
            splitter.ManipulationStarted += OnColumnResizeStarted;
            splitter.ManipulationDelta += OnColumnResizeDelta;
            splitter.ManipulationCompleted += OnColumnResizeCompleted;
            splitter.PointerEntered += OnColumnResizePointerEntered;
            splitter.PointerExited += OnColumnResizePointerExited;

            var host = new Grid
            {
                IsHitTestVisible = false
            };

            if (!isLastColumn)
            {
                host.Children.Add(new Border
                {
                    Width = 1,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Background = (Brush)Application.Current.Resources["ShellTableGridLineBrush"]
                });
            }

            splitter.Child = host;
            return splitter;
        }

        private void OnColumnResizePointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border)
            {
                ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
            }
        }

        private void OnColumnResizePointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Border && _activeResizeColumnIndex < 0)
            {
                ProtectedCursor = null;
            }
        }

        private FrameworkElement CreateHeaderTopCell(CbsTableColumnDefinition column)
        {
            var contentGrid = new Grid
            {
                Background = (Brush)Application.Current.Resources["ShellTableHeaderBackgroundBrush"]
            };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (SupportsFilterModeButton(column))
            {
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(FilterModeButtonWidth) });
            }

            var mainContent = CreateHeaderSortHost(column);
            Grid.SetColumn(mainContent, 0);
            contentGrid.Children.Add(mainContent);

            if (SupportsFilterModeButton(column))
            {
                var modeButton = CreateFilterModeButton(column);
                Grid.SetColumn(modeButton, 1);
                contentGrid.Children.Add(modeButton);
                _filterModeButtons[column.FieldKey] = modeButton;
            }

            var border = CreateHeaderBackgroundCell(hasBottomBorder: true);
            border.Child = contentGrid;
            return border;
        }

        private FrameworkElement CreateHeaderFilterCell(CbsTableColumnDefinition column)
        {
            var border = CreateHeaderBackgroundCell(hasBottomBorder: true);
            if (!column.IsFilterable)
            {
                return border;
            }

            if (column.Filter.EditorKind == CbsTableFilterEditorKind.MultiSelect)
            {
                border.Child = CreateMultiSelectFilterButton(column);
                return border;
            }

            if (column.Filter.EditorKind == CbsTableFilterEditorKind.Boolean)
            {
                var checkBox = CreateBooleanFilterCheckBox(column);
                border.Child = checkBox;
                _filterBooleanCheckBoxes[column.FieldKey] = checkBox;
                return border;
            }

            if (IsDateFilterMode(column.Filter.Mode))
            {
                border.Child = CreateDateTimeFilterHost(column);
                return border;
            }

            var textBox = CreateTextFilterTextBox(column);
            border.Child = textBox;
            _filterTextBoxes[column.FieldKey] = textBox;
            return border;
        }

        private CheckBox CreateBooleanFilterCheckBox(CbsTableColumnDefinition column)
        {
            var checkBox = new CheckBox
            {
                Tag = column,
                IsThreeState = true,
                IsChecked = TryGetBooleanFilterValue(column.Filter.Value),
                MinWidth = 24,
                MaxHeight = 24,     
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Foreground = GetFilterForegroundBrush(column),
                Background = GetFilterBackgroundBrush(column)
            };
            ToolTipService.SetToolTip(checkBox, "Фильтр: все / да / нет");
            checkBox.Checked += OnBooleanFilterCheckBoxChanged;
            checkBox.Unchecked += OnBooleanFilterCheckBoxChanged;
            checkBox.Indeterminate += OnBooleanFilterCheckBoxChanged;
            return checkBox;
        }

        private TextBox CreateTextFilterTextBox(CbsTableColumnDefinition column)
        {
            var textBox = new TextBox
            {
                Tag = column,
                Height = FilterTextBoxHeight,
                MinHeight = FilterTextBoxHeight,
                Margin = new Thickness(4, 1, 4, 1),
                Padding = new Thickness(6, 2, 6, 0),
                Text = GetFilterText(column),
                PlaceholderText = IsDateFilterMode(column.Filter.Mode)
                    ? GetDateTimePlaceholder(column)
                    : column.Filter.PlaceholderText,
                FontSize = Math.Max(11, GetHeaderFontSize() - 1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(1),
                BorderBrush = GetFilterBorderBrush(column),
                Background = GetFilterBackgroundBrush(column),
                Foreground = GetFilterForegroundBrush(column),
                MinWidth = 24
            };
            if (column.Filter.Mode == DataFilterMode.Numeric)
            {
                textBox.BeforeTextChanging += OnNumericFilterTextBoxBeforeTextChanging;
            }
            else if (IsDateFilterMode(column.Filter.Mode))
            {
                textBox.BeforeTextChanging += OnDateTimeFilterTextBoxBeforeTextChanging;
            }

            textBox.TextChanged += OnFilterTextChanged;
            return textBox;
        }

        private FrameworkElement CreateDateTimeFilterHost(CbsTableColumnDefinition column)
        {
            var textBox = CreateTextFilterTextBox(column);
            var datePicker = CreateDateTimeFilterDatePicker(column);
            var clearButton = CreateDateTimeFilterClearButton(column);

            var host = new Grid();
            host.Children.Add(textBox);
            host.Children.Add(datePicker);
            host.Children.Add(clearButton);

            _filterTextBoxes[column.FieldKey] = textBox;
            _filterDateTimeStates[column.FieldKey] = new DateTimeFilterUiState
            {
                Column = column,
                TextBox = textBox,
                DatePicker = datePicker,
                ClearButton = clearButton
            };

            RefreshDateTimeFilterTextBox(column);
            return host;
        }

        private CalendarDatePicker CreateDateTimeFilterDatePicker(CbsTableColumnDefinition column)
        {
            var datePicker = new CalendarDatePicker
            {
                Tag = column,
                Height = FilterDatePickerHeight,
                MinHeight = FilterDatePickerHeight,
                Margin = new Thickness(4, 1, 4, 1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 24,
                Date = TryGetDateFilterValue(column.Filter.Value),
                PlaceholderText = string.Empty,
                Padding = new Thickness(0, 0, 24, 0),
                BorderBrush = GetFilterBorderBrush(column),
                Background = GetFilterBackgroundBrush(column),
                Foreground = GetFilterForegroundBrush(column)
            };

            datePicker.DateChanged += OnDateTimeFilterDateChanged;
            return datePicker;
        }

        private Button CreateDateTimeFilterClearButton(CbsTableColumnDefinition column)
        {
            var button = new Button
            {
                Tag = column,
                Width = 20,
                Height = 20,
                MinWidth = 20,
                MinHeight = 20,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Content = new FontIcon
                {
                    Glyph = "\uE711"
                }
            };

            ToolTipService.SetToolTip(button, "Очистить фильтр даты");
            button.Click += OnDateTimeFilterClearButtonClick;
            return button;
        }

        private Button CreateMultiSelectFilterButton(CbsTableColumnDefinition column)
        {
            var button = new Button
            {
                Tag = column,
                Height = MultiSelectFilterButtonHeight,
                MinHeight = MultiSelectFilterButtonHeight,
                Margin = new Thickness(4, 1, 4, 1),
                Padding = new Thickness(8, 1, 8, 1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(1),
                BorderBrush = GetFilterBorderBrush(column),
                Background = GetFilterBackgroundBrush(column),
                Foreground = GetFilterForegroundBrush(column),
                MinWidth = 24
            };

            var searchTextBox = new TextBox
            {
                PlaceholderText = "Поиск",
                Height = MultiSelectFilterSearchHeight,
                MinHeight = MultiSelectFilterSearchHeight,
                Padding = new Thickness(6, 1, 6, 0),
                FontSize = Math.Max(11, GetHeaderFontSize() - 1),
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0)
            };

            var clearButton = CreateMultiSelectFilterFlyoutActionButton(FilterIconFactory.BuildFilterClearIcon(), "очистить");
            clearButton.Click += OnMultiSelectClearButtonClick;

            var closeButton = CreateMultiSelectFilterFlyoutActionButton(
                new FontIcon
                {
                    Glyph = "\uE711",
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 10
                },
                "закрыть");
            closeButton.HorizontalAlignment = HorizontalAlignment.Right;
            closeButton.VerticalAlignment = VerticalAlignment.Top;
            closeButton.Margin = new Thickness(0, -10, -10, 0);
            closeButton.BorderThickness = new Thickness(0);
            closeButton.Click += OnMultiSelectCloseButtonClick;

            var flyoutHeader = new Grid
            {
                Width = MultiSelectFilterFlyoutWidth * MultiSelectFilterHeaderWidthFactor,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 8),
                ColumnSpacing = 4
            };
            flyoutHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            flyoutHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(searchTextBox, 0);
            flyoutHeader.Children.Add(searchTextBox);
            Grid.SetColumn(clearButton, 1);
            flyoutHeader.Children.Add(clearButton);

            var optionsHost = new StackPanel
            {
                Spacing = 2
            };

            var scrollViewer = new ScrollViewer
            {
                MaxHeight = MultiSelectFilterFlyoutMaxHeight,
                VerticalScrollMode = ScrollMode.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = optionsHost
            };

            var flyoutBody = new StackPanel
            {
                Width = MultiSelectFilterFlyoutWidth,
                Spacing = 0,
                Children =
                {
                    flyoutHeader,
                    scrollViewer
                }
            };

            var flyoutContent = new Grid
            {
                Width = MultiSelectFilterFlyoutWidth,
                Children =
                {
                    flyoutBody,
                    closeButton
                }
            };

            var flyout = new Flyout
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                Content = flyoutContent
            };

            var state = new MultiSelectFilterUiState
            {
                Column = column,
                Button = button,
                SearchTextBox = searchTextBox,
                OptionsHost = optionsHost,
                AvailableOptions = GetMultiSelectOptions(column).ToList(),
                SelectedOptions = GetMultiSelectOptions(column)
                    .Where(option => NormalizeFilterSelectedValues(column.Filter.Value).Any(value => AreFilterValuesEqual(value, option.Value)))
                    .ToList(),
                SelectedValues = NormalizeFilterSelectedValues(column.Filter.Value),
                SearchText = string.Empty
            };

            searchTextBox.Tag = state;
            flyoutContent.Tag = state;
            clearButton.Tag = state;
            closeButton.Tag = state;
            searchTextBox.TextChanged += OnMultiSelectSearchTextChanged;
            flyout.Opened += OnMultiSelectFlyoutOpened;

            button.Flyout = flyout;
            _filterMultiSelectButtons[column.FieldKey] = button;
            _filterMultiSelectStates[column.FieldKey] = state;
            UpdateMultiSelectFilterButtonContent(button, column);
            RebuildMultiSelectOptionItems(state);
            return button;
        }

        private Button CreateMultiSelectFilterFlyoutActionButton(UIElement icon, string tooltip)
        {
            var button = new Button
            {
                Width = MultiSelectFilterActionButtonSize,
                Height = MultiSelectFilterActionButtonSize,
                MinWidth = MultiSelectFilterActionButtonSize,
                MinHeight = MultiSelectFilterActionButtonSize,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Content = icon
            };
            ToolTipService.SetToolTip(button, tooltip);
            return button;
        }

        private Border CreateHeaderBackgroundCell(bool hasBottomBorder)
        {
            return new Border
            {
                Background = (Brush)Application.Current.Resources["ShellTableHeaderBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ShellTableGridLineBrush"],
                BorderThickness = new Thickness(0, 0, 0, hasBottomBorder ? 1 : 0)
            };
        }

        private FrameworkElement CreateHeaderSortHost(CbsTableColumnDefinition column)
        {
            if (!column.IsSortable)
            {
                return new Border
                {
                    Padding = GetHeaderPadding(SupportsFilterModeButton(column)),
                    Child = CreateHeaderTitleContent(column)
                };
            }

            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = GetHeaderPadding(SupportsFilterModeButton(column)),
                BorderThickness = new Thickness(0),
                Background = (Brush)Application.Current.Resources["ShellTableHeaderBackgroundBrush"],
                Tag = column,
                Content = CreateHeaderTitleContent(column)
            };
            button.Click += OnHeaderButtonClick;
            return button;
        }

        private FrameworkElement CreateHeaderTitleContent(CbsTableColumnDefinition column)
        {
            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HeaderAdornmentWidth) });

            var titleText = new TextBlock
            {
                FontWeight = FontWeights.SemiBold,
                FontSize = GetHeaderFontSize(),
                Foreground = (Brush)Application.Current.Resources["ShellTableHeaderTextBrush"],
                Text = column.Header,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleText, 0);
            contentGrid.Children.Add(titleText);

            var adornmentHost = new Grid
            {
                Width = HeaderAdornmentWidth,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (column.IsSortable && string.Equals(CurrentSortField, column.FieldKey, StringComparison.OrdinalIgnoreCase))
            {
                adornmentHost.Children.Add(new FontIcon
                {
                    Glyph = CurrentSortDirection == DataSortDirection.Descending ? "\uE70D" : "\uE70E",
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 10,
                    Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            Grid.SetColumn(adornmentHost, 1);
            contentGrid.Children.Add(adornmentHost);
            return contentGrid;
        }

        private Button CreateFilterModeButton(CbsTableColumnDefinition column)
        {
            var button = new Button
            {
                Width = FilterModeButtonWidth,
                Height = 20,
                Margin = new Thickness(0, 0, 2, 0),
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(0),
                Background = (Brush)Application.Current.Resources["ShellTableHeaderBackgroundBrush"],
                Tag = column
            };

            UpdateFilterModeButtonContent(button, GetFilterMode(column));

            var flyout = new MenuFlyout();
            foreach (var mode in GetSupportedFilterModes(column))
            {
                var item = new MenuFlyoutItem
                {
                    Text = GetFilterModeLabel(column, mode),
                    Tag = (column, mode)
                };
                item.Click += OnFilterModeMenuItemClick;
                flyout.Items.Add(item);
            }

            button.Flyout = flyout;
            return button;
        }

        private void OnFilterModeMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem { Tag: ValueTuple<CbsTableColumnDefinition, DataFilterMatchMode> payload })
            {
                return;
            }

            var (column, mode) = payload;
            _filterModes[GetFilterStateKey(column)] = mode;
            column.Filter.MatchMode = mode;

            if (_filterModeButtons.TryGetValue(column.FieldKey, out var button))
            {
                UpdateFilterModeButtonContent(button, mode);
            }

            RefreshDateTimeFilterTextBox(column);

            FilterRequested?.Invoke(
                this,
                new CbsTableFilterRequestedEventArgs(
                    column.FieldKey,
                    mode,
                    GetFilterValue(column)));
        }

        private void OnFilterTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox { Tag: CbsTableColumnDefinition column } textBox)
            {
                return;
            }

            _filterTexts[GetFilterStateKey(column)] = textBox.Text;
            column.Filter.Value = GetFilterValue(column);
            textBox.BorderBrush = GetFilterBorderBrush(column);
            textBox.Background = GetFilterBackgroundBrush(column);
            textBox.Foreground = GetFilterForegroundBrush(column);

            if (_suppressFilterNotifications)
            {
                return;
            }

            FilterRequested?.Invoke(
                this,
                new CbsTableFilterRequestedEventArgs(
                    column.FieldKey,
                    GetFilterMode(column),
                    GetFilterValue(column)));
        }

        private void OnDateTimeFilterTextBoxBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            if (sender.Tag is not CbsTableColumnDefinition column
                || !IsMaskedDateTimeMode(column))
            {
                return;
            }
        }

        private void RefreshDateTimeFilterTextBox(CbsTableColumnDefinition column)
        {
            if (!IsDateFilterMode(column.Filter.Mode)
                || !_filterDateTimeStates.TryGetValue(column.FieldKey, out var state))
            {
                return;
            }

            if (IsMaskedDateTimeMode(column))
            {
                state.TextBox.Visibility = Visibility.Visible;
                state.DatePicker.Visibility = Visibility.Collapsed;
                state.ClearButton.Visibility = Visibility.Collapsed;
                state.TextBox.PlaceholderText = GetDateTimePlaceholder(column);
                state.TextBox.BorderBrush = GetFilterBorderBrush(column);
                state.TextBox.Background = GetFilterBackgroundBrush(column);
                state.TextBox.Foreground = GetFilterForegroundBrush(column);
                return;
            }

            state.TextBox.Visibility = Visibility.Collapsed;
            state.DatePicker.Visibility = Visibility.Visible;
            state.DatePicker.PlaceholderText = string.Empty;
            state.DatePicker.BorderBrush = GetFilterBorderBrush(column);
            state.DatePicker.Background = GetFilterBackgroundBrush(column);
            state.DatePicker.Foreground = GetFilterForegroundBrush(column);
            state.ClearButton.Visibility = state.DatePicker.Date.HasValue
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnDateTimeFilterDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            if (sender.Tag is not CbsTableColumnDefinition column)
            {
                return;
            }

            column.Filter.Value = GetFilterValue(column);
            RefreshDateTimeFilterTextBox(column);

            if (_suppressFilterNotifications)
            {
                return;
            }

            FilterRequested?.Invoke(
                this,
                new CbsTableFilterRequestedEventArgs(
                    column.FieldKey,
                    GetFilterMode(column),
                    GetFilterValue(column)));
        }

        private void OnDateTimeFilterClearButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: CbsTableColumnDefinition column }
                || !_filterDateTimeStates.TryGetValue(column.FieldKey, out var state))
            {
                return;
            }

            state.DatePicker.Date = null;
            column.Filter.Value = null;
            RefreshDateTimeFilterTextBox(column);
        }

        private void OnBooleanFilterCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox { Tag: CbsTableColumnDefinition column } checkBox)
            {
                return;
            }

            column.Filter.Value = checkBox.IsChecked;
            checkBox.Foreground = GetFilterForegroundBrush(column);
            checkBox.Background = GetFilterBackgroundBrush(column);

            if (_suppressFilterNotifications)
            {
                return;
            }

            FilterRequested?.Invoke(
                this,
                new CbsTableFilterRequestedEventArgs(
                    column.FieldKey,
                    DataFilterMatchMode.Equals,
                    checkBox.IsChecked));
        }

        private void OnMultiSelectFlyoutOpened(object? sender, object e)
        {
            if (sender is not Flyout { Content: FrameworkElement { Tag: MultiSelectFilterUiState state } })
            {
                return;
            }

            state.SearchText = string.Empty;
            state.SearchTextBox.Text = string.Empty;
            RebuildMultiSelectOptionItems(state);
        }

        private void OnMultiSelectSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox { Tag: MultiSelectFilterUiState state })
            {
                return;
            }

            state.SearchText = state.SearchTextBox.Text ?? string.Empty;
            RebuildMultiSelectOptionItems(state);
        }

        private void OnMultiSelectClearButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: MultiSelectFilterUiState state })
            {
                return;
            }

            state.SearchText = string.Empty;
            state.SearchTextBox.Text = string.Empty;
            state.SelectedValues = Array.Empty<object?>();
            state.SelectedOptions = [];
            state.Column.Filter.Value = state.SelectedValues;
            UpdateMultiSelectFilterButtonContent(state.Button, state.Column);
            RebuildMultiSelectOptionItems(state);

            if (_suppressFilterNotifications)
            {
                return;
            }

            FilterRequested?.Invoke(
                this,
                new CbsTableFilterRequestedEventArgs(
                    state.Column.FieldKey,
                    GetFilterMode(state.Column),
                    CbsTableMultiSelectFilterValue.Create(GetMultiSelectOptions(state.Column), state.SelectedValues)));
        }

        private void OnMultiSelectCloseButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: MultiSelectFilterUiState state }
                && state.Button.Flyout is Flyout flyout)
            {
                flyout.Hide();
            }
        }

        private void OnMultiSelectOptionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox
                {
                    Tag: ValueTuple<MultiSelectFilterUiState, CbsTableFilterOptionDefinition> payload,
                    IsChecked: bool isChecked
                })
            {
                return;
            }

            var (state, option) = payload;
            var selectedValues = state.SelectedValues.ToList();

            if (isChecked)
            {
                if (!selectedValues.Any(value => AreFilterValuesEqual(value, option.Value)))
                {
                    selectedValues.Add(option.Value);
                }
            }
            else
            {
                selectedValues.RemoveAll(value => AreFilterValuesEqual(value, option.Value));
            }

            state.SelectedValues = selectedValues;
            state.SelectedOptions = GetMultiSelectOptions(state.Column)
                .Where(optionItem => state.SelectedValues.Any(value => AreFilterValuesEqual(value, optionItem.Value)))
                .ToList();
            state.Column.Filter.Value = state.SelectedValues;
            UpdateMultiSelectFilterButtonContent(state.Button, state.Column);

            if (_suppressFilterNotifications)
            {
                return;
            }

            FilterRequested?.Invoke(
                this,
                new CbsTableFilterRequestedEventArgs(
                    state.Column.FieldKey,
                    GetFilterMode(state.Column),
                    CbsTableMultiSelectFilterValue.Create(GetMultiSelectOptions(state.Column), state.SelectedValues)));
        }

        private void OnNumericFilterTextBoxBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            if (sender.Tag is not CbsTableColumnDefinition column || column.Filter.Mode != DataFilterMode.Numeric)
            {
                return;
            }

            if (!IsValidNumericFilterInput(args.NewText))
            {
                args.Cancel = true;
            }
        }

        private void EnsureFilterState(CbsTableColumnDefinition column)
        {
            var filterStateKey = GetFilterStateKey(column);

            if (!_filterModes.ContainsKey(filterStateKey))
            {
                _filterModes[filterStateKey] = column.Filter.MatchMode;
            }

            if (column.Filter.EditorKind == CbsTableFilterEditorKind.MultiSelect
                || column.Filter.EditorKind == CbsTableFilterEditorKind.Boolean)
            {
                return;
            }
            else if (!_filterTexts.ContainsKey(filterStateKey))
            {
                _filterTexts[filterStateKey] = FormatFilterValue(column.Filter.Value);
            }
        }

        private static IReadOnlyList<object?> NormalizeFilterSelectedValues(object? value)
        {
            return value switch
            {
                null => [],
                string => [value],
                System.Collections.IEnumerable values => values.Cast<object?>().ToList(),
                _ => [value]
            };
        }

        private static bool? TryGetBooleanFilterValue(object? value)
        {
            return value switch
            {
                bool boolValue => boolValue,
                string text when bool.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static DateTimeOffset? TryGetDateFilterValue(object? value)
        {
            return value switch
            {
                DateTimeOffset dateTimeOffset => dateTimeOffset,
                DateTime dateTime => new DateTimeOffset(dateTime),
                string text when DateTimeOffset.TryParse(
                    text,
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                    out var parsedOffset) => parsedOffset,
                string text when DateTimeOffset.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                    out var parsedOffset) => parsedOffset,
                _ => null
            };
        }

        private static string FormatFilterValue(object? value)
        {
            return value switch
            {
                null => string.Empty,
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("d", CultureInfo.CurrentCulture),
                DateTime dateTime => dateTime.ToString("d", CultureInfo.CurrentCulture),
                _ => value.ToString() ?? string.Empty
            };
        }

        private static bool AreFilterValuesEqual(object? left, object? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            if (TryConvertFilterNumber(left, out var leftNumber)
                && TryConvertFilterNumber(right, out var rightNumber))
            {
                return leftNumber == rightNumber;
            }

            return Equals(left, right);
        }

        private static bool TryConvertFilterNumber(object value, out decimal number)
        {
            switch (value)
            {
                case byte byteValue:
                    number = byteValue;
                    return true;
                case sbyte sbyteValue:
                    number = sbyteValue;
                    return true;
                case short shortValue:
                    number = shortValue;
                    return true;
                case ushort ushortValue:
                    number = ushortValue;
                    return true;
                case int intValue:
                    number = intValue;
                    return true;
                case uint uintValue:
                    number = uintValue;
                    return true;
                case long longValue:
                    number = longValue;
                    return true;
                case ulong ulongValue:
                    number = ulongValue;
                    return true;
                case decimal decimalValue:
                    number = decimalValue;
                    return true;
                case string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed):
                    number = parsed;
                    return true;
                default:
                    number = default;
                    return false;
            }
        }

        private DataFilterMatchMode GetFilterMode(CbsTableColumnDefinition column)
        {
            return _filterModes.TryGetValue(GetFilterStateKey(column), out var mode)
                ? mode
                : column.Filter.MatchMode;
        }

        private string GetFilterText(CbsTableColumnDefinition column)
        {
            return _filterTexts.TryGetValue(GetFilterStateKey(column), out var text)
                ? text
                : string.Empty;
        }

        private object? GetFilterValue(CbsTableColumnDefinition column)
        {
            if (IsDateFilterMode(column.Filter.Mode))
            {
                if (!IsMaskedDateTimeMode(column)
                    && _filterDateTimeStates.TryGetValue(column.FieldKey, out var dateTimeState))
                {
                    return dateTimeState.DatePicker.Date;
                }

                var dateTimeText = _filterTextBoxes.TryGetValue(column.FieldKey, out var textBox)
                    ? NormalizeDateTimeFilterValue(column, textBox.Text)
                    : GetFilterText(column);
                return string.IsNullOrWhiteSpace(dateTimeText) ? null : dateTimeText;
            }

            var text = GetFilterText(column);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private bool HasActiveFilter(CbsTableColumnDefinition column)
        {
            if (!column.IsFilterable)
            {
                return false;
            }

            if (column.Filter.EditorKind == CbsTableFilterEditorKind.MultiSelect)
            {
                return column.Filter.Value is CbsTableMultiSelectFilterValue multiSelectValue
                    ? multiSelectValue.SelectedValues.Count > 0
                    : NormalizeFilterSelectedValues(column.Filter.Value).Count > 0;
            }

            if (column.Filter.EditorKind == CbsTableFilterEditorKind.Boolean)
            {
                return TryGetBooleanFilterValue(column.Filter.Value).HasValue;
            }

            if (_filterTexts.TryGetValue(GetFilterStateKey(column), out var filterText))
            {
                return !string.IsNullOrWhiteSpace(filterText);
            }

            return column.Filter.Value switch
            {
                null => false,
                string text => !string.IsNullOrWhiteSpace(text),
                _ => true
            };
        }

        private Brush GetFilterBorderBrush(CbsTableColumnDefinition column)
        {
            return (Brush)Application.Current.Resources[
                HasActiveFilter(column)
                    ? "ShellTableFilterActiveBorderBrush"
                    : "ShellTableGridLineBrush"];
        }

        private Brush GetFilterBackgroundBrush(CbsTableColumnDefinition column)
        {
            return (Brush)Application.Current.Resources[
                HasActiveFilter(column)
                    ? "ShellTableFilterActiveBackgroundBrush"
                    : "ShellTableHeaderBackgroundBrush"];
        }

        private Brush GetFilterForegroundBrush(CbsTableColumnDefinition column)
        {
            return (Brush)Application.Current.Resources[
                HasActiveFilter(column)
                    ? "ShellTableFilterActiveTextBrush"
                    : "ShellPrimaryTextBrush"];
        }

        private string GetFilterStateKey(CbsTableColumnDefinition column)
        {
            return $"{TableStateKey}|{column.FieldKey}";
        }

        private IReadOnlyList<CbsTableFilterOptionDefinition> GetMultiSelectOptions(CbsTableColumnDefinition column)
        {
            if (!string.IsNullOrWhiteSpace(column.Filter.OptionsSourceKey)
                && MultiSelectOptionsSources.TryGetValue(column.Filter.OptionsSourceKey, out var options))
            {
                return options;
            }

            return column.Filter.StaticOptions;
        }

        private void RefreshMultiSelectFilterStates()
        {
            foreach (var state in _filterMultiSelectStates.Values)
            {
                var allOptions = GetMultiSelectOptions(state.Column);
                state.AvailableOptions = allOptions.ToList();
                state.SelectedValues = state.SelectedValues
                    .Where(value => allOptions.Any(option => AreFilterValuesEqual(option.Value, value)))
                    .ToList();
                state.SelectedOptions = allOptions
                    .Where(option => state.SelectedValues.Any(value => AreFilterValuesEqual(value, option.Value)))
                    .ToList();
                UpdateMultiSelectFilterButtonContent(state.Button, state.Column);
                RebuildMultiSelectOptionItems(state);
            }
        }

        private void RebuildMultiSelectOptionItems(MultiSelectFilterUiState state)
        {
            state.OptionsHost.Children.Clear();

            state.SearchText = state.SearchTextBox.Text ?? string.Empty;
            var searchText = state.SearchText.Trim();
            var allOptions = GetMultiSelectOptions(state.Column);
            state.AvailableOptions = allOptions
                .Where(option => string.IsNullOrWhiteSpace(searchText)
                    || option.Label.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (state.AvailableOptions.Count == 0)
            {
                state.SelectedOptions = allOptions
                    .Where(option => state.SelectedValues.Any(value => Equals(value, option.Value)))
                    .ToList();
                state.OptionsHost.Children.Add(new TextBlock
                {
                    Text = allOptions.Count == 0 ? "Нет доступных опций" : "Ничего не найдено",
                    Margin = new Thickness(4, 2, 4, 2),
                    Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"],
                    FontSize = Math.Max(11, GetHeaderFontSize() - 1),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            state.SelectedOptions = allOptions
                .Where(option => state.SelectedValues.Any(value => AreFilterValuesEqual(value, option.Value)))
                .ToList();

            foreach (var option in state.AvailableOptions)
            {
                var checkBox = new CheckBox
                {
                    Tag = (state, option),
                    IsChecked = state.SelectedValues.Any(value => AreFilterValuesEqual(value, option.Value)),
                    MinHeight = 20,
                    Padding = new Thickness(0),
                    Margin = new Thickness(4, 1, 4, 1)
                };
                checkBox.Content = new TextBlock
                {
                    Text = option.Label,
                    Margin = new Thickness(2, 0, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                checkBox.Checked += OnMultiSelectOptionChanged;
                checkBox.Unchecked += OnMultiSelectOptionChanged;
                state.OptionsHost.Children.Add(checkBox);
            }
        }

        private void UpdateMultiSelectFilterButtonContent(Button button, CbsTableColumnDefinition column)
        {
            if (!_filterMultiSelectStates.TryGetValue(column.FieldKey, out var state))
            {
                return;
            }

            button.BorderBrush = GetFilterBorderBrush(column);
            button.Background = GetFilterBackgroundBrush(column);
            button.Foreground = GetFilterForegroundBrush(column);

            var selectedCount = state.SelectedOptions.Count;
            var text = selectedCount == 0
                ? column.Filter.EmptySelectionText
                : $"Выбрано: {selectedCount}";

            var contentGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textBlock = new TextBlock
            {
                Text = text,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = Math.Max(11, GetHeaderFontSize() - 1),
                Foreground = GetFilterForegroundBrush(column),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(textBlock, 0);
            contentGrid.Children.Add(textBlock);

            var icon = new FontIcon
            {
                Glyph = "\uE70D",
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 10,
                Margin = new Thickness(6, 0, 0, 0),
                Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"],
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 1);
            contentGrid.Children.Add(icon);

            button.Content = contentGrid;
        }

        private static bool SupportsFilterModeButton(CbsTableColumnDefinition column)
        {
            return column.IsFilterable
                && column.Filter.EditorKind != CbsTableFilterEditorKind.MultiSelect
                && column.Filter.EditorKind != CbsTableFilterEditorKind.Boolean;
        }

        private void UpdateFilterModeButtonContent(Button button, DataFilterMatchMode mode)
        {
            button.Content = new TextBlock
            {
                Text = GetFilterModeShortLabel(mode),
                FontSize = 10,
                Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
        }

        private static IReadOnlyList<DataFilterMatchMode> GetSupportedFilterModes(CbsTableColumnDefinition column)
        {
            if (column.Filter.Mode == DataFilterMode.Numeric)
            {
                return
                [
                    DataFilterMatchMode.Equals,
                    DataFilterMatchMode.LessThan,
                    DataFilterMatchMode.LessThanOrEqual,
                    DataFilterMatchMode.GreaterThan,
                    DataFilterMatchMode.GreaterThanOrEqual,
                    DataFilterMatchMode.Contains,
                    DataFilterMatchMode.StartsWith,
                    DataFilterMatchMode.EndsWith,
                    DataFilterMatchMode.NotContains
                ];
            }

            if (IsDateFilterMode(column.Filter.Mode))
            {
                return
                [
                    DataFilterMatchMode.GreaterThanOrEqual,
                    DataFilterMatchMode.LessThanOrEqual,
                    DataFilterMatchMode.GreaterThan,
                    DataFilterMatchMode.LessThan,
                    DataFilterMatchMode.Equals,
                    DataFilterMatchMode.Contains,
                    DataFilterMatchMode.StartsWith,
                    DataFilterMatchMode.EndsWith,
                    DataFilterMatchMode.NotContains
                ];
            }

            return
            [
                DataFilterMatchMode.Contains,
                DataFilterMatchMode.StartsWith,
                DataFilterMatchMode.Equals,
                DataFilterMatchMode.EndsWith,
                DataFilterMatchMode.NotContains
            ];
        }

        private static string GetFilterModeShortLabel(DataFilterMatchMode mode)
        {
            return mode switch
            {
                DataFilterMatchMode.LessThan => "<",
                DataFilterMatchMode.LessThanOrEqual => "≤",
                DataFilterMatchMode.GreaterThan => ">",
                DataFilterMatchMode.GreaterThanOrEqual => "≥",
                DataFilterMatchMode.StartsWith => "A*",
                DataFilterMatchMode.Equals => "=",
                DataFilterMatchMode.EndsWith => "*A",
                DataFilterMatchMode.NotContains => "!=",
                _ => "*A*"
            };
        }

        private static string GetFilterModeLabel(CbsTableColumnDefinition column, DataFilterMatchMode mode)
        {
            if (IsDateFilterMode(column.Filter.Mode))
            {
                return mode switch
                {
                    DataFilterMatchMode.LessThan => "Ранее чем",
                    DataFilterMatchMode.LessThanOrEqual => "Не позже чем",
                    DataFilterMatchMode.GreaterThan => "Позже чем",
                    DataFilterMatchMode.GreaterThanOrEqual => "Не ранее чем",
                    DataFilterMatchMode.Equals => "Точно в",
                    DataFilterMatchMode.StartsWith => "Начинается с",
                    DataFilterMatchMode.EndsWith => "Заканчивается на",
                    DataFilterMatchMode.NotContains => "Не содержит",
                    _ => "Содержит"
                };
            }

            return mode switch
            {
                DataFilterMatchMode.LessThan => "Меньше",
                DataFilterMatchMode.LessThanOrEqual => "Меньше или равно",
                DataFilterMatchMode.GreaterThan => "Больше",
                DataFilterMatchMode.GreaterThanOrEqual => "Больше или равно",
                DataFilterMatchMode.StartsWith => "Начинается с",
                DataFilterMatchMode.Equals => "Равно",
                DataFilterMatchMode.EndsWith => "Заканчивается на",
                DataFilterMatchMode.NotContains => "Не содержит",
                _ => "Содержит"
            };
        }

        private static bool IsValidNumericFilterInput(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            var separatorCount = 0;

            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];

                if (char.IsDigit(character))
                {
                    continue;
                }

                if (character is '.' or ',')
                {
                    separatorCount++;
                    if (separatorCount > 1)
                    {
                        return false;
                    }

                    continue;
                }

                if (character == '-' && index == 0)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private string GetDateTimePlaceholder(CbsTableColumnDefinition column)
        {
            return IsMaskedDateTimeMode(column)
                ? (column.Filter.Mode == DataFilterMode.Date
                    ? BuildIsoDatePlaceholderPattern()
                    : BuildIsoDateTimePlaceholderPattern())
                : column.Filter.PlaceholderText;
        }

        private bool IsMaskedDateTimeMode(CbsTableColumnDefinition column)
        {
            if (!IsDateFilterMode(column.Filter.Mode))
            {
                return false;
            }

            return GetFilterMode(column) is DataFilterMatchMode.Contains
                or DataFilterMatchMode.StartsWith
                or DataFilterMatchMode.EndsWith
                or DataFilterMatchMode.NotContains;
        }

        private string NormalizeDateTimeFilterValue(CbsTableColumnDefinition column, string? maskedText)
        {
            if (string.IsNullOrWhiteSpace(maskedText))
            {
                return string.Empty;
            }

            var text = maskedText.Trim();
            if (!text.Any(char.IsDigit))
            {
                return string.Empty;
            }

            return GetFilterMode(column) switch
            {
                DataFilterMatchMode.Contains or DataFilterMatchMode.StartsWith or DataFilterMatchMode.EndsWith or DataFilterMatchMode.NotContains
                    => NormalizeIsoDateTimeTextFragment(column.Filter.Mode, text),
                _ => TryExtractCompleteDateTimeValue(column.Filter.Mode, text, out var completeValue)
                    ? completeValue
                    : string.Empty
            };
        }

        private static bool TryExtractCompleteDateTimeValue(DataFilterMode mode, string maskedText, out string value)
        {
            value = string.Empty;
            return DateTime.TryParse(
                    maskedText,
                    System.Globalization.CultureInfo.CurrentCulture,
                    System.Globalization.DateTimeStyles.AllowWhiteSpaces | System.Globalization.DateTimeStyles.AssumeLocal,
                    out var dateTime)
                && (value = dateTime.ToString(
                    mode == DataFilterMode.Date ? "yyyy-MM-dd" : "yyyy-MM-dd'T'HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture)) is not null;
        }

        private static string NormalizeIsoDateTimeTextFragment(DataFilterMode mode, string text)
        {
            var allowed = text.Where(static character =>
                char.IsDigit(character)
                || character is '-' or ':' or ' ' or 'T').ToArray();

            if (mode == DataFilterMode.Date)
            {
                allowed = allowed.Where(static character => char.IsDigit(character) || character == '-').ToArray();
            }

            return new string(allowed);
        }

        private static string BuildIsoDatePlaceholderPattern()
        {
            return "ГГГГ-ММ-ДД";
        }

        private static bool IsDateFilterMode(DataFilterMode mode)
        {
            return mode is DataFilterMode.Date or DataFilterMode.DateTime;
        }

        private static string BuildIsoDateTimePlaceholderPattern()
        {
            return "ГГГГ-ММ-ДД ЧЧ:ММ:СС";
        }

    }

    internal sealed class MultiSelectFilterUiState
    {
        public required CbsTableColumnDefinition Column { get; init; }

        public required Button Button { get; init; }

        public required TextBox SearchTextBox { get; init; }

        public required StackPanel OptionsHost { get; init; }

        public required IReadOnlyList<CbsTableFilterOptionDefinition> AvailableOptions { get; set; }

        public required IReadOnlyList<CbsTableFilterOptionDefinition> SelectedOptions { get; set; }

        public required IReadOnlyList<object?> SelectedValues { get; set; }

        public required string SearchText { get; set; }
    }

    internal sealed class DateTimeFilterUiState
    {
        public required CbsTableColumnDefinition Column { get; init; }

        public required TextBox TextBox { get; init; }

        public required CalendarDatePicker DatePicker { get; init; }

        public required Button ClearButton { get; init; }
    }

    public sealed class CbsTableLoadMoreRequestedEventArgs : EventArgs;

    public sealed class CbsTableSortRequestedEventArgs : EventArgs
    {
        public CbsTableSortRequestedEventArgs(string fieldKey, DataSortDirection? direction)
        {
            FieldKey = fieldKey;
            Direction = direction;
        }

        public string FieldKey { get; }

        public DataSortDirection? Direction { get; }
    }

    public sealed class CbsTableTraceEventArgs : EventArgs
    {
        public CbsTableTraceEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; }
    }

    public sealed class CbsTableViewportChangedEventArgs : EventArgs
    {
        public CbsTableViewportChangedEventArgs(int startIndex, int endIndex, int retainedBufferRows)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
            RetainedBufferRows = retainedBufferRows;
        }

        public int StartIndex { get; }

        public int EndIndex { get; }

        public int RetainedBufferRows { get; }
    }

    public sealed class CbsTableColumnWidthChangedEventArgs : EventArgs
    {
        public CbsTableColumnWidthChangedEventArgs(string fieldKey, string? width, double widthPixels)
        {
            FieldKey = fieldKey;
            Width = width;
            WidthPixels = widthPixels;
        }

        public string FieldKey { get; }

        public string? Width { get; }

        public double WidthPixels { get; }
    }

    public sealed class CbsTableFilterRequestedEventArgs : EventArgs
    {
        public CbsTableFilterRequestedEventArgs(string fieldKey, DataFilterMatchMode matchMode, object? value)
        {
            FieldKey = fieldKey;
            MatchMode = matchMode;
            Value = value;
        }

        public string FieldKey { get; }

        public DataFilterMatchMode MatchMode { get; }

        public object? Value { get; }
    }

    public sealed class CbsTableRowDoubleTappedEventArgs : EventArgs
    {
        public CbsTableRowDoubleTappedEventArgs(TableDataRow row, int rowIndex)
        {
            Row = row;
            RowIndex = rowIndex;
        }

        public TableDataRow Row { get; }

        public int RowIndex { get; }
    }

    public sealed class CbsTableRowSelectionChangedEventArgs : EventArgs
    {
        public CbsTableRowSelectionChangedEventArgs(TableDataRow? row, int rowIndex, bool isSelected)
        {
            Row = row;
            RowIndex = rowIndex;
            IsSelected = isSelected;
        }

        public TableDataRow? Row { get; }

        public int RowIndex { get; }

        public bool IsSelected { get; }
    }

    internal readonly record struct CbsTableCellPosition(int RowIndex, int ColumnIndex);
}
