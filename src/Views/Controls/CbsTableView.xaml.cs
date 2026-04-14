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
using Microsoft.UI.Xaml.Media;

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
                new PropertyMetadata(40d, OnRowHeightChanged));

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
        private const int WindowBufferRows = 8;
        private const int WindowStepRows = 8;

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

        public int RetainedBufferRows
        {
            get => (int)GetValue(RetainedBufferRowsProperty);
            set => SetValue(RetainedBufferRowsProperty, value);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            RebuildHeader();
            RebuildRows();
            RowsScrollViewer.ViewChanged += OnScrollViewerViewChanged;
            await Task.Yield();
            AppendTrace("Attached explicit table ScrollViewer.");
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            RowsScrollViewer.ViewChanged -= OnScrollViewerViewChanged;
        }

        private async void OnScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
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
                return;
            }

            for (var index = 0; index < Columns.Count; index++)
            {
                HeaderGrid.ColumnDefinitions.Add(CreateColumnDefinition(Columns[index]));

                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(8, 4, 8, 4),
                    BorderThickness = new Thickness(0),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    Tag = Columns[index]
                };
                button.Click += OnHeaderButtonClick;

                var headerText = Columns[index].Header;
                if (Columns[index].IsSortable && string.Equals(CurrentSortField, Columns[index].FieldKey, StringComparison.OrdinalIgnoreCase))
                {
                    headerText = CurrentSortDirection switch
                    {
                        DataSortDirection.Descending => $"{headerText} v",
                        _ => $"{headerText} ^"
                    };
                }

                button.Content = new TextBlock
                {
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["ShellPrimaryTextBrush"],
                    Text = headerText,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };

                Grid.SetColumn(button, index);
                HeaderGrid.Children.Add(button);

                if (index < Columns.Count - 1)
                {
                    var splitter = new Border
                    {
                        Width = 1,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Background = (Brush)Application.Current.Resources["ShellPanelBorderBrush"]
                    };
                    Grid.SetColumn(splitter, index);
                    HeaderGrid.Children.Add(splitter);
                }
            }
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
                _rowPool[index].Configure(
                    sourceRows[window.Start + index],
                    Columns,
                    RowHeight);
            }

            stopwatch.Stop();
            AppendTrace($"TABLE REBUILD END seq={sequence} rows={rowCount} elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms");
            TrackLayoutCompletion(sequence, rowCount);
        }

        private void OnHeaderButtonClick(object sender, RoutedEventArgs e)
        {
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
            control.RebuildRows();
        }

        private static ColumnDefinition CreateColumnDefinition(CbsTableColumnDefinition column)
        {
            if (double.TryParse(column.Width, out var fixedWidth))
            {
                return new ColumnDefinition { Width = new GridLength(fixedWidth) };
            }

            if (string.Equals(column.Width, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return new ColumnDefinition { Width = GridLength.Auto };
            }

            if (string.Equals(column.FieldKey, "id", StringComparison.OrdinalIgnoreCase))
            {
                return new ColumnDefinition { Width = new GridLength(88) };
            }

            return new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
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
                _rowPool.Add(rowView);
                RowsHost.Children.Add(rowView);
            }
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
}
