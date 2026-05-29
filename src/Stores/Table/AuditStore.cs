// Owns audit timeline state and loading for the currently active table page context.
using System.Globalization;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Shell;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Shell;
using CbsContractsDesktopClient.Services.Workspace;
using CbsContractsDesktopClient.ViewModels.Shell;

namespace CbsContractsDesktopClient.Stores.Table
{
    public sealed class AuditStore
    {
        private const int AuditPageSize = 20;
        private readonly AppShellViewModel _shellViewModel;
        private readonly IDataQueryService _dataQueryService;
        private CancellationTokenSource? _auditCts;
        private string _lastAuditPanelKey = string.Empty;
        private List<AuditRecord> _auditRecords = [];
        private int _auditOffset;
        private bool _hasPreviousAuditRecords;
        private bool _hasNextAuditRecords = true;
        private bool _isAuditLoading;
        private DateTimeOffset? _auditFromDate;
        private DateTimeOffset? _auditToDate;
        private IReadOnlyList<string> _auditActions = [];
        private bool _hasActiveTable;
        private TablePageDefinition? _tablePage;
        private ReferenceDefinition? _reference;
        private TableDataRow? _selectedRow;

        public AuditStore(AppShellViewModel shellViewModel, IDataQueryService dataQueryService)
        {
            _shellViewModel = shellViewModel;
            _dataQueryService = dataQueryService;
        }

        public void UpdateContext(
            bool hasActiveTable,
            TablePageDefinition? tablePage,
            ReferenceDefinition? reference,
            TableDataRow? selectedRow)
        {
            var keyBefore = BuildAuditScope()?.Key;
            _hasActiveTable = hasActiveTable;
            _tablePage = tablePage;
            _reference = reference;
            _selectedRow = selectedRow;
            var keyAfter = BuildAuditScope()?.Key;

            if (!string.Equals(keyBefore, keyAfter, StringComparison.Ordinal))
            {
                _auditCts?.Cancel();
                ResetPagingState();
            }
        }

        public void Reset()
        {
            _auditCts?.Cancel();
            _hasActiveTable = false;
            _tablePage = null;
            _reference = null;
            _selectedRow = null;
            ResetPagingState();
            _shellViewModel.ResetAuditPanelState();
        }

        public async Task RefreshAsync(bool force = false)
        {
            if (!_shellViewModel.IsAuditPanelOpen)
            {
                return;
            }

            if (_selectedRow is not null && TryGetSelectedRowId(_selectedRow) is null)
            {
                _auditCts?.Cancel();
                ResetPagingState();
                _shellViewModel.SetAuditPanelState(new AuditPanelState
                {
                    Title = "Аудит изменений",
                    Description = _tablePage is null
                        ? "Выбранная запись"
                        : BuildSelectedRecordAuditDescription(_tablePage, _selectedRow),
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
                ResetPagingState();
                _shellViewModel.SetAuditPanelState(new AuditPanelState
                {
                    Title = "Аудит изменений",
                    Description = "Выберите таблицу, чтобы увидеть последние события.",
                    Entries =
                    [
                        BuildAuditPanelMessageEntry(
                            "Таблица не выбрана",
                            "Последние события появятся после выбора активной таблицы.")
                    ]
                });
                _shellViewModel.SetAuditPanelText("Таблица не выбрана.");
                return;
            }

            if (!force && string.Equals(_lastAuditPanelKey, scope.Key, StringComparison.Ordinal))
            {
                return;
            }

            _auditCts?.Cancel();
            ResetPagingState();
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

            await LoadPageAsync(scope.Key, offset: 0);
        }

        public async Task<bool> ShiftWindowAsync(int direction)
        {
            if (!_shellViewModel.IsAuditPanelOpen
                || _isAuditLoading
                || string.IsNullOrWhiteSpace(_lastAuditPanelKey))
            {
                return false;
            }

            if (direction > 0)
            {
                return _hasNextAuditRecords
                    && await LoadPageAsync(_lastAuditPanelKey, _auditOffset + AuditPageSize);
            }

            if (direction < 0)
            {
                return _hasPreviousAuditRecords
                    && await LoadPageAsync(_lastAuditPanelKey, Math.Max(0, _auditOffset - AuditPageSize));
            }

            return false;
        }

        public async Task SetDateRangeAsync(DateTimeOffset? fromDate, DateTimeOffset? toDate)
        {
            var normalizedFrom = fromDate?.Date;
            var normalizedTo = toDate?.Date;

            if (normalizedFrom is not null
                && normalizedTo is not null
                && normalizedFrom > normalizedTo)
            {
                (normalizedFrom, normalizedTo) = (normalizedTo, normalizedFrom);
            }

            if (_auditFromDate == normalizedFrom && _auditToDate == normalizedTo)
            {
                return;
            }

            _auditFromDate = normalizedFrom;
            _auditToDate = normalizedTo;
            await RefreshAsync(force: true);
        }

        public async Task SetActionFilterAsync(IReadOnlyList<string> actions)
        {
            var normalizedActions = actions
                .Select(AuditPanelFormatter.NormalizeAction)
                .Where(static action => !string.IsNullOrWhiteSpace(action))
                .Distinct()
                .Order()
                .ToList();

            if (_auditActions.SequenceEqual(normalizedActions))
            {
                return;
            }

            _auditActions = normalizedActions;
            await RefreshAsync(force: true);
        }

        private async Task<bool> LoadPageAsync(string auditKey, int offset)
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

        private void ResetPagingState()
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
            if (!_hasActiveTable || _tablePage is null)
            {
                return null;
            }

            var model = _tablePage.AuditModel;
            var filters = new Dictionary<string, object?>
            {
                ["auditable_type__eq"] = model
            };
            ApplyDateRangeFilters(filters);
            ApplyActionFilters(filters);
            var filterKey = BuildAuditFilterKey();

            if (_selectedRow is not null)
            {
                var selectedId = TryGetSelectedRowId(_selectedRow);
                if (selectedId is null)
                {
                    return null;
                }

                filters["auditable_id__eq"] = selectedId.Value;
                return new AuditScope(
                    $"record:{model}:{selectedId.Value}:{filterKey}",
                    "Аудит изменений",
                    BuildSelectedRecordAuditDescription(_tablePage, _selectedRow),
                    filters);
            }

            return new AuditScope(
                $"table:{model}:{filterKey}",
                "Последние события аудита",
                $"Активная таблица: {_tablePage.EffectiveNavigationDescription}",
                filters);
        }

        private void ApplyDateRangeFilters(Dictionary<string, object?> filters)
        {
            if (_auditFromDate is DateTimeOffset fromDate)
            {
                filters["created_at__gte"] = fromDate
                    .Date
                    .ToString("yyyy-MM-dd'T'00:00:00", CultureInfo.InvariantCulture);
            }

            if (_auditToDate is DateTimeOffset toDate)
            {
                filters["created_at__lte"] = toDate
                    .Date
                    .ToString("yyyy-MM-dd'T'23:59:59", CultureInfo.InvariantCulture);
            }
        }

        private void ApplyActionFilters(Dictionary<string, object?> filters)
        {
            if (_auditActions.Count > 0)
            {
                filters["action__in"] = _auditActions
                    .Select(AuditPanelFormatter.GetActionFilterValue)
                    .Where(static action => action is not null)
                    .Select(static action => action!.Value)
                    .ToList();
            }
        }

        private string BuildAuditFilterKey()
        {
            var from = _auditFromDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "any";
            var to = _auditToDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "any";
            var actions = _auditActions.Count == 0
                ? "any"
                : string.Join(",", _auditActions);
            return $"{from}..{to}:{actions}";
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
                Title = AuditPanelFormatter.GetActionTitle(audit.Action),
                Description = BuildAuditRecordText(audit),
                BackgroundBrushKey = AuditPanelFormatter.GetActionBrushKey(audit.Action)
            };
        }

        private static string BuildAuditPanelText(IReadOnlyList<AuditRecord> audits)
        {
            return audits.Count == 0
                ? "Событий не найдено."
                : string.Join(
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
                BackgroundBrushKey = "ShellMutedPanelBackgroundBrush",
                IsCopyEnabled = title.Contains("ошиб", StringComparison.OrdinalIgnoreCase)
                    || title.Contains("не удалось", StringComparison.OrdinalIgnoreCase)
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

        private static string BuildSelectedRecordAuditDescription(TablePageDefinition definition, TableDataRow row)
        {
            var name =
                row.GetValue("name")?.ToString()
                ?? row.GetValue("contract.name")?.ToString()
                ?? row.GetValue("title")?.ToString()
                ?? row.GetValue("full_name")?.ToString()
                ?? row.GetValue("display_name")?.ToString()
                ?? definition.EffectiveNavigationDescription;
            var id = row.GetValue("id")?.ToString();

            return string.IsNullOrWhiteSpace(id)
                ? name
                : $"{name} (ID: {id})";
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

        private sealed record AuditScope(
            string Key,
            string Title,
            string Description,
            Dictionary<string, object?> Filters);
    }
}
