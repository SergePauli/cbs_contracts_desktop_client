// Contains the reference definition registry and local table settings persistence.
// Concrete reference metadata is split across ReferenceDefinitionService.*Definition.cs partial files.
using System.Threading;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.Settings;

namespace CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions
{
    public partial class ReferenceDefinitionService : IReferenceDefinitionService
    {
        private readonly IReadOnlyDictionary<string, ReferenceDefinition> _definitions;
        private readonly ITableSettingsService _tableSettingsService;

        public ReferenceDefinitionService(ITableSettingsService tableSettingsService)
        {
            _tableSettingsService = tableSettingsService;
            _definitions = BuildDefinitions()
                .ToDictionary(static item => item.Route, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetByRoute(string? route, out ReferenceDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(route) && _definitions.TryGetValue(route, out var storedDefinition))
            {
                definition = ApplySavedSettings(storedDefinition.Clone());
                return true;
            }

            definition = null!;
            return false;
        }

        private static IReadOnlyList<ReferenceDefinition> BuildDefinitions()
        {
            return
            [
                ..BuildSimpleReferenceDefinitions(),
                BuildHolidayReferenceDefinition(),
                BuildEmployeeReferenceDefinition(),
                BuildContragentReferenceDefinition(),
                BuildProfileReferenceDefinition()
            ];
        }

        private static ReferenceDefinition CreateReferenceDefinition(
            string route,
            string model,
            string title,
            IReadOnlyList<ReferenceFieldDefinition> fields,
            IReadOnlyList<CbsTableColumnDefinition> columns,
            string preset = "item",
            string? navigationDescription = null,
            string? initialSortField = null,
            DataSortDirection? initialSortDirection = null,
            ReferenceEditorKind editorKind = ReferenceEditorKind.Generic,
            bool isAuditEnabled = false)
        {
            return new ReferenceDefinition
            {
                Route = route,
                Model = model,
                Title = title,
                NavigationDescription = navigationDescription,
                Preset = preset,
                InitialSortField = initialSortField,
                InitialSortDirection = initialSortDirection,
                EditorKind = editorKind,
                IsAuditEnabled = isAuditEnabled,
                Fields = fields,
                Columns = columns
            };
        }

        private static CbsTableColumnDefinition CreateTextColumn(string key, string header, string? width = null)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = key,
                Header = header,
                ApiField = key,
                DefaultWidth = width ?? GetDefaultTextWidth(key),
                Alignment = CbsTableColumnAlignment.Left,
                IsFilterable = true,
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    EditorKind = CbsTableFilterEditorKind.Text,
                    Mode = DataFilterMode.Text,
                    MatchMode = DataFilterMatchMode.Contains,
                    PlaceholderText = "\u2315"
                }
            };
        }

        private static ReferenceFieldDefinition CreateTextField(
            string key,
            string label,
            bool isRequired = false,
            bool isReadOnlyOnCreate = false,
            bool isReadOnlyOnEdit = false)
        {
            return new ReferenceFieldDefinition
            {
                FieldKey = key,
                Label = label,
                ApiField = key,
                EditorType = ReferenceFieldEditorType.Text,
                IsRequired = isRequired,
                IsReadOnlyOnCreate = isReadOnlyOnCreate,
                IsReadOnlyOnEdit = isReadOnlyOnEdit
            };
        }

        private static CbsTableColumnDefinition CreateNumberColumn(string key, string header, string? width = null)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = key,
                Header = header,
                ApiField = key,
                DefaultWidth = width ?? GetDefaultNumberWidth(key),
                Alignment = CbsTableColumnAlignment.Right,
                IsFilterable = true,
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    EditorKind = CbsTableFilterEditorKind.Numeric,
                    Mode = DataFilterMode.Numeric,
                    MatchMode = DataFilterMatchMode.Equals,
                    PlaceholderText = "\u2315"
                }
            };
        }

        private static ReferenceFieldDefinition CreateNumberField(
            string key,
            string label,
            bool isRequired = false,
            bool isReadOnlyOnCreate = false,
            bool isReadOnlyOnEdit = false)
        {
            return new ReferenceFieldDefinition
            {
                FieldKey = key,
                Label = label,
                ApiField = key,
                EditorType = ReferenceFieldEditorType.Number,
                IsRequired = isRequired,
                IsReadOnlyOnCreate = isReadOnlyOnCreate,
                IsReadOnlyOnEdit = isReadOnlyOnEdit
            };
        }

        private static CbsTableColumnDefinition CreateDateColumn(
            string key,
            string header,
            string? width = null,
            DataFilterMatchMode matchMode = DataFilterMatchMode.GreaterThanOrEqual)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = key,
                Header = header,
                ApiField = key,
                SortField = key,
                DefaultWidth = width ?? "10rem",
                Alignment = CbsTableColumnAlignment.Left,
                IsFilterable = true,
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    EditorKind = CbsTableFilterEditorKind.Text,
                    Mode = DataFilterMode.Date,
                    MatchMode = matchMode,
                    PlaceholderText = "\u2315"
                }
            };
        }

        private static ReferenceFieldDefinition CreateDateField(
            string key,
            string label,
            bool isRequired = false,
            bool isReadOnlyOnCreate = false,
            bool isReadOnlyOnEdit = false)
        {
            return new ReferenceFieldDefinition
            {
                FieldKey = key,
                Label = label,
                ApiField = key,
                EditorType = ReferenceFieldEditorType.Date,
                IsRequired = isRequired,
                IsReadOnlyOnCreate = isReadOnlyOnCreate,
                IsReadOnlyOnEdit = isReadOnlyOnEdit
            };
        }

        private static CbsTableColumnDefinition CreateBooleanColumn(string key, string header, string? width = null)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = key,
                Header = header,
                ApiField = key,
                DefaultWidth = width ?? "3rem",
                Alignment = CbsTableColumnAlignment.Center,
                BodyMode = CbsTableBodyMode.BooleanIcon,
                Filter = new CbsTableColumnFilterDefinition
                {
                    EditorKind = CbsTableFilterEditorKind.Boolean,
                    MatchMode = DataFilterMatchMode.Equals
                }
            };
        }

        private static ReferenceFieldDefinition CreateBooleanField(
            string key,
            string label,
            bool isRequired = false,
            bool isReadOnlyOnCreate = false,
            bool isReadOnlyOnEdit = false)
        {
            return new ReferenceFieldDefinition
            {
                FieldKey = key,
                Label = label,
                ApiField = key,
                EditorType = ReferenceFieldEditorType.Boolean,
                IsRequired = isRequired,
                IsReadOnlyOnCreate = isReadOnlyOnCreate,
                IsReadOnlyOnEdit = isReadOnlyOnEdit
            };
        }

        private static string GetDefaultTextWidth(string key)
        {
            return key switch
            {
                "okopf" => "5rem",
                "unit" => "3rem",
                "duration" => "7rem",
                _ => "16rem"
            };
        }

        private static string GetDefaultNumberWidth(string key)
        {
            return key switch
            {
                "id" => "5rem",
                "code" => "3rem",
                "order" => "3rem",
                "priority" => "7rem",
                "cost" => "7rem",
                _ => "6rem"
            };
        }

        private ReferenceDefinition ApplySavedSettings(ReferenceDefinition definition)
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
            }

            if (!string.IsNullOrWhiteSpace(tableSettings.Sort?.FieldKey)
                && Enum.TryParse<DataSortDirection>(tableSettings.Sort.Direction, ignoreCase: true, out var direction))
            {
                return ApplySavedSort(definition, tableSettings.Sort.FieldKey, direction);
            }

            return definition;
        }

        private static ReferenceDefinition ApplySavedSort(
            ReferenceDefinition definition,
            string fieldKey,
            DataSortDirection direction)
        {
            return new ReferenceDefinition
            {
                Route = definition.Route,
                Model = definition.Model,
                Title = definition.Title,
                NavigationDescription = definition.NavigationDescription,
                Preset = definition.Preset,
                Summary = definition.Summary,
                EditorKind = definition.EditorKind,
                IsAuditEnabled = definition.IsAuditEnabled,
                InitialSortField = fieldKey,
                InitialSortDirection = direction,
                Fields = definition.Fields,
                Columns = definition.Columns
            };
        }

    }
}
