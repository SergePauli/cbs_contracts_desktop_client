using System.Threading;
using System.Text.Json;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Settings;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Settings;

namespace CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions
{
    public sealed partial class TablePageDefinitionService : ITablePageDefinitionService
    {
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly ITableSettingsService _tableSettingsService;
        private readonly IReadOnlyDictionary<string, TablePageDefinition> _definitions;

        public TablePageDefinitionService(
            IReferenceDefinitionService referenceDefinitionService,
            ITableSettingsService tableSettingsService)
        {
            _referenceDefinitionService = referenceDefinitionService;
            _tableSettingsService = tableSettingsService;
            _definitions = BuildDefinitions()
                .ToDictionary(static definition => definition.Route, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetByRoute(string? route, out TablePageDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                definition = null!;
                return false;
            }

            if (_definitions.TryGetValue(route, out var storedDefinition))
            {
                definition = ApplySavedSettings(storedDefinition.Clone());
                return true;
            }

            if (_referenceDefinitionService.TryGetByRoute(route, out var referenceDefinition))
            {
                definition = ApplySavedSettings(referenceDefinition.ToTablePageDefinition());
                return true;
            }

            definition = null!;
            return false;
        }

        public async Task SaveColumnWidthAsync(
            TableColumnWidthSettings settings,
            CancellationToken cancellationToken = default)
        {
            await _tableSettingsService.SaveColumnWidthAsync(settings, cancellationToken);
        }

        public async Task SaveSortAsync(
            TableSortSettings settings,
            CancellationToken cancellationToken = default)
        {
            await _tableSettingsService.SaveSortAsync(settings, cancellationToken);
        }

        public async Task SaveColumnLayoutAsync(
            TableColumnLayoutSettings settings,
            CancellationToken cancellationToken = default)
        {
            await _tableSettingsService.SaveColumnLayoutAsync(settings, cancellationToken);
        }

        public async Task SaveFiltersAsync(
            string route,
            IReadOnlyList<DataFilterCriterion> filters,
            CancellationToken cancellationToken = default)
        {
            await _tableSettingsService.SaveFiltersAsync(route, filters, cancellationToken);
        }

        private static IReadOnlyList<TablePageDefinition> BuildDefinitions()
        {
            return
            [
                BuildContractsDefinition(),
                BuildRevisionsDefinition(),
                BuildStagesDefinition()
            ];
        }
        private TablePageDefinition ApplySavedSettings(TablePageDefinition definition)
        {
            var tableSettings = _tableSettingsService.GetTableSettings(definition.Route);
            if (tableSettings is null)
            {
                return definition;
            }

            foreach (var column in definition.Columns)
            {
                if (tableSettings.Columns.TryGetValue(column.FieldKey, out var columnSettings)
                    && !string.IsNullOrWhiteSpace(columnSettings.Width))
                {
                    column.Width = columnSettings.Width;
                }

                if (tableSettings.Columns.TryGetValue(column.FieldKey, out columnSettings)
                    && columnSettings.IsVisible.HasValue
                    && !column.IsImmutable)
                {
                    column.IsVisible = columnSettings.IsVisible.Value;
                }
            }

            if (tableSettings.ColumnOrder.Count > 0)
            {
                definition = ApplySavedColumnOrder(definition, tableSettings.ColumnOrder);
            }

            if (definition.Capabilities.HasFlag(TablePageCapabilities.PersistFilters)
                && tableSettings.Filters.Count > 0)
            {
                definition = ApplySavedFilters(definition, tableSettings.Filters);
            }

            if (!string.IsNullOrWhiteSpace(tableSettings.Sort?.FieldKey)
                && Enum.TryParse<DataSortDirection>(tableSettings.Sort.Direction, ignoreCase: true, out var direction))
            {
                return new TablePageDefinition
                {
                    Route = definition.Route,
                    Model = definition.Model,
                    Title = definition.Title,
                    NavigationDescription = definition.NavigationDescription,
                    Preset = definition.Preset,
                    Summary = definition.Summary,
                    Kind = definition.Kind,
                    Capabilities = definition.Capabilities,
                    InitialSortField = tableSettings.Sort.FieldKey,
                    InitialSortDirection = direction,
                    InitialFilters = definition.InitialFilters,
                    Columns = definition.Columns,
                    RowStyleKey = definition.RowStyleKey
                };
            }

            return definition;
        }

        private static TablePageDefinition ApplySavedFilters(
            TablePageDefinition definition,
            IReadOnlyList<LocalTableFilterSettings> savedFilters)
        {
            var filters = savedFilters
                .Select(TryCreateSavedFilter)
                .Where(static filter => filter is not null)
                .Select(static filter => filter!)
                .ToList();
            if (filters.Count == 0)
            {
                return definition;
            }

            foreach (var column in definition.Columns)
            {
                var filter = filters.FirstOrDefault(item =>
                    string.Equals(item.FieldKey, column.FieldKey, StringComparison.OrdinalIgnoreCase));
                if (filter is null)
                {
                    continue;
                }

                column.Filter.MatchMode = filter.MatchMode;
                column.Filter.Value = filter.Value;
            }

            return new TablePageDefinition
            {
                Route = definition.Route,
                Model = definition.Model,
                Title = definition.Title,
                NavigationDescription = definition.NavigationDescription,
                Preset = definition.Preset,
                Summary = definition.Summary,
                Kind = definition.Kind,
                Capabilities = definition.Capabilities,
                InitialSortField = definition.InitialSortField,
                InitialSortDirection = definition.InitialSortDirection,
                InitialFilters = filters,
                Columns = definition.Columns,
                RowStyleKey = definition.RowStyleKey
            };
        }

        private static DataFilterCriterion? TryCreateSavedFilter(LocalTableFilterSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.FieldKey)
                || !Enum.TryParse<DataFilterMode>(settings.FilterMode, ignoreCase: true, out var filterMode)
                || !Enum.TryParse<DataFilterMatchMode>(settings.MatchMode, ignoreCase: true, out var matchMode))
            {
                return null;
            }

            var value = NormalizeSavedFilterValue(settings.Value);
            if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return new DataFilterCriterion
            {
                FieldKey = settings.FieldKey,
                FilterMode = filterMode,
                MatchMode = matchMode,
                Value = value
            };
        }

        private static object? NormalizeSavedFilterValue(object? value)
        {
            return value switch
            {
                JsonElement element => NormalizeSavedJsonElement(element),
                _ => value
            };
        }

        private static object? NormalizeSavedJsonElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Array => element.EnumerateArray()
                    .Select(NormalizeSavedJsonElement)
                    .ToList(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => element.ToString()
            };
        }

        private static TablePageDefinition ApplySavedColumnOrder(
            TablePageDefinition definition,
            IReadOnlyList<string> savedOrder)
        {
            var columnsByKey = definition.Columns.ToDictionary(
                static column => column.FieldKey,
                StringComparer.OrdinalIgnoreCase);
            var orderedColumns = new List<CbsTableColumnDefinition>();

            foreach (var fieldKey in savedOrder)
            {
                if (columnsByKey.Remove(fieldKey, out var column))
                {
                    orderedColumns.Add(column);
                }
            }

            orderedColumns.AddRange(definition.Columns.Where(column => columnsByKey.ContainsKey(column.FieldKey)));

            return new TablePageDefinition
            {
                Route = definition.Route,
                Model = definition.Model,
                Title = definition.Title,
                NavigationDescription = definition.NavigationDescription,
                Preset = definition.Preset,
                Summary = definition.Summary,
                Kind = definition.Kind,
                Capabilities = definition.Capabilities,
                InitialSortField = definition.InitialSortField,
                InitialSortDirection = definition.InitialSortDirection,
                InitialFilters = definition.InitialFilters,
                Columns = orderedColumns,
                RowStyleKey = definition.RowStyleKey
            };
        }

    }
}
