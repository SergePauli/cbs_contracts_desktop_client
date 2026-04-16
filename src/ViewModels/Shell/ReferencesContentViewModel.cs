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
        private const bool DiagnosticsEnabled = false;
        private const int MaxUiTraceLines = 80;
        private readonly AppShellViewModel _shellViewModel;
        private readonly IDataQueryService _dataQueryService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly SemaphoreSlim _navigationGate = new(1, 1);
        private LazyDataViewState<ReferenceDataRow>? _state;
        private ICbsTableRows<ReferenceDataRow>? _rows;
        private INotifyPropertyChanged? _rowsNotifier;
        private CancellationTokenSource? _navigationCts;
        private IReadOnlyList<ReferenceDataRow> _itemsSnapshot = [];
        private string _lastAuditPanelText = string.Empty;
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

        public ObservableCollection<ReferenceFilterField> FilterFields { get; }


        public string CurrentTableStateKey => CurrentReference?.Route ?? string.Empty;
        public IReadOnlyList<CbsTableColumnDefinition> CurrentColumns => CurrentReference?.Columns ?? [];

        public bool HasFilters => FilterFields.Count > 0;

        public bool ShowPlaceholder => !HasActiveReference;

        public bool HasMoreItems => _rows?.HasMoreItems == true;

        public int LoadedCount => _rows?.LoadedCount ?? 0;

        public int ResidentCount => _rows?.ResidentCount ?? 0;

        public string TotalCountText => $"Записей: {TotalCount}";

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
        }

        partial void OnTotalCountChanged(int value)
        {
            OnPropertyChanged(nameof(TotalCountText));
            OnPropertyChanged(nameof(LoadedCountText));
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
            UpdateAuditPanelText();
        }

        public void RefreshAuditPanelSnapshot()
        {
            UpdateAuditPanelText(force: true);
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

        public async Task ApplyFilterAsync(
            string fieldKey,
            DataFilterMatchMode matchMode,
            string? value,
            CancellationToken cancellationToken = default)
        {
            if (_state is null)
            {
                return;
            }

            var column = CurrentReference?.Columns.FirstOrDefault(
                column => string.Equals(column.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));
            if (column is not null)
            {
                column.Filter.MatchMode = matchMode;
            }

            await _state.SetFilterAsync(fieldKey, matchMode, value, cancellationToken);
            await _state.SetFilterAsync(
                fieldKey,
                column?.Filter.Mode ?? DataFilterMode.Text,
                matchMode,
                value,
                cancellationToken);
        }

        public async Task ResetFiltersAsync(CancellationToken cancellationToken = default)
        {
            foreach (var filterField in FilterFields)
            {
                filterField.Value = string.Empty;
            }

            if (_state is not null)
            {
                await _state.ClearFiltersAsync(cancellationToken);
            }
        }

        public async Task ApplySortAsync(string fieldKey, DataSortDirection direction, CancellationToken cancellationToken = default)
        {
            if (_state is null)
            {
                return;
            }

            await _state.SetSortAsync(fieldKey, direction, cancellationToken);
            CurrentSortField = fieldKey;
            CurrentSortDirection = direction;
        }

        public async Task ClearSortsAsync(CancellationToken cancellationToken = default)
        {
            if (_state is null)
            {
                return;
            }

            await _state.ClearSortsAsync(cancellationToken);
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
                    UpdateAuditPanelText(force: true);
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
                ContentTitle = definition.Title;
                ContentDescription = definition.Description;
                PlaceholderMessage = string.Empty;
                CurrentReference = definition;
                HasActiveReference = true;
                CurrentSortField = "id";
                CurrentSortDirection = DataSortDirection.Ascending;
                UiTraceLog = string.Empty;
                _lastAuditPanelText = string.Empty;

                BuildFilters(definition);

                _state = new LazyDataViewState<ReferenceDataRow>(
                    _dataQueryService,
                    model: definition.Model,
                    preset: definition.Preset,
                    pageSize: 50,
                    fieldMap: definition.Columns.ToDictionary(
                        static column => column.FieldKey,
                        static column => column.ApiField ?? column.FieldKey),
                    placeholderFactory: ReferenceDataRow.CreatePlaceholder,
                    isPlaceholder: static row => row.IsPlaceholder,
                    initialSorts:
                    [
                        new DataSortCriterion
                        {
                            FieldKey = "id",
                            Direction = DataSortDirection.Ascending
                        }
                    ]);

                AppendUiTrace($"STATE CREATED model={definition.Model} {GetDebugStateSnapshot()}");

                AttachRows(_state, new CbsVirtualTableRows<ReferenceDataRow>(_state.Items));
                AppendUiTrace($"NAVIGATE AFTER ATTACH model={definition.Model} {GetDebugStateSnapshot()}");

                OnPropertyChanged(nameof(CurrentColumns));
                OnPropertyChanged(nameof(HasFilters));
                OnPropertyChanged(nameof(Items));
                OnPropertyChanged(nameof(Rows));
                OnPropertyChanged(nameof(UiTraceLog));
                OnPropertyChanged(nameof(HasMoreItems));
                OnPropertyChanged(nameof(LoadedCount));
                OnPropertyChanged(nameof(CurrentTableStateKey));
                OnPropertyChanged(nameof(ResidentCount));
                OnPropertyChanged(nameof(LastCountRequestJson));
                OnPropertyChanged(nameof(LastPageRequestJson));
                OnPropertyChanged(nameof(TraceLog));
                OnPropertyChanged(nameof(CombinedTraceLog));

                await _rows!.InitializeAsync(cancellationToken);
                AppendUiTrace($"NAVIGATE AFTER INITIALIZE model={definition.Model} {GetDebugStateSnapshot()}");
                UpdateStateProperties();
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
            _lastAuditPanelText = string.Empty;
            CurrentSortField = null;
            CurrentSortDirection = null;
            FilterFields.Clear();
            _shellViewModel.ResetAuditPanelState();

            OnPropertyChanged(nameof(CurrentColumns));
            OnPropertyChanged(nameof(HasFilters));
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(Rows));
            OnPropertyChanged(nameof(HasMoreItems));
            OnPropertyChanged(nameof(LoadedCount));
            OnPropertyChanged(nameof(LastCountRequestJson));
            OnPropertyChanged(nameof(CurrentTableStateKey));
            OnPropertyChanged(nameof(LastPageRequestJson));
            OnPropertyChanged(nameof(TraceLog));
            OnPropertyChanged(nameof(UiTraceLog));
            OnPropertyChanged(nameof(CombinedTraceLog));
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
                    Header = column.Header
                });
            }
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
            UpdateAuditPanelText();
        }

        private void UpdateAuditPanelText(bool force = false)
        {
            if (!DiagnosticsEnabled)
            {
                return;
            }

            if (!HasActiveReference)
            {
                return;
            }

            if (!_shellViewModel.IsAuditPanelOpen)
            {
                return;
            }

            const string auditTitle = "Диагностика таблицы";
            const string auditDescription = "Временный диагностический поток жизненного цикла state и lazy-scroll таблицы.";

            if (!string.Equals(_shellViewModel.AuditPanelState.Title, auditTitle, StringComparison.Ordinal)
                || !string.Equals(_shellViewModel.AuditPanelState.Description, auditDescription, StringComparison.Ordinal))
            {
                _shellViewModel.SetAuditPanelState(new AuditPanelState
                {
                    Title = auditTitle,
                    Description = auditDescription,
                    Entries = []
                });
            }

            var auditText =
                $"Loaded: {LoadedCount}/{TotalCount}{Environment.NewLine}" +
                $"Resident: {ResidentCount}/{TotalCount}{Environment.NewLine}{Environment.NewLine}" +
                $"Trace:{Environment.NewLine}{CombinedTraceLog}";

            if (!force && string.Equals(_lastAuditPanelText, auditText, StringComparison.Ordinal))
            {
                return;
            }

            _lastAuditPanelText = auditText;
            _shellViewModel.SetAuditPanelText(auditText);
        }

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
                || message.StartsWith("STATE CREATED", StringComparison.Ordinal)
                || message.StartsWith("PLACEHOLDER APPLY", StringComparison.Ordinal)
                || message.StartsWith("ATTACH ROWS", StringComparison.Ordinal)
                || message.StartsWith("DETACH STATE", StringComparison.Ordinal)
                || message.StartsWith("STEP API ", StringComparison.Ordinal)
                || message.StartsWith("STEP VM ", StringComparison.Ordinal)
                || message.StartsWith("VIEWMODEL LOAD STATE NULL", StringComparison.Ordinal)
                || message.StartsWith("VIEWMODEL RETENTION STATE NULL", StringComparison.Ordinal);
        }
    }
}
