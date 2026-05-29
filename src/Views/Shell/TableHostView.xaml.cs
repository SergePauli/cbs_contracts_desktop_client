// Wraps CbsTableView as the shell-level table host boundary.
using System.Collections;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Stores.Table;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class TableHostView : UserControl
    {
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(
                nameof(Columns),
                typeof(IReadOnlyList<CbsTableColumnDefinition>),
                typeof(TableHostView),
                new PropertyMetadata(Array.Empty<CbsTableColumnDefinition>(), OnColumnsChanged));

        public static readonly DependencyProperty StoreProperty =
            DependencyProperty.Register(
                nameof(Store),
                typeof(TablePageStore),
                typeof(TableHostView),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(
                nameof(ItemsSource),
                typeof(IEnumerable),
                typeof(TableHostView),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty CurrentSortFieldProperty =
            DependencyProperty.Register(
                nameof(CurrentSortField),
                typeof(string),
                typeof(TableHostView),
                new PropertyMetadata(null, OnCurrentSortFieldChanged));

        public static readonly DependencyProperty CurrentSortDirectionProperty =
            DependencyProperty.Register(
                nameof(CurrentSortDirection),
                typeof(DataSortDirection?),
                typeof(TableHostView),
                new PropertyMetadata(null, OnCurrentSortDirectionChanged));

        public static readonly DependencyProperty TableStateKeyProperty =
            DependencyProperty.Register(
                nameof(TableStateKey),
                typeof(string),
                typeof(TableHostView),
                new PropertyMetadata(string.Empty, OnTableStateKeyChanged));

        public static readonly DependencyProperty MultiSelectOptionsSourcesProperty =
            DependencyProperty.Register(
                nameof(MultiSelectOptionsSources),
                typeof(IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>),
                typeof(TableHostView),
                new PropertyMetadata(
                    new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase),
                    OnMultiSelectOptionsSourcesChanged));

        public static readonly DependencyProperty HasMoreItemsProperty =
            DependencyProperty.Register(
                nameof(HasMoreItems),
                typeof(bool),
                typeof(TableHostView),
                new PropertyMetadata(false, OnHasMoreItemsChanged));

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(
                nameof(IsLoading),
                typeof(bool),
                typeof(TableHostView),
                new PropertyMetadata(false, OnIsLoadingChanged));

        public static readonly DependencyProperty LoadedCountProperty =
            DependencyProperty.Register(
                nameof(LoadedCount),
                typeof(int),
                typeof(TableHostView),
                new PropertyMetadata(0, OnLoadedCountChanged));

        public static readonly DependencyProperty TotalCountProperty =
            DependencyProperty.Register(
                nameof(TotalCount),
                typeof(int),
                typeof(TableHostView),
                new PropertyMetadata(0, OnTotalCountChanged));

        public static readonly DependencyProperty RowHeightProperty =
            DependencyProperty.Register(
                nameof(RowHeight),
                typeof(double),
                typeof(TableHostView),
                new PropertyMetadata(22d, OnRowHeightChanged));

        public static readonly DependencyProperty DensityProperty =
            DependencyProperty.Register(
                nameof(Density),
                typeof(CbsTableDensity),
                typeof(TableHostView),
                new PropertyMetadata(CbsTableDensity.Compact, OnDensityChanged));

        public static readonly DependencyProperty RowStyleKeyProperty =
            DependencyProperty.Register(
                nameof(RowStyleKey),
                typeof(CbsTableRowStyleKey),
                typeof(TableHostView),
                new PropertyMetadata(CbsTableRowStyleKey.None, OnRowStyleKeyChanged));

        public static readonly DependencyProperty ShowStageCostFractionProperty =
            DependencyProperty.Register(
                nameof(ShowStageCostFraction),
                typeof(bool),
                typeof(TableHostView),
                new PropertyMetadata(false, OnShowStageCostFractionChanged));

        public static readonly DependencyProperty SupportsRowSelectionProperty =
            DependencyProperty.Register(
                nameof(SupportsRowSelection),
                typeof(bool),
                typeof(TableHostView),
                new PropertyMetadata(false, OnSupportsRowSelectionChanged));

        public static readonly DependencyProperty SupportsMultipleRowSelectionProperty =
            DependencyProperty.Register(
                nameof(SupportsMultipleRowSelection),
                typeof(bool),
                typeof(TableHostView),
                new PropertyMetadata(false, OnSupportsMultipleRowSelectionChanged));

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(
                nameof(SelectedItem),
                typeof(TableDataRow),
                typeof(TableHostView),
                new PropertyMetadata(null, OnSelectedItemChanged));

        public static readonly DependencyProperty RetainedBufferRowsProperty =
            DependencyProperty.Register(
                nameof(RetainedBufferRows),
                typeof(int),
                typeof(TableHostView),
                new PropertyMetadata(0, OnRetainedBufferRowsChanged));

        public TableHostView()
        {
            InitializeComponent();

            TableView.LoadMoreRequested += (_, args) => LoadMoreRequested?.Invoke(this, args);
            TableView.SortRequested += (_, args) => SortRequested?.Invoke(this, args);
            TableView.TraceGenerated += (_, args) => TraceGenerated?.Invoke(this, args);
            TableView.ViewportChanged += (_, args) => ViewportChanged?.Invoke(this, args);
            TableView.ColumnWidthChanged += (_, args) => ColumnWidthChanged?.Invoke(this, args);
            TableView.FilterRequested += (_, args) => FilterRequested?.Invoke(this, args);
            TableView.RowSelectionChanged += (_, args) =>
            {
                SelectedItem = args.Row;
                RowSelectionChanged?.Invoke(this, args);
            };
            TableView.RowDoubleTapped += (_, args) => RowDoubleTapped?.Invoke(this, args);
        }

        public event EventHandler<CbsTableLoadMoreRequestedEventArgs>? LoadMoreRequested;

        public event EventHandler<CbsTableSortRequestedEventArgs>? SortRequested;

        public event EventHandler<CbsTableTraceEventArgs>? TraceGenerated;

        public event EventHandler<CbsTableViewportChangedEventArgs>? ViewportChanged;

        public event EventHandler<CbsTableColumnWidthChangedEventArgs>? ColumnWidthChanged;

        public event EventHandler<CbsTableFilterRequestedEventArgs>? FilterRequested;

        public event EventHandler<CbsTableRowSelectionChangedEventArgs>? RowSelectionChanged;

        public event EventHandler<CbsTableRowDoubleTappedEventArgs>? RowDoubleTapped;

        public TablePageStore? Store
        {
            get => (TablePageStore?)GetValue(StoreProperty);
            set => SetValue(StoreProperty, value);
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

        public TableDataRow? SelectedItem
        {
            get => (TableDataRow?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public int RetainedBufferRows
        {
            get => (int)GetValue(RetainedBufferRowsProperty);
            set => SetValue(RetainedBufferRowsProperty, value);
        }

        public void ClearFilterInputs()
        {
            TableView.ClearFilterInputs();
        }

        public void ApplyFilterInputs(IReadOnlyList<DataFilterCriterion> filters)
        {
            TableView.ApplyFilterInputs(filters);
        }

        private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.Columns = (IReadOnlyList<CbsTableColumnDefinition>)e.NewValue;
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.ItemsSource = (IEnumerable?)e.NewValue;
        }

        private static void OnCurrentSortFieldChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.CurrentSortField = (string?)e.NewValue;
        }

        private static void OnCurrentSortDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.CurrentSortDirection = (DataSortDirection?)e.NewValue;
        }

        private static void OnTableStateKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.TableStateKey = (string)e.NewValue;
        }

        private static void OnMultiSelectOptionsSourcesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.MultiSelectOptionsSources =
                (IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>)e.NewValue;
        }

        private static void OnHasMoreItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.HasMoreItems = (bool)e.NewValue;
        }

        private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.IsLoading = (bool)e.NewValue;
        }

        private static void OnLoadedCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.LoadedCount = (int)e.NewValue;
        }

        private static void OnTotalCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.TotalCount = (int)e.NewValue;
        }

        private static void OnRowHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.RowHeight = (double)e.NewValue;
        }

        private static void OnDensityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.Density = (CbsTableDensity)e.NewValue;
        }

        private static void OnRowStyleKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.RowStyleKey = (CbsTableRowStyleKey)e.NewValue;
        }

        private static void OnShowStageCostFractionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.ShowStageCostFraction = (bool)e.NewValue;
        }

        private static void OnSupportsRowSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.SupportsRowSelection = (bool)e.NewValue;
        }

        private static void OnSupportsMultipleRowSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.SupportsMultipleRowSelection = (bool)e.NewValue;
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.SelectedItem = (TableDataRow?)e.NewValue;
        }

        private static void OnRetainedBufferRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TableHostView)d).TableView.RetainedBufferRows = (int)e.NewValue;
        }
    }
}
