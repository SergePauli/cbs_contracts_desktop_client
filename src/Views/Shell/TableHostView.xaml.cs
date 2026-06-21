// Owns per-table UI state and delegates rendering to CbsTableView.
using System.ComponentModel;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions;
using CbsContractsDesktopClient.ViewModels.Data;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class TableHostView : UserControl
    {
        private LazyDataViewState<TableDataRow>? _state;
        private ICbsTableRows<TableDataRow>? _rows;
        private INotifyPropertyChanged? _rowsNotifier;
        private ITableRowReplacementSource? _rowReplacementSource;
        private IReadOnlyList<CbsTableColumnDefinition> _columns = [];
        private IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>> _filterOptionsSources =
            new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase);
        private TablePageDefinition? _definition;
        private string _tableStateKey = string.Empty;
        private string? _currentSortField;
        private DataSortDirection? _currentSortDirection;
        private TableDataRow? _selectedRow;
        private bool _hasMoreItems;
        private bool _isLoading;
        private int _loadedCount;
        private int _totalCount;
        private int _residentCount;
        private int _lastViewportStart = -1;
        private int _lastViewportEnd = -1;
        private int _retainedBufferRows;

        public TableHostView()
        {
            InitializeComponent();
            ApplyVisualOptionsToRenderer();

            TableView.LoadMoreRequested += (_, args) => LoadMoreRequested?.Invoke(this, args);
            TableView.SortRequested += (_, args) => SortRequested?.Invoke(this, args);
            TableView.TraceGenerated += (_, args) => TraceGenerated?.Invoke(this, args);
            TableView.ViewportChanged += (_, args) =>
            {
                _lastViewportStart = args.StartIndex;
                _lastViewportEnd = args.EndIndex;
                _retainedBufferRows = args.RetainedBufferRows;
                ViewportChanged?.Invoke(this, args);
            };
            TableView.ColumnWidthChanged += (_, args) => ColumnWidthChanged?.Invoke(this, args);
            TableView.FilterRequested += (_, args) => FilterRequested?.Invoke(this, args);
            TableView.RowSelectionChanged += (_, args) =>
            {
                _selectedRow = args.IsSelected ? args.Row : null;
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

        internal LazyDataViewState<TableDataRow>? State => _state;

        internal ICbsTableRows<TableDataRow>? Rows => _rows;

        internal IReadOnlyList<CbsTableColumnDefinition> Columns => _columns;

        internal IReadOnlyList<TableDataRow> Items => _rows?.Items ?? [];

        internal IReadOnlyList<DataFilterCriterion> Filters => _state?.Filters.ToList() ?? [];

        internal IReadOnlyList<DataSortCriterion> Sorts => _state?.Sorts.ToList() ?? [];

        public string TableStateKey => _tableStateKey;

        public string? CurrentSortField => _currentSortField;

        public DataSortDirection? CurrentSortDirection => _currentSortDirection;

        public TableDataRow? SelectedRow => _selectedRow;

        public bool HasMoreItems => _hasMoreItems;

        public bool IsLoading => _isLoading;

        public int LoadedCount => _loadedCount;

        public int TotalCount => _totalCount;

        public int ResidentCount => _residentCount;

        public int LastViewportStart => _lastViewportStart;

        public int LastViewportEnd => _lastViewportEnd;

        public int RetainedBufferRows => _retainedBufferRows;

        public double RowHeight
        {
            get => TableView.RowHeight;
            set => TableView.RowHeight = value;
        }

        public CbsTableDensity Density
        {
            get => TableView.Density;
            set => TableView.Density = value;
        }

        public CbsTableRowStyleKey RowStyleKey
        {
            get => TableView.RowStyleKey;
            set => TableView.RowStyleKey = value;
        }

        public bool ShowStageCostFraction
        {
            get => TableView.ShowStageCostFraction;
            set => TableView.ShowStageCostFraction = value;
        }

        public bool SupportsRowSelection
        {
            get => TableView.SupportsRowSelection;
            set => TableView.SupportsRowSelection = value;
        }

        public bool SupportsMultipleRowSelection
        {
            get => TableView.SupportsMultipleRowSelection;
            set => TableView.SupportsMultipleRowSelection = value;
        }

        public void AttachTableState(
            TablePageDefinition definition,
            LazyDataViewState<TableDataRow> state,
            ICbsTableRows<TableDataRow> rows,
            IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>? filterOptionsSources = null)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(rows);

            DetachTableState();

            _definition = definition;
            _state = state;
            _rows = rows;
            _rowsNotifier = rows;
            _rowsNotifier.PropertyChanged += OnRowsPropertyChanged;
            AttachRowReplacementSource(rows);
            _columns = definition.Columns.Where(static column => column.IsVisible).ToList();
            _tableStateKey = definition.Route;
            RowStyleKey = definition.RowStyleKey;
            _filterOptionsSources = filterOptionsSources
                ?? new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase);

            RefreshRowsStateSnapshot();
            RefreshSortSnapshot();
            ApplyRowsStateToRenderer();
            ApplyStructureToRenderer();
        }

        public void AttachTableRows(
            TablePageDefinition definition,
            ICbsTableRows<TableDataRow> rows,
            IReadOnlyList<DataSortCriterion>? sorts = null,
            IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>? filterOptionsSources = null)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(rows);

            DetachTableState();

            _definition = definition;
            _rows = rows;
            _rowsNotifier = rows;
            _rowsNotifier.PropertyChanged += OnRowsPropertyChanged;
            AttachRowReplacementSource(rows);
            _columns = definition.Columns.Where(static column => column.IsVisible).ToList();
            _tableStateKey = definition.Route;
            RowStyleKey = definition.RowStyleKey;
            _filterOptionsSources = filterOptionsSources
                ?? new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase);

            RefreshRowsStateSnapshot();
            RefreshSortSnapshot(sorts);
            ApplyRowsStateToRenderer();
            ApplyStructureToRenderer();
        }

        public void DetachTableState()
        {
            if (_rowsNotifier is not null)
            {
                _rowsNotifier.PropertyChanged -= OnRowsPropertyChanged;
                _rowsNotifier = null;
            }

            DetachRowReplacementSource();

            _definition = null;
            _state = null;
            _rows = null;
            _columns = [];
            _filterOptionsSources = new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase);
            _tableStateKey = string.Empty;
            _currentSortField = null;
            _currentSortDirection = null;
            _selectedRow = null;
            _hasMoreItems = false;
            _isLoading = false;
            _loadedCount = 0;
            _totalCount = 0;
            _residentCount = 0;
            _lastViewportStart = -1;
            _lastViewportEnd = -1;
            _retainedBufferRows = 0;

            ApplyStructureToRenderer();
            ApplyRowsStateToRenderer();
        }

        public void SetFilterOptionsSources(
            IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>> filterOptionsSources)
        {
            ArgumentNullException.ThrowIfNull(filterOptionsSources);
            _filterOptionsSources = filterOptionsSources;
            TableView.MultiSelectOptionsSources = _filterOptionsSources;
        }

        public void SetSelectedRow(TableDataRow? row)
        {
            _selectedRow = row;
            if (row is null)
            {
                TableView.ClearSelection();
                return;
            }

            TableView.SetSelectedItem(row);
        }

        public void RefreshSortSnapshot()
        {
            RefreshSortSnapshot(_state?.Sorts.ToList());
        }

        public void RefreshSortSnapshot(IReadOnlyList<DataSortCriterion>? sorts)
        {
            var sort = sorts?.FirstOrDefault();
            _currentSortField = sort?.FieldKey;
            _currentSortDirection = sort?.Direction;
            TableView.CurrentSortField = _currentSortField;
            TableView.CurrentSortDirection = _currentSortDirection;
        }

        public void RefreshRowsStateSnapshot()
        {
            _hasMoreItems = _rows?.HasMoreItems == true;
            _isLoading = _rows?.IsLoading == true;
            _loadedCount = _rows?.LoadedCount ?? 0;
            _totalCount = _rows?.TotalCount ?? 0;
            _residentCount = _rows?.ResidentCount ?? 0;
        }

        public void ClearFilterInputs()
        {
            TableView.ClearFilterInputs();
        }

        public void ApplyFilterInputs(IReadOnlyList<DataFilterCriterion> filters)
        {
            TableView.ApplyFilterInputs(filters);
        }

        public void InvalidateRows(TableRenderRequest request)
        {
            TableView.InvalidateRows(request);
        }

        private void OnRowsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ICbsTableRows<TableDataRow>.Items))
            {
                RefreshRowsStateSnapshot();
                ApplyRowsStateToRenderer();
                ApplyItemsToRenderer();
                TableView.RefreshVisibleRowsIfViewportHasPlaceholders();
                return;
            }

            if (e.PropertyName == nameof(ICbsTableRows<TableDataRow>.IsLoading)
                || e.PropertyName == nameof(ICbsTableRows<TableDataRow>.HasMoreItems)
                || e.PropertyName == nameof(ICbsTableRows<TableDataRow>.LoadedCount)
                || e.PropertyName == nameof(ICbsTableRows<TableDataRow>.TotalCount)
                || e.PropertyName == nameof(ICbsTableRows<TableDataRow>.ResidentCount)
                || e.PropertyName == nameof(ICbsTableRows<TableDataRow>.Items))
            {
                RefreshRowsStateSnapshot();
                ApplyRowsStateToRenderer();
            }
        }

        private void AttachRowReplacementSource(ICbsTableRows<TableDataRow> rows)
        {
            if (rows is not ITableRowReplacementSource source)
            {
                return;
            }

            _rowReplacementSource = source;
            _rowReplacementSource.RowReplaced += OnRowReplaced;
        }

        private void DetachRowReplacementSource()
        {
            if (_rowReplacementSource is null)
            {
                return;
            }

            _rowReplacementSource.RowReplaced -= OnRowReplaced;
            _rowReplacementSource = null;
        }

        private void OnRowReplaced(object? sender, TableRowReplacedEventArgs e)
        {
            if (e.Row is not TableDataRow row)
            {
                return;
            }

            TableView.RefreshVisibleRow(e.Index, row);
        }

        private void ApplyStructureToRenderer()
        {
            TableView.Columns = _columns;
            TableView.TableStateKey = _tableStateKey;
            TableView.MultiSelectOptionsSources = _filterOptionsSources;
            TableView.CurrentSortField = _currentSortField;
            TableView.CurrentSortDirection = _currentSortDirection;
            TableView.SelectedItem = _selectedRow;
            ApplyItemsToRenderer();
        }

        private void ApplyItemsToRenderer()
        {
            TableView.ItemsSource = _rows?.Items ?? [];
        }

        private void ApplyRowsStateToRenderer()
        {
            TableView.HasMoreItems = _hasMoreItems;
            TableView.IsLoading = _isLoading;
            TableView.LoadedCount = _loadedCount;
            TableView.TotalCount = _totalCount;
            TableView.SelectedItem = _selectedRow;
        }

        private void ApplyVisualOptionsToRenderer()
        {
            TableView.Density = CbsTableDensity.Compact;
            TableView.SupportsMultipleRowSelection = false;
            TableView.SupportsRowSelection = true;
        }
    }
}
