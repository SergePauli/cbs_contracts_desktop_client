using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Collections;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Shell;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.Data;

namespace CbsContractsDesktopClient.ViewModels.Shell
{
    public partial class ReferencesContentViewModel : ObservableObject
    {
        private static readonly bool DiagnosticsEnabled = true;
        private const int MaxUiTraceLines = 80;
        private const int AuditPageSize = 20;
        private readonly AppShellViewModel _shellViewModel;
        private readonly IDataQueryService _dataQueryService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly SemaphoreSlim _navigationGate = new(1, 1);
        private LazyDataViewState<ReferenceDataRow>? _state;
        private ICbsTableRows<ReferenceDataRow>? _rows;
        private INotifyPropertyChanged? _rowsNotifier;
        private CancellationTokenSource? _navigationCts;
        private CancellationTokenSource? _auditCts;
        private IReadOnlyList<ReferenceDataRow> _itemsSnapshot = [];
        private string _lastDiagnosticsSnapshot = string.Empty;
        private string _lastAuditPanelKey = string.Empty;
        private List<AuditRecord> _auditRecords = [];
        private int _auditOffset;
        private bool _hasPreviousAuditRecords;
        private bool _hasNextAuditRecords = true;
        private bool _isAuditLoading;
        private int _lastViewportEnsureStart = -1;
        private int _lastViewportEnsureEnd = -1;

        public ReferencesContentViewModel(
            AppShellViewModel shellViewModel,
            IDataQueryService dataQueryService,
            IReferenceDefinitionService referenceDefinitionService)
        {
            _shellViewModel = shellViewModel;
            _dataQueryService = dataQueryService;
            _referenceDefinitionService = referenceDefinitionService;

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
        public partial ReferenceDataRow? SelectedRow { get; set; }

        public ObservableCollection<ReferenceFilterField> FilterFields { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>> CurrentFilterOptionsSources { get; private set; }
            = new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<CbsTableColumnDefinition> CurrentColumns => CurrentReference?.Columns ?? [];

        public string CurrentTableStateKey => CurrentReference?.Route ?? string.Empty;

        public bool HasFilters => FilterFields.Count > 0;

        public bool ShowPlaceholder => !HasActiveReference;

        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool HasSelectedRow => SelectedRow is not null && !SelectedRow.IsPlaceholder;

        public bool IsEmployeeReference => CurrentReference?.EditorKind == ReferenceEditorKind.Employee;

        public bool ShowEmployeeDetailView => IsEmployeeReference && HasSelectedRow;

        public string SelectedRowInfoMessage => BuildSelectedRowInfoMessage();

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

        public ICbsTableRows<ReferenceDataRow>? Rows => _rows;

        public IReadOnlyList<ReferenceDataRow> Items => _itemsSnapshot;

        public string GetDebugStateSnapshot()
        {
            return
                $"route={_shellViewModel.CurrentRoute ?? "<null>"} " +
                $"hasActiveReference={HasActiveReference} " +
                $"referenceModel={CurrentReference?.Model ?? "<null>"} " +
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
            OnPropertyChanged(nameof(ShowEmployeeDetailView));
        }

        partial void OnErrorMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasError));
        }

        partial void OnSelectedRowChanged(ReferenceDataRow? value)
        {
            OnPropertyChanged(nameof(HasSelectedRow));
            OnPropertyChanged(nameof(SelectedRowInfoMessage));
            OnPropertyChanged(nameof(ShowEmployeeDetailView));
            _shellViewModel.SetFooterTableStats(
                BuildFooterTotalCountValue(),
                BuildFooterSelectedRecordText());
            _ = RefreshAuditPanelAsync();
        }

        partial void OnCurrentReferenceChanged(ReferenceDefinition? value)
        {
            OnPropertyChanged(nameof(IsEmployeeReference));
            OnPropertyChanged(nameof(ShowEmployeeDetailView));

            _auditCts?.Cancel();
            ResetAuditPagingState();
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

        public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        {
            if (_rows is not null)
            {
                await _rows.InitializeAsync(cancellationToken);
                return;
            }

            await NavigateAsync(_shellViewModel.CurrentRoute, cancellationToken);
        }

        public async Task ReloadCurrentReferenceAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentReference is null)
            {
                return;
            }

            await NavigateAsync(CurrentReference.Route, cancellationToken);
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

            var column = CurrentReference?.Columns.FirstOrDefault(
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
            _lastViewportEnsureStart = -1;
            _lastViewportEnsureEnd = -1;
            AppendUiTrace(
                $"FILTER VM APPLIED field={fieldKey} mode={matchMode} value={DescribeFilterValue(normalizedValue)}");
        }

        public async Task ResetFiltersAsync(CancellationToken cancellationToken = default)
        {
            foreach (var filterField in FilterFields)
            {
                filterField.Value = null;
            }

            if (_state is not null)
            {
                await _state.ClearFiltersAsync(cancellationToken);
            }
        }

        public async Task ApplySortAsync(string fieldKey, DataSortDirection direction, CancellationToken cancellationToken = default)
        {
            if (_state is null || CurrentReference is null)
            {
                return;
            }

            await _state.SetSortAsync(fieldKey, direction, cancellationToken);
            await _referenceDefinitionService.SaveSortAsync(
                new ReferenceTableSortSettings
                {
                    Route = CurrentReference.Route,
                    FieldKey = fieldKey,
                    Direction = direction
                },
                cancellationToken);
            CurrentSortField = fieldKey;
            CurrentSortDirection = direction;
        }

        public async Task ClearSortsAsync(CancellationToken cancellationToken = default)
        {
            if (_state is null || CurrentReference is null)
            {
                return;
            }

            await _state.ClearSortsAsync(cancellationToken);
            await _referenceDefinitionService.SaveSortAsync(
                new ReferenceTableSortSettings
                {
                    Route = CurrentReference.Route,
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

        public async Task<bool> ShiftAuditPanelWindowAsync(int direction)
        {
            if (!_shellViewModel.IsAuditPanelOpen
                || _isAuditLoading
                || string.IsNullOrWhiteSpace(_lastAuditPanelKey))
            {
                return false;
            }

            if (direction > 0)
            {
                if (!_hasNextAuditRecords)
                {
                    return false;
                }

                return await LoadAuditPageAsync(_lastAuditPanelKey, _auditOffset + AuditPageSize);
            }

            if (direction < 0)
            {
                if (!_hasPreviousAuditRecords)
                {
                    return false;
                }

                return await LoadAuditPageAsync(
                    _lastAuditPanelKey,
                    Math.Max(0, _auditOffset - AuditPageSize));
            }

            return false;
        }

        public async Task SaveColumnWidthAsync(string fieldKey, string? width, CancellationToken cancellationToken = default)
        {
            if (CurrentReference is null || string.IsNullOrWhiteSpace(fieldKey))
            {
                return;
            }

            var column = CurrentReference.Columns.FirstOrDefault(
                column => string.Equals(column.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));
            if (column is null)
            {
                return;
            }

            column.Width = width;
            await _referenceDefinitionService.SaveColumnWidthAsync(
                new ReferenceTableColumnWidthSettings
                {
                    Route = CurrentReference.Route,
                    FieldKey = fieldKey,
                    Width = width
                },
                cancellationToken);
        }

        public async Task ResetColumnWidthsAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentReference is null || CurrentReference.Columns.Count == 0)
            {
                return;
            }

            foreach (var column in CurrentReference.Columns)
            {
                column.Width = null;
                await _referenceDefinitionService.SaveColumnWidthAsync(
                    new ReferenceTableColumnWidthSettings
                    {
                        Route = CurrentReference.Route,
                        FieldKey = column.FieldKey,
                        Width = null
                    },
                    cancellationToken);
            }

            CurrentReference = CurrentReference.Clone();
            OnPropertyChanged(nameof(CurrentColumns));
        }

        public void UpdateViewportRetention(
            int visibleStart,
            int visibleEnd,
            int retainedBufferRows)
        {
            if (_state is null)
            {
                AppendUiTrace($"VIEWMODEL RETENTION STATE NULL {GetDebugStateSnapshot()}");
            }
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

                if (hasLoadedPages || hasReleasedRows)
                {
                    RefreshItemsSnapshot();
                    AppendUiTrace($"STEP VM 06 after-refresh-snapshot visible={visibleStart}..{visibleEnd}");
                    OnPropertyChanged(nameof(Items));
                    AppendUiTrace($"STEP VM 07 after-items-changed visible={visibleStart}..{visibleEnd}");
                    UpdateStateProperties();
                    AppendUiTrace($"STEP VM 08 after-state-update visible={visibleStart}..{visibleEnd}");
                }
                else
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
                    await RefreshAuditPanelAsync(force: true);
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

                if (!_referenceDefinitionService.TryGetByRoute(route, out var definition))
                {
                    AppendUiTrace($"NAVIGATE ROUTE NOT FOUND route={route ?? "<null>"}");
                    ApplyPlaceholderForCurrentSelection();
                    return;
                }

                SectionTitle = "Справочники";
                ContentTitle = definition.EffectiveNavigationDescription;
                ContentDescription = definition.Description;
                PlaceholderMessage = string.Empty;
                CurrentReference = definition;
                HasActiveReference = true;
                CurrentSortField = definition.InitialSortField ?? "id";
                CurrentSortDirection = definition.InitialSortDirection ?? DataSortDirection.Ascending;
                SelectedRow = null;
                UiTraceLog = string.Empty;
                _lastDiagnosticsSnapshot = string.Empty;
                ResetAuditPagingState();
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

                _state = new LazyDataViewState<ReferenceDataRow>(
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
                    placeholderFactory: ReferenceDataRow.CreatePlaceholder,
                    isPlaceholder: static row => row.IsPlaceholder,
                    initialSorts: initialSorts);

                AppendUiTrace($"STATE CREATED model={definition.Model} {GetDebugStateSnapshot()}");

                AttachRows(_state, new CbsVirtualTableRows<ReferenceDataRow>(_state.Items));
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
                    await RefreshAuditPanelAsync(force: true);
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
                ? "Универсальная таблица появится после выбора поддерживаемого справочника."
                : $"Маршрут {_shellViewModel.CurrentRoute} пока не подключен к универсальному view справочников.";

            CurrentReference = null;
            HasActiveReference = false;
            ErrorMessage = string.Empty;
            TotalCount = 0;
            UiTraceLog = string.Empty;
            _lastDiagnosticsSnapshot = string.Empty;
            ResetAuditPagingState();
            CurrentSortField = null;
            CurrentSortDirection = null;
            SelectedRow = null;
            FilterFields.Clear();
            CurrentFilterOptionsSources = new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase);
            _shellViewModel.ResetAuditPanelState();

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

        private void BuildFilters(ReferenceDefinition definition)
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
                        ? CbsTableMultiSelectFilterValue.Create(column.Filter.StaticOptions, Array.Empty<object?>())
                        : null
                });
            }
        }

        private async Task TryLoadFilterOptionSourcesAsync(ReferenceDefinition definition, CancellationToken cancellationToken)
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

        private async Task LoadFilterOptionSourcesAsync(ReferenceDefinition definition, CancellationToken cancellationToken)
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
            if (!string.Equals(sourceKey, "Department", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            var rows = await _dataQueryService.GetDataAsync<ReferenceDataRow>(
                new DataQueryRequest
                {
                    Model = "Department",
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
            LazyDataViewState<ReferenceDataRow> state,
            ICbsTableRows<ReferenceDataRow> rows)
        {
            AppendUiTrace($"ATTACH ROWS ENTER incoming={rows.GetType().Name} {GetDebugStateSnapshot()}");
            DetachState();
            AppendUiTrace($"ATTACH ROWS AFTER DETACH incoming={rows.GetType().Name} {GetDebugStateSnapshot()}");
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
            if (e.PropertyName != nameof(ICbsTableRows<ReferenceDataRow>.IsLoading)
                && e.PropertyName != nameof(ICbsTableRows<ReferenceDataRow>.ErrorMessage)
                && e.PropertyName != nameof(ICbsTableRows<ReferenceDataRow>.TotalCount)
                && e.PropertyName != nameof(ICbsTableRows<ReferenceDataRow>.HasMoreItems)
                && e.PropertyName != nameof(ICbsTableRows<ReferenceDataRow>.LoadedCount)
                && e.PropertyName != nameof(ICbsTableRows<ReferenceDataRow>.ResidentCount)
                && e.PropertyName != nameof(ICbsTableRows<ReferenceDataRow>.LastCountRequestJson)
                && e.PropertyName != nameof(ICbsTableRows<ReferenceDataRow>.LastPageRequestJson)
                && e.PropertyName != nameof(ICbsTableRows<ReferenceDataRow>.TraceLog)
                && e.PropertyName != nameof(ICbsTableRows<ReferenceDataRow>.Items))
            {
                return;
            }

            AppendUiTrace($"STEP VM 09 items-property {e.PropertyName}");

            if (e.PropertyName == nameof(ICbsTableRows<ReferenceDataRow>.LoadedCount)
                || e.PropertyName == nameof(ICbsTableRows<ReferenceDataRow>.TotalCount)
                || e.PropertyName == nameof(ICbsTableRows<ReferenceDataRow>.Items))
            {
                RefreshItemsSnapshot();
                AppendUiTrace($"STEP VM 10 items-refreshed {e.PropertyName}");
                OnPropertyChanged(nameof(Items));
                AppendUiTrace($"STEP VM 11 items-notified {e.PropertyName}");
            }

            UpdateStateProperties();
            AppendUiTrace($"STEP VM 12 state-updated {e.PropertyName}");
        }

        private void UpdateStateProperties()
        {
            IsLoading = _rows?.IsLoading == true;
            TotalCount = _rows?.TotalCount ?? 0;
            ErrorMessage = string.IsNullOrWhiteSpace(_rows?.ErrorMessage)
                ? string.Empty
                : $"Не удалось загрузить справочник {CurrentReference?.Model}: {_rows?.ErrorMessage}";

            OnPropertyChanged(nameof(HasMoreItems));
            OnPropertyChanged(nameof(LoadedCount));
            OnPropertyChanged(nameof(ResidentCount));
            OnPropertyChanged(nameof(LoadedCountText));
            OnPropertyChanged(nameof(LastCountRequestJson));
            OnPropertyChanged(nameof(LastPageRequestJson));
            OnPropertyChanged(nameof(TraceLog));
            OnPropertyChanged(nameof(CombinedTraceLog));
            _shellViewModel.SetFooterTableStats(
                BuildFooterTotalCountValue(),
                BuildFooterSelectedRecordText());
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

        private string BuildFooterSelectedRecordText()
        {
            if (!HasSelectedRow || SelectedRow is null)
            {
                return string.Empty;
            }

            var name = SelectedRow.GetValue("name")?.ToString();
            var id = SelectedRow.GetValue("id")?.ToString();

            if (string.Equals(CurrentReference?.Model, "Profile", StringComparison.OrdinalIgnoreCase))
            {
                var login =
                    SelectedRow.GetValue("user.name")?.ToString()
                    ?? name;
                var fio =
                    SelectedRow.GetValue("user.person.full_name")?.ToString()
                    ?? SelectedRow.GetValue("user.person.person_name.naming.fio")?.ToString()
                    ?? SelectedRow.GetValue("full_name")?.ToString()
                    ?? SelectedRow.GetValue("person")?.ToString();

                if (!string.IsNullOrWhiteSpace(login) && !string.IsNullOrWhiteSpace(fio))
                {
                    return $"{login} - {fio}";
                }

                if (!string.IsNullOrWhiteSpace(login))
                {
                    return login;
                }

                return fio ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
            {
                return $"{name} (ID: {id})";
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return string.IsNullOrWhiteSpace(id)
                ? string.Empty
                : $"ID: {id}";
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

            var diagnosticsText =
                $"Route: {_shellViewModel.CurrentRoute}{Environment.NewLine}" +
                $"Reference: {CurrentReference?.Model ?? "<none>"}{Environment.NewLine}" +
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
            DiagnosticsFileLogger.AppendBlock("TABLE DIAGNOSTICS", diagnosticsText);
        }

        private async Task RefreshAuditPanelAsync(bool force = false)
        {
            if (!_shellViewModel.IsAuditPanelOpen)
            {
                return;
            }

            if (HasSelectedRow && SelectedRow is not null && TryGetSelectedRowId(SelectedRow) is null)
            {
                _auditCts?.Cancel();
                ResetAuditPagingState();
                _shellViewModel.SetAuditPanelState(new AuditPanelState
                {
                    Title = "Аудит изменений",
                    Description = CurrentReference is null
                        ? "Выбранная запись"
                        : BuildSelectedRecordAuditDescription(CurrentReference, SelectedRow),
                    Entries =
                    [
                        BuildAuditPanelMessageEntry(
                            "ID не найден",
                            "Не удалось определить ID записи для загрузки аудита.")
                    ]
                });
                _shellViewModel.SetAuditPanelText("Не удалось определить ID записи для загрузки аудита.");
                return;
            }

            var scope = BuildAuditScope();
            if (scope is null)
            {
                _auditCts?.Cancel();
                ResetAuditPagingState();
                _shellViewModel.SetAuditPanelState(new AuditPanelState
                {
                    Title = "Аудит изменений",
                    Description = "Выберите справочник, чтобы увидеть последние события.",
                    Entries =
                    [
                        BuildAuditPanelMessageEntry(
                            "Справочник не выбран",
                            "Последние события появятся после выбора активного справочника.")
                    ]
                });
                _shellViewModel.SetAuditPanelText("Справочник не выбран.");
                return;
            }

            if (!force && string.Equals(_lastAuditPanelKey, scope.Key, StringComparison.Ordinal))
            {
                return;
            }

            _auditCts?.Cancel();
            ResetAuditPagingState();
            _lastAuditPanelKey = scope.Key;

            _shellViewModel.SetAuditPanelState(new AuditPanelState
            {
                Title = scope.Title,
                Description = scope.Description,
                Entries =
                [
                    BuildAuditPanelMessageEntry(
                        "Загрузка",
                        "Загрузка событий аудита...")
                ]
            });
            _shellViewModel.SetAuditPanelText("Загрузка событий аудита...");

            await LoadAuditPageAsync(scope.Key, offset: 0);
        }

        private async Task<bool> LoadAuditPageAsync(string auditKey, int offset)
        {
            var scope = BuildAuditScope();
            if (scope is null || !string.Equals(scope.Key, auditKey, StringComparison.Ordinal))
            {
                return false;
            }

            _isAuditLoading = true;
            _auditCts?.Cancel();
            _auditCts = new CancellationTokenSource();
            var cancellationToken = _auditCts.Token;
            var requestedOffset = Math.Max(0, offset);

            try
            {
                var audits = await _dataQueryService.GetDataAsync<AuditRecord>(
                    new DataQueryRequest
                    {
                        Model = "Audit",
                        Filters = scope.Filters,
                        Sorts = ["created_at desc"],
                        Limit = AuditPageSize,
                        Offset = requestedOffset,
                        Preset = "card"
                    },
                    cancellationToken);

                if (cancellationToken.IsCancellationRequested
                    || !string.Equals(_lastAuditPanelKey, scope.Key, StringComparison.Ordinal))
                {
                    return false;
                }

                _auditRecords = audits
                    .OrderByDescending(GetAuditSortTimestamp)
                    .ThenByDescending(static audit => audit.Id)
                    .ToList();
                _auditOffset = requestedOffset;
                _hasPreviousAuditRecords = _auditOffset > 0;
                _hasNextAuditRecords = audits.Count == AuditPageSize;

                _shellViewModel.SetAuditPanelState(new AuditPanelState
                {
                    Title = scope.Title,
                    Description = BuildAuditWindowDescription(scope, _auditOffset),
                    Entries = BuildAuditEntries(_auditRecords)
                });
                _shellViewModel.SetAuditPanelText(BuildAuditPanelText(_auditRecords));
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _shellViewModel.SetAuditPanelState(new AuditPanelState
                    {
                        Title = scope.Title,
                        Description = $"{BuildAuditWindowDescription(scope, _auditOffset)} Не удалось загрузить страницу: {ex.Message}",
                        Entries = _auditRecords.Count == 0
                            ? [
                                BuildAuditPanelMessageEntry(
                                    "Не удалось загрузить аудит",
                                    ex.Message)
                            ]
                            : BuildAuditEntries(_auditRecords)
                    });
                    _shellViewModel.SetAuditPanelText($"Не удалось загрузить аудит: {ex.Message}");
                }

                return false;
            }
            finally
            {
                _isAuditLoading = false;
            }
        }

        private void ResetAuditPagingState()
        {
            _lastAuditPanelKey = string.Empty;
            _auditRecords = [];
            _auditOffset = 0;
            _hasPreviousAuditRecords = false;
            _hasNextAuditRecords = true;
            _isAuditLoading = false;
        }

        private AuditScope? BuildAuditScope()
        {
            if (!HasActiveReference || CurrentReference is null)
            {
                return null;
            }

            var model = CurrentReference.Model;
            var filters = new Dictionary<string, object?>
            {
                ["auditable_type__eq"] = model
            };

            if (HasSelectedRow && SelectedRow is not null)
            {
                var selectedId = TryGetSelectedRowId(SelectedRow);
                if (selectedId is null)
                {
                    return null;
                }

                filters["auditable_id__eq"] = selectedId.Value;
                return new AuditScope(
                    $"record:{model}:{selectedId.Value}",
                    "Аудит изменений",
                    BuildSelectedRecordAuditDescription(CurrentReference, SelectedRow),
                    filters);
            }

            return new AuditScope(
                $"reference:{model}",
                "Последние события аудита",
                $"Активный справочник: {CurrentReference.EffectiveNavigationDescription}",
                filters);
        }

        private static string BuildAuditWindowDescription(AuditScope scope, int offset)
        {
            return offset == 0
                ? scope.Description
                : $"{scope.Description}. Позиция timeline: {offset + 1}";
        }

        private static IReadOnlyList<AuditEntry> BuildAuditEntries(IReadOnlyList<AuditRecord> audits)
        {
            if (audits.Count == 0)
            {
                return
                [
                    BuildAuditPanelMessageEntry(
                        "Событий не найдено",
                        "По текущему контексту нет событий аудита.")
                ];
            }

            return audits.Select(ToAuditEntry).ToList();
        }

        private static AuditEntry ToAuditEntry(AuditRecord audit)
        {
            return new AuditEntry
            {
                Timestamp = audit.When ?? string.Empty,
                Title = GetAuditActionTitle(audit.Action),
                Description = BuildAuditRecordText(audit),
                BackgroundBrushKey = GetAuditBrushKey(audit.Action)
            };
        }

        private static string BuildAuditPanelText(IReadOnlyList<AuditRecord> audits)
        {
            if (audits.Count == 0)
            {
                return "Событий не найдено.";
            }

            return string.Join(
                $"{Environment.NewLine}{Environment.NewLine}",
                audits.Select(BuildAuditRecordText));
        }

        private static AuditEntry BuildAuditPanelMessageEntry(string title, string description)
        {
            return new AuditEntry
            {
                Timestamp = "Статус",
                Title = title,
                Description = description,
                BackgroundBrushKey = "ShellMutedPanelBackgroundBrush"
            };
        }

        private static DateTimeOffset GetAuditSortTimestamp(AuditRecord audit)
        {
            return DateTimeOffset.TryParse(audit.When, out var timestamp)
                ? timestamp
                : DateTimeOffset.MinValue;
        }

        private static string BuildAuditRecordText(AuditRecord audit)
        {
            var lines = new List<string>();

            if (!string.IsNullOrWhiteSpace(audit.Where))
            {
                lines.Add($"где: {audit.Where}");
            }

            var what = !string.IsNullOrWhiteSpace(audit.What)
                ? audit.What
                : audit.Detail;
            if (!string.IsNullOrWhiteSpace(what))
            {
                lines.Add($"что: {what}");
            }

            if (!string.IsNullOrWhiteSpace(audit.Field))
            {
                lines.Add($"поле: {audit.Field}; изменено {audit.Before} на {audit.After}");
            }

            lines.Add($"кем: {audit.Who ?? string.Empty}");
            return lines.Count == 0
                ? "Детали события не переданы."
                : string.Join(Environment.NewLine, lines);
        }

        private static string GetAuditActionTitle(string? action)
        {
            return action switch
            {
                "added" => "Добавлено:",
                "updated" => "Изменено:",
                "removed" => "Удалено:",
                "deleted" => "Удалено:",
                "archived" => "Архивировано:",
                "imported" => "Импорт:",
                "closed" => "Закрыто:",
                "signed" => "Подписано:",
                _ => action ?? "Событие:"
            };
        }

        private static string GetAuditBrushKey(string? action)
        {
            return action switch
            {
                "added" => "ShellTableRowSelectedBackgroundBrush",
                "updated" => "ShellAccentPanelBackgroundBrush",
                "removed" or "deleted" => "ShellTableHeaderBackgroundBrush",
                _ => "ShellAccentPanelBackgroundBrush"
            };
        }

        private static string BuildEmployeeAuditDescription(ReferenceDataRow row)
        {
            var name =
                row.GetValue("person.full_name")?.ToString()
                ?? row.GetValue("name")?.ToString()
                ?? row.GetValue("head")?.ToString()
                ?? "Сотрудник";
            var id = row.GetValue("id")?.ToString();

            return string.IsNullOrWhiteSpace(id)
                ? name
                : $"{name} (ID: {id})";
        }

        private static string BuildSelectedRecordAuditDescription(
            ReferenceDefinition definition,
            ReferenceDataRow row)
        {
            if (definition.EditorKind == ReferenceEditorKind.Employee)
            {
                return BuildEmployeeAuditDescription(row);
            }

            var name =
                row.GetValue("name")?.ToString()
                ?? row.GetValue("title")?.ToString()
                ?? row.GetValue("full_name")?.ToString()
                ?? row.GetValue("display_name")?.ToString()
                ?? definition.EffectiveNavigationDescription;
            var id = row.GetValue("id")?.ToString();

            return string.IsNullOrWhiteSpace(id)
                ? name
                : $"{name} (ID: {id})";
        }

        private static long? TryGetSelectedRowId(ReferenceDataRow row)
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

        private sealed record AuditScope(
            string Key,
            string Title,
            string Description,
            Dictionary<string, object?> Filters);

        private void RefreshItemsSnapshot()
        {
            if (_rows is null)
            {
                _itemsSnapshot = [];
                return;
            }

            if (_rows.Items is IList<ReferenceDataRow> list)
            {
                _itemsSnapshot = new ReadOnlyCollection<ReferenceDataRow>(list);
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
                || message.StartsWith("STEP API ", StringComparison.Ordinal)
                || message.StartsWith("STEP VM ", StringComparison.Ordinal)
                || message.StartsWith("FILTER ", StringComparison.Ordinal)
                || message.StartsWith("VIEWMODEL LOAD STATE NULL", StringComparison.Ordinal)
                || message.StartsWith("VIEWMODEL RETENTION STATE NULL", StringComparison.Ordinal);
        }

        private string BuildSelectedRowInfoMessage()
        {
            if (!HasSelectedRow || SelectedRow is null)
            {
                return string.Empty;
            }

            var id = SelectedRow.GetValue("id")?.ToString();
            var primaryText =
                SelectedRow.GetValue("name")?.ToString()
                ?? SelectedRow.GetValue("full_name")?.ToString()
                ?? SelectedRow.GetValue("description")?.ToString()
                ?? ContentTitle;

            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(primaryText))
            {
                return $"{TruncateSelectedRowText(primaryText, 30)} (ID: {id})";
            }

            if (!string.IsNullOrWhiteSpace(primaryText))
            {
                return TruncateSelectedRowText(primaryText, 30);
            }

            return !string.IsNullOrWhiteSpace(id)
                ? $"ID: {id}"
                : "Запись выбрана";
        }

        private static string TruncateSelectedRowText(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value;
            }

            return $"{value[..maxLength].TrimEnd()}...";
        }
    }
}
