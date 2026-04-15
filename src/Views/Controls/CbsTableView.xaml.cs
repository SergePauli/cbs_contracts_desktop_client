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
        private const int WindowBufferRows = 8;
        private const int WindowStepRows = 8;
        private const double HeaderSideBorderCompensation = 1d;
        private const double MinimumColumnWidth = 48d;
        private const double ResizeHandleWidth = 12d;
        private const double HeaderAdornmentWidth = 18d;
        private int _activeResizeColumnIndex = -1;
        private double _activeResizeStartWidth;
        private bool _suppressNextHeaderClick;

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

            if (Columns.Count == 0)
            {
                UpdateHeaderViewportCompensation();
                return;
            }

            for (var index = 0; index < Columns.Count; index++)
            {
                HeaderGrid.ColumnDefinitions.Add(CreateDataColumnDefinition(Columns[index]));

                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = GetHeaderPadding(),
                    BorderThickness = new Thickness(0),
                    Background = (Brush)Application.Current.Resources["ShellTableHeaderBackgroundBrush"],
                    Tag = Columns[index]
                };
                button.Click += OnHeaderButtonClick;

                button.Content = CreateHeaderContent(Columns[index]);

                Grid.SetColumn(button, index);
                HeaderGrid.Children.Add(button);

                var splitter = CreateResizeHandle(index, index == Columns.Count - 1);
                Grid.SetColumn(splitter, index);
                HeaderGrid.Children.Add(splitter);
            }

            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var fillerHeader = new Border
            {
                Background = (Brush)Application.Current.Resources["ShellTableHeaderBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ShellTableGridLineBrush"],
                BorderThickness = new Thickness(1, 0, 0, 0)
            };
            Grid.SetColumn(fillerHeader, Columns.Count);
            HeaderGrid.Children.Add(fillerHeader);

            HeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
            var scrollbarCompensationCell = new Border
            {
                Background = (Brush)Application.Current.Resources["ShellTableHeaderBackgroundBrush"]
            };
            Grid.SetColumn(scrollbarCompensationCell, Columns.Count + 1);
            HeaderGrid.Children.Add(scrollbarCompensationCell);

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

        private Thickness GetHeaderPadding()
        {
            return Density switch
            {
                CbsTableDensity.Comfortable => new Thickness(10, 6, 10, 6),
                CbsTableDensity.Standard => new Thickness(8, 5, 8, 5),
                _ => new Thickness(6, 4, 6, 4)
            };
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

        private FrameworkElement CreateHeaderContent(CbsTableColumnDefinition column)
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
}
