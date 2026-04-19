using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
                new PropertyMetadata(false));

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

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(ReferenceDataRow),
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
        private IEnumerable? _lastItemsSourceReference;
        private readonly List<CbsTableRowView> _rowPool = [];
        private readonly HashSet<int> _selectedIndexes = [];
        private readonly Dictionary<string, string> _filterTexts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DataFilterMatchMode> _filterModes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TextBox> _filterTextBoxes = new(StringComparer.OrdinalIgnoreCase);
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
        private const double MultiSelectFilterButtonHeight = 22d;
        private const double MultiSelectFilterFlyoutWidth = 260d;
        private const double MultiSelectFilterFlyoutMaxHeight = 180d;
        private int _activeResizeColumnIndex = -1;
        private double _activeResizeStartWidth;
        private bool _suppressNextHeaderClick;
        private bool _suppressFilterNotifications;

        public CbsTableView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public event EventHandler<CbsTableLoadMoreRequestedEventArgs>? LoadMoreRequested;

        public event EventHandler<CbsTableSortRequestedEventArgs>? SortRequested;

        public event EventHandler<CbsTableTraceEventArgs>? TraceGenerated;

        public event EventHandler<CbsTableViewportChangedEventArgs>? ViewportChanged;

        public event EventHandler<CbsTableColumnWidthChangedEventArgs>? ColumnWidthChanged;

        public event EventHandler<CbsTableFilterRequestedEventArgs>? FilterRequested;

        public void ClearFilterInputs()
        {
            _suppressFilterNotifications = true;

            try
            {
                foreach (var column in Columns.Where(static column => column.IsFilterable))
                {
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
                    else
                    {
                        _filterTexts[GetFilterStateKey(column)] = string.Empty;
                    }
                }

                foreach (var textBox in _filterTextBoxes.Values)
                {
                    if (!string.IsNullOrEmpty(textBox.Text))
                    {
                        textBox.Text = string.Empty;
                    }
                }
            }
            finally
            {
                _suppressFilterNotifications = false;
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

        public ReferenceDataRow? SelectedItem
        {
            get => (ReferenceDataRow?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
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

        private async void OnScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            UpdateHeaderViewportCompensation();
            RebuildRows();

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

            if (window.Start == _lastWindowStart
                && window.End == _lastWindowEnd
                && totalRows == _lastSourceCount
                && ReferenceEquals(ItemsSource, _lastItemsSourceReference))
            {
                UpdateSpacerHeights(totalRows, window.Start, window.End);
                return;
            }

            _lastWindowStart = window.Start;
            _lastWindowEnd = window.End;
            _lastSourceCount = totalRows;
            _lastItemsSourceReference = ItemsSource;
            var retainedBufferRows = GetEffectiveRetainedBufferRows(window.End - window.Start);
            AppendTrace(
                $"VIEWPORT CHANGED start={window.Start} end={window.End} buffer={retainedBufferRows} total={totalRows}");
            ViewportChanged?.Invoke(this, new CbsTableViewportChangedEventArgs(
                window.Start,
                window.End,
                retainedBufferRows));

            var sequence = ++_rebuildSequence;
            var stopwatch = Stopwatch.StartNew();
            var rowCount = Math.Max(0, window.End - window.Start);
            AppendTrace($"TABLE REBUILD START seq={sequence} window={window.Start}..{window.End} total={totalRows}");

            UpdateSpacerHeights(totalRows, window.Start, window.End);
            EnsureRowPool(rowCount);

            for (var index = 0; index < rowCount; index++)
            {
                var absoluteIndex = window.Start + index;
                _rowPool[index].Configure(
                    sourceRows[absoluteIndex],
                    Columns,
                    RowHeight);
                _rowPool[index].Tag = absoluteIndex;
                ApplyRowSelectionState(_rowPool[index], absoluteIndex);
            }

            stopwatch.Stop();
            AppendTrace($"TABLE REBUILD END seq={sequence} rows={rowCount} elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms");
            TrackLayoutCompletion(sequence, rowCount);
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
            control.InvalidateWindowCache();
            control.RebuildHeader();
            control.RebuildRows();
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (CbsTableView)d;
            control.InvalidateWindowCache();
            control.RebuildRows();
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
            _lastItemsSourceReference = null;
        }

        private IReadOnlyList<ReferenceDataRow> GetSourceRows()
        {
            if (ItemsSource is IReadOnlyList<ReferenceDataRow> readOnlyList)
            {
                return readOnlyList;
            }

            if (ItemsSource is IList<ReferenceDataRow> list)
            {
                return list.ToList();
            }

            return ItemsSource?.OfType<ReferenceDataRow>().ToList() ?? [];
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
            var rawStart = Math.Max(0, firstVisibleIndex - WindowBufferRows);
            var alignedStart = (rawStart / WindowStepRows) * WindowStepRows;
            var start = Math.Max(0, alignedStart);
            var end = Math.Min(totalRows, start + visibleRowCount + (WindowBufferRows * 2));
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
                rowView.PointerReleased += OnRowPointerReleased;
                rowView.Tapped += OnRowTapped;
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
            if (!SupportsRowSelection || sender is not CbsTableRowView rowView)
            {
                return;
            }

            rowView.IsPressed = true;
        }

        private void OnRowPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not CbsTableRowView rowView)
            {
                return;
            }

            rowView.IsPressed = false;
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

            if (SupportsMultipleRowSelection)
            {
                if (!_selectedIndexes.Add(rowIndex))
                {
                    _selectedIndexes.Remove(rowIndex);
                }
            }
            else
            {
                _selectedIndexes.Clear();
                _selectedIndexes.Add(rowIndex);
                SelectedItem = rowView.Row;
            }

            UpdateVisibleRowSelectionStates();
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
                CbsTableDensity.Comfortable => new Thickness(10, 6, 10, 6),
                CbsTableDensity.Standard => new Thickness(8, 5, 8, 5),
                _ => new Thickness(6, 4, 6, 4)
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

            var textBox = CreateTextFilterTextBox(column);
            border.Child = textBox;
            _filterTextBoxes[column.FieldKey] = textBox;
            return border;
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
                PlaceholderText = column.Filter.PlaceholderText,
                FontSize = Math.Max(11, GetHeaderFontSize() - 1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(1),
                MinWidth = 24
            };
            if (column.Filter.Mode == DataFilterMode.Numeric)
            {
                textBox.BeforeTextChanging += OnNumericFilterTextBoxBeforeTextChanging;
            }

            textBox.TextChanged += OnFilterTextChanged;
            return textBox;
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
                MinWidth = 24
            };

            var searchTextBox = new TextBox
            {
                PlaceholderText = "Поиск",
                Margin = new Thickness(0, 0, 0, 8)
            };

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

            var flyoutContent = new StackPanel
            {
                Width = MultiSelectFilterFlyoutWidth,
                Spacing = 0,
                Children =
                {
                    searchTextBox,
                    scrollViewer
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
                SelectedOptions = [],
                SelectedValues = Array.Empty<object?>(),
                SearchText = string.Empty
            };

            searchTextBox.Tag = state;
            searchTextBox.TextChanged += OnMultiSelectSearchTextChanged;
            flyout.Opened += OnMultiSelectFlyoutOpened;

            button.Flyout = flyout;
            _filterMultiSelectButtons[column.FieldKey] = button;
            _filterMultiSelectStates[column.FieldKey] = state;
            UpdateMultiSelectFilterButtonContent(button, column);
            RebuildMultiSelectOptionItems(state);
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
                Margin = new Thickness(0, 2, 2, 2),
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
                    Text = GetFilterModeLabel(mode),
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

            FilterRequested?.Invoke(
                this,
                new CbsTableFilterRequestedEventArgs(
                    column.FieldKey,
                    mode,
                    GetFilterText(column)));
        }

        private void OnFilterTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox { Tag: CbsTableColumnDefinition column } textBox)
            {
                return;
            }

            _filterTexts[GetFilterStateKey(column)] = textBox.Text;

            if (_suppressFilterNotifications)
            {
                return;
            }

            FilterRequested?.Invoke(
                this,
                new CbsTableFilterRequestedEventArgs(
                    column.FieldKey,
                    GetFilterMode(column),
                    textBox.Text));
        }

        private void OnMultiSelectFlyoutOpened(object? sender, object e)
        {
            if (sender is not Flyout { Content: StackPanel { Children.Count: > 0 } content }
                || content.Children[0] is not TextBox searchTextBox
                || searchTextBox.Tag is not MultiSelectFilterUiState state)
            {
                return;
            }

            state.SearchText = string.Empty;
            searchTextBox.Text = string.Empty;
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
                if (!selectedValues.Any(value => Equals(value, option.Value)))
                {
                    selectedValues.Add(option.Value);
                }
            }
            else
            {
                selectedValues.RemoveAll(value => Equals(value, option.Value));
            }

            state.SelectedValues = selectedValues;
            state.SelectedOptions = GetMultiSelectOptions(state.Column)
                .Where(optionItem => state.SelectedValues.Any(value => Equals(value, optionItem.Value)))
                .ToList();
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

            if (column.Filter.EditorKind == CbsTableFilterEditorKind.MultiSelect)
            {
                return;
            }
            else if (!_filterTexts.ContainsKey(filterStateKey))
            {
                _filterTexts[filterStateKey] = string.Empty;
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
                    .Where(value => allOptions.Any(option => Equals(option.Value, value)))
                    .ToList();
                state.SelectedOptions = allOptions
                    .Where(option => state.SelectedValues.Any(value => Equals(value, option.Value)))
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
                .Where(option => state.SelectedValues.Any(value => Equals(value, option.Value)))
                .ToList();

            foreach (var option in state.AvailableOptions)
            {
                var checkBox = new CheckBox
                {
                    Tag = (state, option),
                    IsChecked = state.SelectedValues.Any(value => Equals(value, option.Value)),
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
            return column.IsFilterable && column.Filter.EditorKind != CbsTableFilterEditorKind.MultiSelect;
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

        private static string GetFilterModeLabel(DataFilterMatchMode mode)
        {
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
}
