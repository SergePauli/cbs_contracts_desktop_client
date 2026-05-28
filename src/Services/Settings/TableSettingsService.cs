// Contains shared local persistence for table column, sort, layout, and filter settings.
using System.Text.Json;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Settings;

namespace CbsContractsDesktopClient.Services.Settings
{
    public sealed class TableSettingsService : ITableSettingsService
    {
        private readonly ILocalUserSettingsService _localUserSettingsService;
        private readonly SemaphoreSlim _settingsGate = new(1, 1);
        private LocalUserSettings? _cachedSettings;

        public TableSettingsService(ILocalUserSettingsService localUserSettingsService)
        {
            _localUserSettingsService = localUserSettingsService;
        }

        public LocalTableSettings? GetTableSettings(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                return null;
            }

            var settings = GetOrLoadSettings();
            return settings.Tables.TryGetValue(route, out var tableSettings)
                ? tableSettings
                : null;
        }

        public async Task SaveColumnWidthAsync(
            TableColumnWidthSettings settings,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (string.IsNullOrWhiteSpace(settings.Route) || string.IsNullOrWhiteSpace(settings.FieldKey))
            {
                return;
            }

            await _settingsGate.WaitAsync(cancellationToken);

            try
            {
                var localSettings = await GetOrLoadSettingsAsync(cancellationToken);
                var tableSettings = GetOrCreateTableSettings(localSettings, settings.Route);

                if (string.IsNullOrWhiteSpace(settings.Width))
                {
                    if (tableSettings.Columns.TryGetValue(settings.FieldKey, out var columnSettings))
                    {
                        columnSettings.Width = null;
                        if (columnSettings.IsVisible is null)
                        {
                            tableSettings.Columns.Remove(settings.FieldKey);
                        }
                    }
                }
                else
                {
                    var columnSettings = GetOrCreateColumnSettings(tableSettings, settings.FieldKey);
                    columnSettings.Width = settings.Width;
                }

                RemoveTableIfEmpty(localSettings, settings.Route, tableSettings);

                await _localUserSettingsService.SaveAsync(localSettings, cancellationToken);
            }
            finally
            {
                _settingsGate.Release();
            }
        }

        public async Task SaveSortAsync(
            TableSortSettings settings,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (string.IsNullOrWhiteSpace(settings.Route))
            {
                return;
            }

            await _settingsGate.WaitAsync(cancellationToken);

            try
            {
                var localSettings = await GetOrLoadSettingsAsync(cancellationToken);
                var tableSettings = GetOrCreateTableSettings(localSettings, settings.Route);

                if (string.IsNullOrWhiteSpace(settings.FieldKey) || settings.Direction is null)
                {
                    tableSettings.Sort = null;
                }
                else
                {
                    tableSettings.Sort = new LocalTableSortSettings
                    {
                        FieldKey = settings.FieldKey,
                        Direction = settings.Direction.ToString()
                    };
                }

                RemoveTableIfEmpty(localSettings, settings.Route, tableSettings);

                await _localUserSettingsService.SaveAsync(localSettings, cancellationToken);
            }
            finally
            {
                _settingsGate.Release();
            }
        }

        public async Task SaveColumnLayoutAsync(
            TableColumnLayoutSettings settings,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (string.IsNullOrWhiteSpace(settings.Route))
            {
                return;
            }

            await _settingsGate.WaitAsync(cancellationToken);

            try
            {
                var localSettings = await GetOrLoadSettingsAsync(cancellationToken);
                var tableSettings = GetOrCreateTableSettings(localSettings, settings.Route);

                tableSettings.ColumnOrder = settings.OrderedFieldKeys
                    .Where(static key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var visibleKeys = settings.VisibleFieldKeys
                    .Where(static key => !string.IsNullOrWhiteSpace(key))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var fieldKey in tableSettings.ColumnOrder)
                {
                    var columnSettings = GetOrCreateColumnSettings(tableSettings, fieldKey);
                    columnSettings.IsVisible = visibleKeys.Contains(fieldKey);
                }

                RemoveTableIfEmpty(localSettings, settings.Route, tableSettings);

                await _localUserSettingsService.SaveAsync(localSettings, cancellationToken);
            }
            finally
            {
                _settingsGate.Release();
            }
        }

        public async Task SaveFiltersAsync(
            string route,
            IReadOnlyList<DataFilterCriterion> filters,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                return;
            }

            await _settingsGate.WaitAsync(cancellationToken);

            try
            {
                var localSettings = await GetOrLoadSettingsAsync(cancellationToken);
                var tableSettings = GetOrCreateTableSettings(localSettings, route);

                tableSettings.Filters = filters
                    .Where(static filter => !string.IsNullOrWhiteSpace(filter.FieldKey))
                    .Select(static filter => new LocalTableFilterSettings
                    {
                        FieldKey = filter.FieldKey,
                        FilterMode = filter.FilterMode.ToString(),
                        MatchMode = filter.MatchMode.ToString(),
                        Value = NormalizeFilterValueForSettings(filter.Value)
                    })
                    .ToList();

                RemoveTableIfEmpty(localSettings, route, tableSettings);

                await _localUserSettingsService.SaveAsync(localSettings, cancellationToken);
            }
            finally
            {
                _settingsGate.Release();
            }
        }

        private static LocalTableSettings GetOrCreateTableSettings(
            LocalUserSettings localSettings,
            string route)
        {
            if (!localSettings.Tables.TryGetValue(route, out var tableSettings))
            {
                tableSettings = new LocalTableSettings();
                localSettings.Tables[route] = tableSettings;
            }

            return tableSettings;
        }

        private static LocalTableColumnSettings GetOrCreateColumnSettings(
            LocalTableSettings tableSettings,
            string fieldKey)
        {
            if (!tableSettings.Columns.TryGetValue(fieldKey, out var columnSettings))
            {
                columnSettings = new LocalTableColumnSettings();
                tableSettings.Columns[fieldKey] = columnSettings;
            }

            return columnSettings;
        }

        private static void RemoveTableIfEmpty(
            LocalUserSettings localSettings,
            string route,
            LocalTableSettings tableSettings)
        {
            if (tableSettings.Columns.Count == 0
                && tableSettings.ColumnOrder.Count == 0
                && tableSettings.Filters.Count == 0
                && tableSettings.Sort is null)
            {
                localSettings.Tables.Remove(route);
            }
        }

        private LocalUserSettings GetOrLoadSettings()
        {
            if (_cachedSettings is not null)
            {
                return _cachedSettings;
            }

            _settingsGate.Wait();
            try
            {
                _cachedSettings ??= _localUserSettingsService.Get();
                return _cachedSettings;
            }
            finally
            {
                _settingsGate.Release();
            }
        }

        private async Task<LocalUserSettings> GetOrLoadSettingsAsync(CancellationToken cancellationToken)
        {
            _cachedSettings ??= await _localUserSettingsService.GetAsync(cancellationToken);
            return _cachedSettings;
        }

        private static object? NormalizeFilterValueForSettings(object? value)
        {
            return value switch
            {
                null => null,
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O"),
                DateTime dateTime => dateTime.ToString("O"),
                string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => value,
                System.Collections.IEnumerable values when value is not string => values.Cast<object?>()
                    .Select(NormalizeFilterValueForSettings)
                    .ToList(),
                JsonElement element => element,
                _ => value.ToString()
            };
        }
    }
}
