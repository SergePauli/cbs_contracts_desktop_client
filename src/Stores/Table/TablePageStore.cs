using CbsContractsDesktopClient.ViewModels.Shell;
// Owns table-page state, metadata navigation, row loading, and table commands for shell host views.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Collections;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Shell;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;
using CbsContractsDesktopClient.Services.Shell;
using CbsContractsDesktopClient.Services.Workspace;
using CbsContractsDesktopClient.ViewModels.Data;

namespace CbsContractsDesktopClient.Stores.Table
{
    public partial class TablePageStore : ObservableObject
    {
        private static readonly bool DiagnosticsEnabled = true;
        private const int MaxUiTraceLines = 80;
        private readonly AppShellViewModel _shellViewModel;
        private readonly IDataQueryService _dataQueryService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly ITablePageDefinitionService _tablePageDefinitionService;
        private readonly IReferenceLookupCacheService? _referenceLookupCacheService;
        private readonly IUserService? _userService;
        private readonly AuditStore? _auditStore;
        private readonly SemaphoreSlim _navigationGate = new(1, 1);
        private LazyDataViewState<TableDataRow>? _state;
        private ICbsTableRows<TableDataRow>? _rows;
        private INotifyPropertyChanged? _rowsNotifier;
        private CancellationTokenSource? _navigationCts;
        private IReadOnlyList<TableDataRow> _itemsSnapshot = [];
        private string _lastDiagnosticsSnapshot = string.Empty;
        private string _lastDiagnosticsStateKey = string.Empty;
        private int _lastViewportEnsureStart = -1;
        private int _lastViewportEnsureEnd = -1;
        private int _lastViewportVisibleStart = -1;
        private int _lastViewportVisibleEnd = -1;
        private int _lastViewportRetainedBufferRows;
        private int _viewportMutationDepth;
        private bool _deferredItemsRefresh;
        private bool _deferredStateUpdate;

        public TablePageStore(
            AppShellViewModel shellViewModel,
            IDataQueryService dataQueryService,
            IReferenceDefinitionService referenceDefinitionService,
            ITablePageDefinitionService tablePageDefinitionService,
            IReferenceLookupCacheService? referenceLookupCacheService = null,
            IUserService? userService = null,
            AuditStore? auditStore = null)
        {
            _shellViewModel = shellViewModel;
            _dataQueryService = dataQueryService;
            _referenceDefinitionService = referenceDefinitionService;
            _tablePageDefinitionService = tablePageDefinitionService;
            _referenceLookupCacheService = referenceLookupCacheService;
            _userService = userService;
            _auditStore = auditStore;

            FilterFields = [];

            _shellViewModel.PropertyChanged += OnShellViewModelPropertyChanged;
            ApiServiceBase.TraceEmitted += AppendUiTrace;
            ApplyPlaceholderForCurrentSelection();
        }

        [ObservableProperty]
        public partial string SectionTitle { get; set; } = "Справочники";

        [ObservableProperty]
        public partial string ContentTitle { get; set; } = "Справочники";

        [ObservableProperty]
        public partial string ContentDescription { get; set; } = "Выберите справочник в навигации слева.";

        [ObservableProperty]
        public partial string PlaceholderMessage { get; set; } = "Универсальная таблица появится после выбора поддерживаемого маршрута /references/{Model}.";

        [ObservableProperty]
        public partial ReferenceDefinition? CurrentReference { get; set; }

        [ObservableProperty]
        public partial TablePageDefinition? CurrentTablePage { get; set; }

        [ObservableProperty]
        public partial bool IsLoading { get; set; }

        [ObservableProperty]
        public partial string ErrorMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial int TotalCount { get; set; }

        [ObservableProperty]
        public partial bool HasActiveReference { get; set; }

        [ObservableProperty]
        public partial string? CurrentSortField { get; set; }

        [ObservableProperty]
        public partial DataSortDirection? CurrentSortDirection { get; set; }

        [ObservableProperty]
        public partial TableDataRow? SelectedRow { get; set; }

        public ObservableCollection<ReferenceFilterField> FilterFields { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>> CurrentFilterOptionsSources { get; private set; }
            = new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<CbsTableColumnDefinition> CurrentColumns =>
            CurrentTablePage?.Columns.Where(static column => column.IsVisible).ToList() ?? [];

        public string CurrentTableStateKey => CurrentTablePage?.Route ?? string.Empty;

        public bool HasFilters => FilterFields.Count > 0;

        public bool ShowPlaceholder => !HasActiveReference;

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool HasSelectedRow => SelectedRow is not null && !SelectedRow.IsPlaceholder;

        public IReadOnlyList<DataFilterCriterion> CurrentFilters => _state?.Filters.ToList() ?? [];

        public bool CanCreateRows => CurrentTablePage?.Capabilities.HasFlag(TablePageCapabilities.Create) == true;

        public bool CanEditRows => CurrentTablePage?.Capabilities.HasFlag(TablePageCapabilities.Edit) == true;

        public bool CanDeleteRows => CurrentTablePage?.Capabilities.HasFlag(TablePageCapabilities.Delete) == true;

        public bool CanConfigureColumns => CurrentTablePage?.Capabilities.HasFlag(TablePageCapabilities.ConfigureColumns) == true;

        public CbsTableRowStyleKey CurrentRowStyleKey => CurrentTablePage?.RowStyleKey ?? CbsTableRowStyleKey.None;

        public bool HasMoreItems => _rows?.HasMoreItems == true;

        public int LoadedCount => _rows?.LoadedCount ?? 0;

        public int ResidentCount => _rows?.ResidentCount ?? 0;

        public string TotalCountText => $"Записей: {TotalCount}";

        public string CompactHeaderText => ContentTitle;

        public string LoadedCountText => $"Загружено: {LoadedCount} / {TotalCount} | В памяти: {ResidentCount}";

        public string LastCountRequestJson => _rows?.LastCountRequestJson ?? string.Empty;

        public string LastPageRequestJson => _rows?.LastPageRequestJson ?? string.Empty;

        public string TraceLog => _rows?.TraceLog ?? string.Empty;

        public string UiTraceLog { get; private set; } = string.Empty;

        public string CombinedTraceLog => CombineTraceLogs(UiTraceLog, TraceLog);

        public ICbsTableRows<TableDataRow>? Rows => _rows;

        public IReadOnlyList<TableDataRow> Items => _itemsSnapshot;

        public string GetDebugStateSnapshot()
        {
            return
                $"route={_shellViewModel.CurrentRoute ?? "<null>"} " +
                $"hasActiveReference={HasActiveReference} " +
                $"tableModel={CurrentTablePage?.Model ?? "<null>"} " +
                $"state={(_state is null ? "null" : "set")} " +
                $"rows={(_rows is null ? "null" : _rows.GetType().Name)} " +
                $"total={_rows?.TotalCount ?? 0} " +
                $"loaded={_rows?.LoadedCount ?? 0} " +
                $"resident={_rows?.ResidentCount ?? 0}";
        }

        partial void OnHasActiveReferenceChanged(bool value)
        {
            OnPropertyChanged(nameof(ShowPlaceholder));
            OnPropertyChanged(nameof(CompactHeaderText));
            OnPropertyChanged(nameof(CanCreateRows));
            OnPropertyChanged(nameof(CanEditRows));
            OnPropertyChanged(nameof(CanDeleteRows));
            OnPropertyChanged(nameof(CanConfigureColumns));
        }

        partial void OnErrorMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasError));
        }

        partial void OnSelectedRowChanged(TableDataRow? value)
        {
            OnPropertyChanged(nameof(HasSelectedRow));
            SyncAuditContext();
            _ = RefreshAuditAsync();
        }

        partial void OnCurrentReferenceChanged(ReferenceDefinition? value)
        {
            SyncAuditContext();
        }

        partial void OnCurrentTablePageChanged(TablePageDefinition? value)
        {
            OnPropertyChanged(nameof(CurrentColumns));
            OnPropertyChanged(nameof(CurrentTableStateKey));
            OnPropertyChanged(nameof(CanCreateRows));
            OnPropertyChanged(nameof(CanEditRows));
            OnPropertyChanged(nameof(CanDeleteRows));
            OnPropertyChanged(nameof(CanConfigureColumns));
            OnPropertyChanged(nameof(CurrentRowStyleKey));
            SyncAuditContext();
        }

        partial void OnTotalCountChanged(int value)
        {
            OnPropertyChanged(nameof(TotalCountText));
            OnPropertyChanged(nameof(LoadedCountText));
            OnPropertyChanged(nameof(CompactHeaderText));
        }

        partial void OnContentTitleChanged(string value)
        {
            OnPropertyChanged(nameof(CompactHeaderText));
        }

        public void AppendUiTrace(string message)
        {
            if (!DiagnosticsEnabled)
            {
                return;
            }

            if (!ShouldKeepUiTrace(message))
            {
                return;
            }

            var line = $"[{FormatTraceTimestamp(DateTime.Now)}] {message}";
            UiTraceLog = TrimTrace(
                string.IsNullOrWhiteSpace(UiTraceLog)
                    ? line
                    : $"{line}{Environment.NewLine}{UiTraceLog}");

            OnPropertyChanged(nameof(UiTraceLog));
            OnPropertyChanged(nameof(CombinedTraceLog));
            DiagnosticsFileLogger.AppendLine(line);
        }

        public void RefreshAuditPanelSnapshot()
        {
            WriteDiagnosticsSnapshot(force: true);
        }

        private void SyncAuditContext()
        {
            _auditStore?.UpdateContext(
                HasActiveReference,
                CurrentTablePage,
                CurrentReference,
                SelectedRow);
        }

        private async Task RefreshAuditAsync(bool force = false)
        {
            SyncAuditContext();
            if (_auditStore is not null)
            {
                await _auditStore.RefreshAsync(force);
            }
        }

        public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        {
            if (_rows is not null)
            {
                await _rows.InitializeAsync(cancellationToken);
                return;
            }

            await NavigateAsync(_shellViewModel.CurrentRoute, cancellationToken);
        }

        public async Task NavigateToRouteAsync(string? route, CancellationToken cancellationToken = default)
        {
            await NavigateAsync(route, cancellationToken);
        }

        public async Task ReloadCurrentReferenceAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentTablePage is null)
            {
                return;
            }

            await NavigateAsync(CurrentTablePage.Route, cancellationToken);
        }

        public bool ApplySavedRowUpdate(TableDataRow savedRow)
        {
            ArgumentNullException.ThrowIfNull(savedRow);

            var id = TryGetSelectedRowId(savedRow);
            if (id is null || id.Value <= 0)
            {
                return false;
            }

            var sourceRow = _itemsSnapshot.FirstOrDefault(row =>
                !row.IsPlaceholder && TryGetSelectedRowId(row) == id.Value);
            if (sourceRow is null)
            {
                return false;
            }

            return ReplaceLoadedRow(id.Value, savedRow);
        }

        private bool ReplaceLoadedRow(long id, TableDataRow patchedRow)
        {
            var replaced = _state?.Items.TryReplaceLoadedItem(
                row => !row.IsPlaceholder && TryGetSelectedRowId(row) == id,
                patchedRow) == true;

            if (SelectedRow is not null && TryGetSelectedRowId(SelectedRow) == id)
            {
                SelectedRow = patchedRow;
            }

            if (replaced)
            {
                RefreshItemsSnapshot();
                OnPropertyChanged(nameof(Items));
                UpdateStateProperties();
            }

            return replaced;
        }

        public async Task ApplyFilterAsync(
            string fieldKey,
            DataFilterMatchMode matchMode,
            object? value,
            CancellationToken cancellationToken = default)
        {
            AppendUiTrace(
                $"FILTER VM APPLY field={fieldKey} mode={matchMode} value={DescribeFilterValue(value)}");
            if (_state is null)
            {
                AppendUiTrace("FILTER VM STATE NULL");
                return;
            }

            var column = CurrentTablePage?.Columns.FirstOrDefault(
                column => string.Equals(column.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));
            if (column is not null)
            {
                column.Filter.MatchMode = matchMode;
            }

            var normalizedValue = value is CbsTableMultiSelectFilterValue multiSelectValue
                ? (object?)multiSelectValue.SelectedValues
                : value;

            await _state.SetFilterAsync(
                fieldKey,
                column?.Filter.Mode ?? DataFilterMode.Text,
                matchMode,
                normalizedValue,
                cancellationToken);
            await SaveCurrentFiltersAsync(cancellationToken);
            _lastViewportEnsureStart = -1;
            _lastViewportEnsureEnd = -1;
            AppendUiTrace(
                $"FILTER VM APPLIED field={fieldKey} mode={matchMode} value={DescribeFilterValue(normalizedValue)}");
        }

        public async Task<IReadOnlyList<DataFilterCriterion>> ResetFiltersAsync(CancellationToken cancellationToken = default)
        {
            var resetFilters = GetResetFilters();
            await ApplyFilterSetAsync(resetFilters, cancellationToken);

            return resetFilters;
        }

        public async Task<IReadOnlyList<DataFilterCriterion>> ClearFiltersAsync(CancellationToken cancellationToken = default)
        {
            var filters = Array.Empty<DataFilterCriterion>();
            await ApplyFilterSetAsync(filters, cancellationToken);

            return filters;
        }

        private async Task ApplyFilterSetAsync(
            IReadOnlyList<DataFilterCriterion> filters,
            CancellationToken cancellationToken = default)
        {
            var filtersByField = filters.ToDictionary(
                static filter => filter.FieldKey,
                StringComparer.OrdinalIgnoreCase);

            foreach (var filterField in FilterFields)
            {
                filterField.Value = filtersByField.TryGetValue(filterField.FieldKey, out var filter)
                    ? ToFilterFieldValue(filterField, filter.Value)
                    : null;
            }

            ApplyFilterValuesToCurrentColumns(filtersByField);

            if (_state is not null)
            {
                await _state.SetFiltersAsync(filters, cancellationToken);
                await SaveCurrentFiltersAsync(cancellationToken);
            }
        }

        private async Task SaveCurrentFiltersAsync(CancellationToken cancellationToken)
        {
            if (_state is null
                || CurrentTablePage is null
                || !CurrentTablePage.Capabilities.HasFlag(TablePageCapabilities.PersistFilters))
            {
                return;
            }

            await _tablePageDefinitionService.SaveFiltersAsync(
                CurrentTablePage.Route,
                _state.Filters.ToList(),
                cancellationToken);
        }

        private IReadOnlyList<DataFilterCriterion> GetResetFilters()
        {
            if (CurrentTablePage is not null
                && string.Equals(CurrentTablePage.Route, "/stages", StringComparison.OrdinalIgnoreCase))
            {
                var defaults = StageTableFilterDefaultsReader.FromUser(_userService?.CurrentUser);
                AppendUiTrace(StageTableFilterDefaultsReader.BuildTrace(_userService?.CurrentUser, defaults));
                return defaults.ToCriteria();
            }

            if (CurrentTablePage is not null
                && string.Equals(CurrentTablePage.Route, "/contracts", StringComparison.OrdinalIgnoreCase))
            {
                var defaults = ContractTableFilterDefaultsReader.FromUser(_userService?.CurrentUser);
                AppendUiTrace(ContractTableFilterDefaultsReader.BuildTrace(_userService?.CurrentUser, defaults));
                return defaults.ToCriteria();
            }

            return [];
        }

        private void ApplyFilterValuesToCurrentColumns(IReadOnlyDictionary<string, DataFilterCriterion> filtersByField)
        {
            if (CurrentTablePage is null)
            {
                return;
            }

            foreach (var column in CurrentTablePage.Columns.Where(static column => column.IsFilterable))
            {
                if (filtersByField.TryGetValue(column.FieldKey, out var filter))
                {
                    column.Filter.MatchMode = filter.MatchMode;
                    column.Filter.Value = filter.Value;
                    continue;
                }

                column.Filter.Value = null;
            }
        }

        private static object? ToFilterFieldValue(ReferenceFilterField filterField, object? value)
        {
            return filterField.EditorKind == CbsTableFilterEditorKind.MultiSelect
                ? CbsTableMultiSelectFilterValue.Create(filterField.Options, NormalizeFilterSelectedValues(value))
                : value;
        }

        public async Task ApplySortAsync(string fieldKey, DataSortDirection direction, CancellationToken cancellationToken = default)
        {
            if (_state is null || CurrentTablePage is null)
            {
                return;
            }

            await _state.SetSortAsync(fieldKey, direction, cancellationToken);
            await _tablePageDefinitionService.SaveSortAsync(
                new TableSortSettings
                {
                    Route = CurrentTablePage.Route,
                    FieldKey = fieldKey,
                    Direction = direction
                },
                cancellationToken);
            CurrentSortField = fieldKey;
            CurrentSortDirection = direction;
        }

        public async Task ClearSortsAsync(CancellationToken cancellationToken = default)
        {
            if (_state is null || CurrentTablePage is null)
            {
                return;
            }

            await _state.ClearSortsAsync(cancellationToken);
            await _tablePageDefinitionService.SaveSortAsync(
                new TableSortSettings
                {
                    Route = CurrentTablePage.Route,
                    FieldKey = null,
                    Direction = null
                },
                cancellationToken);
            CurrentSortField = null;
            CurrentSortDirection = null;
        }

        public async Task<uint> LoadMoreAsync(uint requestedCount = 50)
        {
            if (_rows is null)
            {
                return 0;
            }

            var result = await _rows.LoadMoreAsync(requestedCount);
            UpdateStateProperties();
            return result;
        }

        public async Task SaveColumnWidthAsync(string fieldKey, string? width, CancellationToken cancellationToken = default)
        {
            if (CurrentTablePage is null || string.IsNullOrWhiteSpace(fieldKey))
            {
                return;
            }

            var column = CurrentTablePage.Columns.FirstOrDefault(
                column => string.Equals(column.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));
            if (column is null)
            {
                return;
            }

            column.Width = width;
            await _tablePageDefinitionService.SaveColumnWidthAsync(
                new TableColumnWidthSettings
                {
                    Route = CurrentTablePage.Route,
                    FieldKey = fieldKey,
                    Width = width
                },
                cancellationToken);
        }

        public async Task ResetColumnWidthsAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentTablePage is null || CurrentTablePage.Columns.Count == 0)
            {
                return;
            }

            foreach (var column in CurrentTablePage.Columns)
            {
                column.Width = null;
                await _tablePageDefinitionService.SaveColumnWidthAsync(
                    new TableColumnWidthSettings
                    {
                        Route = CurrentTablePage.Route,
                        FieldKey = column.FieldKey,
                        Width = null
                    },
                    cancellationToken);
            }

            CurrentTablePage = CurrentTablePage.Clone();
            OnPropertyChanged(nameof(CurrentColumns));
        }

        public async Task SaveColumnLayoutAsync(
            IReadOnlyList<CbsTableColumnDefinition> columns,
            CancellationToken cancellationToken = default)
        {
            if (CurrentTablePage is null || columns.Count == 0)
            {
                return;
            }

            var updatedDefinition = new TablePageDefinition
            {
                Route = CurrentTablePage.Route,
                Model = CurrentTablePage.Model,
                Title = CurrentTablePage.Title,
                NavigationDescription = CurrentTablePage.NavigationDescription,
                Preset = CurrentTablePage.Preset,
                Summary = CurrentTablePage.Summary,
                Kind = CurrentTablePage.Kind,
                Capabilities = CurrentTablePage.Capabilities,
                InitialSortField = CurrentTablePage.InitialSortField,
                InitialSortDirection = CurrentTablePage.InitialSortDirection,
                InitialFilters = CurrentTablePage.InitialFilters,
                Columns = columns,
                RowStyleKey = CurrentTablePage.RowStyleKey
            };

            CurrentTablePage = updatedDefinition;

            await _tablePageDefinitionService.SaveColumnLayoutAsync(
                new TableColumnLayoutSettings
                {
                    Route = updatedDefinition.Route,
                    OrderedFieldKeys = columns.Select(static column => column.FieldKey).ToList(),
                    VisibleFieldKeys = columns
                        .Where(static column => column.IsVisible)
                        .Select(static column => column.FieldKey)
                        .ToList()
                },
                cancellationToken);

            OnPropertyChanged(nameof(CurrentColumns));
            OnPropertyChanged(nameof(CurrentTableStateKey));
        }

        public void UpdateViewportRetention(
            int visibleStart,
            int visibleEnd,
            int retainedBufferRows)
        {
            _lastViewportVisibleStart = visibleStart;
            _lastViewportVisibleEnd = visibleEnd;
            _lastViewportRetainedBufferRows = retainedBufferRows;

            if (_state is null)
            {
                AppendUiTrace($"VIEWMODEL RETENTION STATE NULL {GetDebugStateSnapshot()}");
            }
        }

        public async Task<bool> RefreshCountAfterCreateAsync(CancellationToken cancellationToken = default)
        {
            if (_state is null)
            {
                throw new InvalidOperationException("Cannot refresh count after create because table state is not loaded.");
            }

            return await _state.Items.RefreshCountIfChangedAsync(cancellationToken);
        }

        public async Task RefreshViewportAfterCreateAsync(CancellationToken cancellationToken = default)
        {
            if (_state is null)
            {
                throw new InvalidOperationException("Cannot refresh viewport after create because table state is not loaded.");
            }

            if (_lastViewportVisibleStart < 0
                || _lastViewportVisibleEnd <= _lastViewportVisibleStart)
            {
                throw new InvalidOperationException("Cannot refresh viewport after create because viewport range is not known.");
            }

            var visibleStart = _lastViewportVisibleStart;
            var visibleRowCount = _lastViewportVisibleEnd - visibleStart;
            var shouldExpandVisibleRange = visibleStart == 0
                && visibleRowCount == _state.Items.ResidentCount
                && visibleRowCount == _state.Items.TotalCount - 1;
            var visibleEnd = shouldExpandVisibleRange
                ? _lastViewportVisibleEnd + 1
                : _lastViewportVisibleEnd;
            _lastViewportVisibleEnd = visibleEnd;

            var effectiveBufferRows = Math.Max(
                Math.Max(1, visibleEnd - visibleStart),
                _lastViewportRetainedBufferRows);
            var bufferStart = Math.Max(0, visibleStart - effectiveBufferRows);
            var bufferEnd = visibleEnd + effectiveBufferRows;
            var selectedId = SelectedRow is null || SelectedRow.IsPlaceholder
                ? null
                : TryGetSelectedRowId(SelectedRow);

            await _state.Items.RefreshRangeAsync(
                bufferStart,
                bufferEnd,
                cancellationToken);

            var normalizedBufferEnd = Math.Min(_state.Items.TotalCount, Math.Max(bufferStart, bufferEnd));
            _state.Items.ReleaseOutsideRange(bufferStart, normalizedBufferEnd);
            RefreshItemsSnapshot();
            OnPropertyChanged(nameof(Items));
            UpdateStateProperties();

            TableDataRow? freshSelectedRow = null;
            if (selectedId.HasValue
                && !TryFindLoadedRowByIdInRange(
                    selectedId.Value,
                    bufferStart,
                    normalizedBufferEnd,
                    out freshSelectedRow,
                    out _))
            {
                SelectedRow = null;
                return;
            }

            if (selectedId.HasValue)
            {
                SelectedRow = freshSelectedRow;
            }
        }

        public void ApplyDeletedRowUpdate(long deletedId)
        {
            if (_state is null)
            {
                throw new InvalidOperationException("Cannot apply delete because table state is not loaded.");
            }

            if (_lastViewportVisibleStart < 0
                || _lastViewportVisibleEnd <= _lastViewportVisibleStart)
            {
                throw new InvalidOperationException("Cannot apply delete because viewport range is not known.");
            }

            var visibleStart = _lastViewportVisibleStart;
            var visibleEnd = _lastViewportVisibleEnd;
            var effectiveBufferRows = Math.Max(
                Math.Max(1, visibleEnd - visibleStart),
                _lastViewportRetainedBufferRows);
            var bufferStart = Math.Max(0, visibleStart - effectiveBufferRows);
            var bufferEnd = visibleEnd + effectiveBufferRows;
            var normalizedBufferEnd = Math.Min(_state.Items.TotalCount, Math.Max(bufferStart, bufferEnd));

            if (TryFindLoadedRowByIdInRange(
                    deletedId,
                    bufferStart,
                    normalizedBufferEnd,
                    out _,
                    out var deletedIndex))
            {
                _state.Items.ApplyDeleteShift(deletedIndex, normalizedBufferEnd);
            }
            else
            {
                _state.Items.ApplyDeleteOutsideLoadedRange();
            }

            RefreshItemsSnapshot();
            OnPropertyChanged(nameof(Items));
            UpdateStateProperties();
            _lastViewportVisibleEnd = Math.Min(_lastViewportVisibleEnd, _state.Items.TotalCount);
            _lastViewportEnsureEnd = Math.Min(_lastViewportEnsureEnd, _state.Items.TotalCount);
            SelectedRow = null;
        }

        public async Task EnsureViewportWindowLoadedAsync(
            int visibleStart,
            int visibleEnd,
            int retainedBufferRows,
            CancellationToken cancellationToken = default)
        {
            AppendUiTrace($"STEP VM 01 ensure-enter visible={visibleStart}..{visibleEnd} buffer={retainedBufferRows}");
            if (_state is null)
            {
                AppendUiTrace($"VIEWMODEL LOAD STATE NULL {GetDebugStateSnapshot()}");
                return;
            }

            if (visibleEnd <= visibleStart)
            {
                AppendUiTrace($"STEP VM 01b skip-empty-window visible={visibleStart}..{visibleEnd}");
                return;
            }

            _lastViewportVisibleStart = visibleStart;
            _lastViewportVisibleEnd = visibleEnd;
            _lastViewportRetainedBufferRows = retainedBufferRows;

            if (_lastViewportEnsureStart == visibleStart && _lastViewportEnsureEnd == visibleEnd)
            {
                AppendUiTrace($"STEP VM 01a skip-same-window visible={visibleStart}..{visibleEnd}");
                return;
            }

            _lastViewportEnsureStart = visibleStart;
            _lastViewportEnsureEnd = visibleEnd;

            var effectiveBufferRows = Math.Max(Math.Max(1, visibleEnd - visibleStart), retainedBufferRows);
            var keepStart = visibleStart - effectiveBufferRows;
            var keepEnd = visibleEnd + effectiveBufferRows;
            var hasLoadedPages = false;
            var hasReleasedRows = false;

            try
            {
                BeginViewportMutationBatch();
                try
                {
                    AppendUiTrace($"STEP VM 02 before-ensure-range visible={visibleStart}..{visibleEnd}");
                    hasLoadedPages = await _state.Items.EnsureRangeLoadedAsync(visibleStart, visibleEnd, cancellationToken);
                    AppendUiTrace($"STEP VM 03 after-ensure-range visible={visibleStart}..{visibleEnd}");
                }
                finally
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        AppendUiTrace($"STEP VM 04 before-release visible={visibleStart}..{visibleEnd}");
                        hasReleasedRows = _state.Items.ReleaseOutsideRange(keepStart, keepEnd);
                        AppendUiTrace($"STEP VM 05 after-release visible={visibleStart}..{visibleEnd}");
                    }
                }
            }
            finally
            {
                var hasDeferredUpdates = EndViewportMutationBatch();
                if (!hasLoadedPages && !hasReleasedRows && !hasDeferredUpdates)
                {
                    AppendUiTrace($"STEP VM 06a skip-refresh visible={visibleStart}..{visibleEnd}");
                }
            }
        }

        private async void OnShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppShellViewModel.IsAuditPanelOpen))
            {
                if (_shellViewModel.IsAuditPanelOpen)
                {
                    await RefreshAuditAsync(force: true);
                }

                return;
            }

            if (e.PropertyName != nameof(AppShellViewModel.CurrentRoute))
            {
                return;
            }

            _navigationCts?.Cancel();
            _navigationCts = new CancellationTokenSource();

            try
            {
                await NavigateAsync(_shellViewModel.CurrentRoute, _navigationCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task NavigateAsync(string? route, CancellationToken cancellationToken)
        {
            await _navigationGate.WaitAsync(cancellationToken);

            try
            {
                AppendUiTrace($"NAVIGATE ENTER route={route ?? "<null>"} {GetDebugStateSnapshot()}");

                if (!_tablePageDefinitionService.TryGetByRoute(route, out var definition))
                {
                    AppendUiTrace($"NAVIGATE ROUTE NOT FOUND route={route ?? "<null>"}");
                    ApplyPlaceholderForCurrentSelection();
                    return;
                }

                SectionTitle = definition.Kind == TablePageKind.Reference ? "Справочники" : "База";
                ContentTitle = definition.EffectiveNavigationDescription;
                ContentDescription = definition.Description;
                PlaceholderMessage = string.Empty;
                CurrentTablePage = definition;
                CurrentReference = _referenceDefinitionService.TryGetByRoute(route, out var referenceDefinition)
                    ? referenceDefinition
                    : null;
                HasActiveReference = true;
                CurrentSortField = definition.InitialSortField ?? "id";
                CurrentSortDirection = definition.InitialSortDirection ?? DataSortDirection.Ascending;
                SelectedRow = null;
                UiTraceLog = string.Empty;
                _lastDiagnosticsSnapshot = string.Empty;
                _lastDiagnosticsStateKey = string.Empty;
                _shellViewModel.SetFooterTableStats(string.Empty);

                var initialSorts = CurrentSortField is not null && CurrentSortDirection is DataSortDirection initialDirection
                    ? new[]
                    {
                        new DataSortCriterion
                        {
                            FieldKey = CurrentSortField,
                            Direction = initialDirection
                        }
                    }
                    : Array.Empty<DataSortCriterion>();

                BuildFilters(definition);
                CurrentFilterOptionsSources = new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase);

                var state = new LazyDataViewState<TableDataRow>(
                    _dataQueryService,
                    model: definition.Model,
                    preset: definition.Preset,
                    pageSize: 50,
                    filterFieldMap: definition.Columns.ToDictionary(
                        static column => column.FieldKey,
                        static column => column.FilterField ?? column.ApiField ?? column.FieldKey),
                    sortFieldMap: definition.Columns.ToDictionary(
                        static column => column.FieldKey,
                        static column => column.SortField ?? column.FilterField ?? column.ApiField ?? column.FieldKey),
                    placeholderFactory: TableDataRow.CreatePlaceholder,
                    isPlaceholder: static row => row.IsPlaceholder,
                    initialFilters: definition.InitialFilters,
                    initialSorts: initialSorts);

                AppendUiTrace($"STATE CREATED model={definition.Model} {GetDebugStateSnapshot()}");

                AttachRows(state, new CbsVirtualTableRows<TableDataRow>(state.Items));
                AppendUiTrace($"NAVIGATE AFTER ATTACH model={definition.Model} {GetDebugStateSnapshot()}");

                OnPropertyChanged(nameof(CurrentColumns));
                OnPropertyChanged(nameof(CurrentTableStateKey));
                OnPropertyChanged(nameof(CurrentFilterOptionsSources));
                OnPropertyChanged(nameof(HasFilters));
                OnPropertyChanged(nameof(Items));
                OnPropertyChanged(nameof(Rows));
                OnPropertyChanged(nameof(UiTraceLog));
                OnPropertyChanged(nameof(HasMoreItems));
                OnPropertyChanged(nameof(LoadedCount));
                OnPropertyChanged(nameof(ResidentCount));
                OnPropertyChanged(nameof(LastCountRequestJson));
                OnPropertyChanged(nameof(LastPageRequestJson));
                OnPropertyChanged(nameof(TraceLog));
                OnPropertyChanged(nameof(CombinedTraceLog));

                await _rows!.InitializeAsync(cancellationToken);
                AppendUiTrace($"NAVIGATE AFTER INITIALIZE model={definition.Model} {GetDebugStateSnapshot()}");
                await TryLoadFilterOptionSourcesAsync(definition, cancellationToken);
                UpdateStateProperties();

                if (_shellViewModel.IsAuditPanelOpen)
                {
                    await RefreshAuditAsync(force: true);
                }
            }
            finally
            {
                AppendUiTrace($"NAVIGATE EXIT route={route ?? "<null>"} {GetDebugStateSnapshot()}");
                _navigationGate.Release();
            }
        }

        private void ApplyPlaceholderForCurrentSelection()
        {
            var route = _shellViewModel.CurrentRoute ?? "<null>";
            AppendUiTrace($"PLACEHOLDER APPLY ENTER route={route} {GetDebugStateSnapshot()}");
            DetachState();
            AppendUiTrace($"PLACEHOLDER APPLY AFTER DETACH route={route} {GetDebugStateSnapshot()}");

            var selectedItem = _shellViewModel.SelectedNavigationItem;

            SectionTitle = string.IsNullOrWhiteSpace(selectedItem?.SectionTitle)
                ? "Рабочая область"
                : selectedItem.SectionTitle;

            ContentTitle = string.IsNullOrWhiteSpace(selectedItem?.Title)
                ? "Справочники"
                : selectedItem.Title;

            ContentDescription = string.IsNullOrWhiteSpace(_shellViewModel.CurrentRoute)
                ? "Выберите раздел в навигации слева."
                : _shellViewModel.CurrentRoute;

            PlaceholderMessage = string.IsNullOrWhiteSpace(_shellViewModel.CurrentRoute)
                ? "Универсальная таблица появится после выбора поддерживаемого маршрута."
                : $"Маршрут {_shellViewModel.CurrentRoute} пока не подключен к универсальному табличному view.";

            CurrentReference = null;
            CurrentTablePage = null;
            HasActiveReference = false;
            ErrorMessage = string.Empty;
            TotalCount = 0;
            UiTraceLog = string.Empty;
            _lastDiagnosticsSnapshot = string.Empty;
            _auditStore?.Reset();
            CurrentSortField = null;
            CurrentSortDirection = null;
            SelectedRow = null;
            FilterFields.Clear();
            CurrentFilterOptionsSources = new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase);
            OnPropertyChanged(nameof(CurrentColumns));
            OnPropertyChanged(nameof(CurrentTableStateKey));
            OnPropertyChanged(nameof(CurrentFilterOptionsSources));
            OnPropertyChanged(nameof(HasFilters));
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(Rows));
            OnPropertyChanged(nameof(HasMoreItems));
            OnPropertyChanged(nameof(LoadedCount));
            OnPropertyChanged(nameof(LastCountRequestJson));
            OnPropertyChanged(nameof(LastPageRequestJson));
            OnPropertyChanged(nameof(TraceLog));
            OnPropertyChanged(nameof(UiTraceLog));
            OnPropertyChanged(nameof(CombinedTraceLog));
            _shellViewModel.SetFooterTableStats(string.Empty);
            AppendUiTrace($"PLACEHOLDER APPLY EXIT route={route} {GetDebugStateSnapshot()}");
        }

        private void BuildFilters(TablePageDefinition definition)
        {
            FilterFields.Clear();

            foreach (var column in definition.Columns.Where(static column => column.IsFilterable))
            {
                FilterFields.Add(new ReferenceFilterField
                {
                    FieldKey = column.FieldKey,
                    Header = column.Header,
                    EditorKind = column.Filter.EditorKind,
                    OptionsSourceKey = column.Filter.OptionsSourceKey,
                    Options = column.Filter.StaticOptions
                        .Select(static option => new CbsTableFilterOptionDefinition
                        {
                            Value = option.Value,
                            Label = option.Label
                        })
                        .ToList(),
                    EmptySelectionText = column.Filter.EmptySelectionText,
                    Value = column.Filter.EditorKind == CbsTableFilterEditorKind.MultiSelect
                        ? CbsTableMultiSelectFilterValue.Create(
                            column.Filter.StaticOptions,
                            NormalizeFilterSelectedValues(column.Filter.Value))
                        : column.Filter.Value
                });
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

        private async Task TryLoadFilterOptionSourcesAsync(TablePageDefinition definition, CancellationToken cancellationToken)
        {
            try
            {
                await LoadFilterOptionSourcesAsync(definition, cancellationToken);
                OnPropertyChanged(nameof(CurrentFilterOptionsSources));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppendUiTrace($"FILTER OPTIONS LOAD FAILED model={definition.Model} error={ex.Message}");
                CurrentFilterOptionsSources = new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase);
                OnPropertyChanged(nameof(CurrentFilterOptionsSources));
            }
        }

        private async Task LoadFilterOptionSourcesAsync(TablePageDefinition definition, CancellationToken cancellationToken)
        {
            var sourceKeys = definition.Columns
                .Where(static column =>
                    column.IsFilterable
                    && column.Filter.EditorKind == CbsTableFilterEditorKind.MultiSelect
                    && !string.IsNullOrWhiteSpace(column.Filter.OptionsSourceKey))
                .Select(static column => column.Filter.OptionsSourceKey!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (sourceKeys.Length == 0)
            {
                CurrentFilterOptionsSources = new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var sources = new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase);

            foreach (var sourceKey in sourceKeys)
            {
                sources[sourceKey] = await LoadLookupOptionsAsync(sourceKey, cancellationToken);
            }

            CurrentFilterOptionsSources = sources;
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadLookupOptionsAsync(
            string sourceKey,
            CancellationToken cancellationToken)
        {
            var model = sourceKey switch
            {
                var key when string.Equals(key, "Department", StringComparison.OrdinalIgnoreCase) => "Department",
                var key when string.Equals(key, "Area", StringComparison.OrdinalIgnoreCase) => "Area",
                var key when string.Equals(key, "Ownership", StringComparison.OrdinalIgnoreCase) => "Ownership",
                _ => null
            };

            if (model is null)
            {
                return [];
            }

            if (_referenceLookupCacheService is not null)
            {
                return await _referenceLookupCacheService.GetOptionsAsync(
                    model,
                    cancellationToken: cancellationToken);
            }

            return await LoadLookupOptionsFromApiAsync(model, cancellationToken);
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadLookupOptionsFromApiAsync(
            string model,
            CancellationToken cancellationToken)
        {
            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = model,
                    Preset = "item",
                    Sorts = ["name asc"],
                    Limit = 500
                },
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(static row => new CbsTableFilterOptionDefinition
                {
                    Value = row.GetValue("id"),
                    Label = row.GetValue("name")?.ToString() ?? string.Empty
                })
                .Where(static option => option.Value is not null && !string.IsNullOrWhiteSpace(option.Label))
                .ToList();
        }

        private static string DescribeFilterValue(object? value)
        {
            if (value is null)
            {
                return "<empty>";
            }

            if (value is string text)
            {
                return string.IsNullOrWhiteSpace(text) ? "<empty>" : text;
            }

            if (value is System.Collections.IEnumerable sequence)
            {
                var items = sequence.Cast<object?>().ToArray();
                return items.Length == 0
                    ? "<empty>"
                    : $"[{string.Join(", ", items.Select(static item => item?.ToString() ?? "null"))}]";
            }

            if (value is CbsTableMultiSelectFilterValue multiSelectValue)
            {
                return multiSelectValue.SelectedValues.Count == 0
                    ? "<empty>"
                    : $"[{string.Join(", ", multiSelectValue.SelectedValues.Select(static item => item?.ToString() ?? "null"))}]";
            }

            return value.ToString() ?? "<empty>";
        }

        private void AttachRows(
            LazyDataViewState<TableDataRow> state,
            ICbsTableRows<TableDataRow> rows)
        {
            AppendUiTrace($"ATTACH ROWS ENTER incoming={rows.GetType().Name} {GetDebugStateSnapshot()}");
            if (_state is not null || _rows is not null || _rowsNotifier is not null)
            {
                DetachState();
                AppendUiTrace($"ATTACH ROWS AFTER DETACH incoming={rows.GetType().Name} {GetDebugStateSnapshot()}");
            }

            _lastViewportEnsureStart = -1;
            _lastViewportEnsureEnd = -1;
            _state = state;
            _rows = rows;
            _rowsNotifier = rows;
            _rowsNotifier.PropertyChanged += OnItemsPropertyChanged;
            RefreshItemsSnapshot();
            UpdateStateProperties();
            AppendUiTrace($"ATTACH ROWS EXIT assigned={rows.GetType().Name} {GetDebugStateSnapshot()}");
        }

        private void DetachState()
        {
            AppendUiTrace(
                $"DETACH STATE ENTER rowsNotifier={(_rowsNotifier is null ? "null" : _rowsNotifier.GetType().Name)} {GetDebugStateSnapshot()}");
            if (_rowsNotifier is not null)
            {
                _rowsNotifier.PropertyChanged -= OnItemsPropertyChanged;
                _rowsNotifier = null;
            }

            _state = null;
            _rows = null;
            _itemsSnapshot = [];
            _lastViewportEnsureStart = -1;
            _lastViewportEnsureEnd = -1;
            AppendUiTrace($"DETACH STATE EXIT {GetDebugStateSnapshot()}");
        }

        private void OnItemsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ICbsTableRows<TableDataRow>.IsLoading)
                && e.PropertyName != nameof(ICbsTableRows<TableDataRow>.ErrorMessage)
                && e.PropertyName != nameof(ICbsTableRows<TableDataRow>.TotalCount)
                && e.PropertyName != nameof(ICbsTableRows<TableDataRow>.HasMoreItems)
                && e.PropertyName != nameof(ICbsTableRows<TableDataRow>.LoadedCount)
                && e.PropertyName != nameof(ICbsTableRows<TableDataRow>.ResidentCount)
                && e.PropertyName != nameof(ICbsTableRows<TableDataRow>.LastCountRequestJson)
                && e.PropertyName != nameof(ICbsTableRows<TableDataRow>.LastPageRequestJson)
                && e.PropertyName != nameof(ICbsTableRows<TableDataRow>.TraceLog)
                && e.PropertyName != nameof(ICbsTableRows<TableDataRow>.Items))
            {
                return;
            }

            AppendUiTrace($"STEP VM 09 items-property {e.PropertyName}");

            if (e.PropertyName == nameof(ICbsTableRows<TableDataRow>.IsLoading))
            {
                UpdateStateProperties();
                AppendUiTrace($"STEP VM 12 loading-state-updated {e.PropertyName}");
                return;
            }

            if (e.PropertyName == nameof(ICbsTableRows<TableDataRow>.LoadedCount)
                || e.PropertyName == nameof(ICbsTableRows<TableDataRow>.TotalCount)
                || e.PropertyName == nameof(ICbsTableRows<TableDataRow>.Items))
            {
                if (_viewportMutationDepth > 0)
                {
                    _deferredItemsRefresh = true;
                    _deferredStateUpdate = true;
                    AppendUiTrace($"STEP VM 09a defer-items {e.PropertyName}");
                    return;
                }

                RefreshItemsSnapshot();
                AppendUiTrace($"STEP VM 10 items-refreshed {e.PropertyName}");
                OnPropertyChanged(nameof(Items));
                AppendUiTrace($"STEP VM 11 items-notified {e.PropertyName}");
            }

            if (_viewportMutationDepth > 0)
            {
                _deferredStateUpdate = true;
                AppendUiTrace($"STEP VM 09b defer-state {e.PropertyName}");
                return;
            }

            UpdateStateProperties();
            AppendUiTrace($"STEP VM 12 state-updated {e.PropertyName}");
        }

        private void BeginViewportMutationBatch()
        {
            _viewportMutationDepth++;
        }

        private bool EndViewportMutationBatch()
        {
            if (_viewportMutationDepth > 0)
            {
                _viewportMutationDepth--;
            }

            if (_viewportMutationDepth > 0)
            {
                return false;
            }

            var shouldRefreshItems = _deferredItemsRefresh;
            var shouldUpdateState = _deferredStateUpdate;
            _deferredItemsRefresh = false;
            _deferredStateUpdate = false;

            if (shouldRefreshItems)
            {
                RefreshItemsSnapshot();
                AppendUiTrace("STEP VM 10b batched-items-refreshed");
                OnPropertyChanged(nameof(Items));
                AppendUiTrace("STEP VM 11b batched-items-notified");
            }

            if (shouldRefreshItems || shouldUpdateState)
            {
                UpdateStateProperties();
                AppendUiTrace("STEP VM 12b batched-state-updated");
            }

            return shouldRefreshItems || shouldUpdateState;
        }

        private void UpdateStateProperties()
        {
            IsLoading = _rows?.IsLoading == true;
            TotalCount = _rows?.TotalCount ?? 0;
            ErrorMessage = string.IsNullOrWhiteSpace(_rows?.ErrorMessage)
                ? string.Empty
                : $"Не удалось загрузить таблицу {CurrentTablePage?.Model}: {_rows?.ErrorMessage}";

            OnPropertyChanged(nameof(HasMoreItems));
            OnPropertyChanged(nameof(LoadedCount));
            OnPropertyChanged(nameof(ResidentCount));
            OnPropertyChanged(nameof(LoadedCountText));
            OnPropertyChanged(nameof(LastCountRequestJson));
            OnPropertyChanged(nameof(LastPageRequestJson));
            OnPropertyChanged(nameof(TraceLog));
            OnPropertyChanged(nameof(CombinedTraceLog));
            _shellViewModel.SetFooterTableStats(BuildFooterTotalCountValue());
            WriteDiagnosticsSnapshot();
        }

        private string BuildFooterTotalCountValue()
        {
            if (!HasActiveReference)
            {
                return string.Empty;
            }

            return TotalCount.ToString();
        }

        private void WriteDiagnosticsSnapshot(bool force = false)
        {
            if (!DiagnosticsEnabled)
            {
                return;
            }

            if (!HasActiveReference)
            {
                return;
            }

            if (!force && IsLoading)
            {
                return;
            }

            var diagnosticsStateKey =
                $"{_shellViewModel.CurrentRoute}|{CurrentTablePage?.Model}|{LoadedCount}|{TotalCount}|{ResidentCount}|{LastCountRequestJson}|{LastPageRequestJson}";
            if (!force && string.Equals(_lastDiagnosticsStateKey, diagnosticsStateKey, StringComparison.Ordinal))
            {
                return;
            }

            var diagnosticsText =
                $"Route: {_shellViewModel.CurrentRoute}{Environment.NewLine}" +
                $"Table: {CurrentTablePage?.Model ?? "<none>"}{Environment.NewLine}" +
                $"Loaded: {LoadedCount}/{TotalCount}{Environment.NewLine}" +
                $"Resident: {ResidentCount}/{TotalCount}{Environment.NewLine}{Environment.NewLine}" +
                $"Count payload:{Environment.NewLine}{LastCountRequestJson}{Environment.NewLine}{Environment.NewLine}" +
                $"Page payload:{Environment.NewLine}{LastPageRequestJson}{Environment.NewLine}{Environment.NewLine}" +
                $"Trace:{Environment.NewLine}{CombinedTraceLog}";

            if (!force && string.Equals(_lastDiagnosticsSnapshot, diagnosticsText, StringComparison.Ordinal))
            {
                return;
            }

            _lastDiagnosticsSnapshot = diagnosticsText;
            _lastDiagnosticsStateKey = diagnosticsStateKey;
            DiagnosticsFileLogger.AppendBlock("TABLE DIAGNOSTICS", diagnosticsText);
        }

        private static long? TryGetSelectedRowId(TableDataRow row)
        {
            var value = row.GetValue("id");

            return value switch
            {
                long longValue => longValue,
                int intValue => intValue,
                string text when long.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private bool TryFindLoadedRowByIdInRange(
            long id,
            int startIndex,
            int endExclusive,
            out TableDataRow row,
            out int rowIndex)
        {
            var start = Math.Max(0, startIndex);
            var end = Math.Min(_itemsSnapshot.Count, Math.Max(start, endExclusive));
            for (var index = start; index < end; index++)
            {
                var candidate = _itemsSnapshot[index];
                if (!candidate.IsPlaceholder && TryGetSelectedRowId(candidate) == id)
                {
                    row = candidate;
                    rowIndex = index;
                    return true;
                }
            }

            row = null!;
            rowIndex = -1;
            return false;
        }

        private void RefreshItemsSnapshot()
        {
            if (_rows is null)
            {
                _itemsSnapshot = [];
                return;
            }

            if (_rows.Items is IList<TableDataRow> list)
            {
                _itemsSnapshot = new ReadOnlyCollection<TableDataRow>(list);
                return;
            }

            _itemsSnapshot = _rows.Items.ToList();
        }

        private static string TrimTrace(string trace)
        {
            var lines = trace
                .Split(Environment.NewLine, StringSplitOptions.None)
                .Take(MaxUiTraceLines);

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatTraceTimestamp(DateTime timestamp)
        {
            return timestamp.ToString("mm':'ss'.'fff");
        }

        private static string CombineTraceLogs(string uiTraceLog, string dataTraceLog)
        {
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(uiTraceLog))
            {
                lines.AddRange(uiTraceLog.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
            }

            if (!string.IsNullOrWhiteSpace(dataTraceLog))
            {
                lines.AddRange(dataTraceLog.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
            }

            if (lines.Count == 0)
            {
                return string.Empty;
            }

            var orderedLines = lines
                .OrderByDescending(GetTraceOrderKey)
                .Take(MaxUiTraceLines);

            return string.Join(Environment.NewLine, orderedLines);
        }

        private static TimeSpan GetTraceOrderKey(string line)
        {
            if (line.Length < 12 || line[0] != '[')
            {
                return TimeSpan.MinValue;
            }

            return TimeSpan.TryParseExact(
                line.AsSpan(1, 9),
                @"mm\:ss\.fff",
                null,
                out var timestamp)
                ? timestamp
                : TimeSpan.MinValue;
        }

        private static bool ShouldKeepUiTrace(string message)
        {
            return message.StartsWith("NAVIGATE ", StringComparison.Ordinal)
                || message.StartsWith("REFERENCE EDIT ", StringComparison.Ordinal)
                || message.StartsWith("STATE CREATED", StringComparison.Ordinal)
                || message.StartsWith("PLACEHOLDER APPLY", StringComparison.Ordinal)
                || message.StartsWith("ATTACH ROWS", StringComparison.Ordinal)
                || message.StartsWith("DETACH STATE", StringComparison.Ordinal)
                || message.StartsWith("HTTP ", StringComparison.Ordinal)
                || message.StartsWith("API SEND ", StringComparison.Ordinal)
                || message.StartsWith("DATA QUERY ", StringComparison.Ordinal)
                || message.StartsWith("STEP API ", StringComparison.Ordinal)
                || message.StartsWith("STEP VM ", StringComparison.Ordinal)
                || message.StartsWith("TABLE ", StringComparison.Ordinal)
                || message.StartsWith("VIEWPORT CHANGED ", StringComparison.Ordinal)
                || message.StartsWith("Trigger load more ", StringComparison.Ordinal)
                || message.StartsWith("Attached explicit table ScrollViewer", StringComparison.Ordinal)
                || message.StartsWith("FILTER ", StringComparison.Ordinal)
                || message.StartsWith("CONTRACT FILTER DEFAULTS ", StringComparison.Ordinal)
                || message.StartsWith("CONTRACT FILTER SETTINGS ", StringComparison.Ordinal)
                || message.StartsWith("STAGE FILTER DEFAULTS ", StringComparison.Ordinal)
                || message.StartsWith("STAGE FILTER SETTINGS ", StringComparison.Ordinal)
                || message.StartsWith("VIEWMODEL LOAD STATE NULL", StringComparison.Ordinal)
                || message.StartsWith("VIEWMODEL RETENTION STATE NULL", StringComparison.Ordinal);
        }

    }
}



