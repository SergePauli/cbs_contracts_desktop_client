using System.Threading;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Settings;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.Settings;

namespace CbsContractsDesktopClient.Services.References
{
    public class ReferenceDefinitionService : IReferenceDefinitionService
    {
        private readonly IReadOnlyDictionary<string, ReferenceDefinition> _definitions;
        private readonly ILocalUserSettingsService _localUserSettingsService;
        private readonly SemaphoreSlim _settingsGate = new(1, 1);
        private LocalUserSettings? _cachedSettings;

        public ReferenceDefinitionService(ILocalUserSettingsService localUserSettingsService)
        {
            _localUserSettingsService = localUserSettingsService;
            _definitions = BuildDefinitions()
                .ToDictionary(static item => item.Route, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetByRoute(string? route, out ReferenceDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(route) && _definitions.TryGetValue(route, out var storedDefinition))
            {
                definition = storedDefinition.Clone();
                ApplySavedWidths(definition);
                return true;
            }

            definition = null!;
            return false;
        }

        public async Task SaveColumnWidthAsync(
            ReferenceTableColumnWidthSettings settings,
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
                if (!localSettings.Tables.TryGetValue(settings.Route, out var tableSettings))
                {
                    tableSettings = new LocalTableSettings();
                    localSettings.Tables[settings.Route] = tableSettings;
                }

                if (string.IsNullOrWhiteSpace(settings.Width))
                {
                    tableSettings.Columns.Remove(settings.FieldKey);
                    if (tableSettings.Columns.Count == 0)
                    {
                        localSettings.Tables.Remove(settings.Route);
                    }
                }
                else
                {
                    tableSettings.Columns[settings.FieldKey] = new LocalTableColumnSettings
                    {
                        Width = settings.Width
                    };
                }

                await _localUserSettingsService.SaveAsync(localSettings, cancellationToken);
            }
            finally
            {
                _settingsGate.Release();
            }
        }

        private static IReadOnlyList<ReferenceDefinition> BuildDefinitions()
        {
            return
            [
                CreateReferenceDefinition(
                    route: "/references/Area",
                    model: "Area",
                    title: "Регионы",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true)
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/Position",
                    model: "Position",
                    title: "Должности",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true)
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/Ownership",
                    model: "Ownership",
                    title: "Формы орг.",
                    preset: "card",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true),
                        CreateTextField("okopf", "ОКОПФ"),
                        CreateTextField("full_name", "Полное наименование")
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("okopf", "ОКОПФ"),
                        CreateTextColumn("full_name", "Полное наименование")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/Department",
                    model: "Department",
                    title: "Отделы",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true)
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/TaskKind",
                    model: "TaskKind",
                    title: "Работы",
                    preset: "card",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateNumberField("code", "Код", isRequired: true),
                        CreateTextField("name", "Наименование", isRequired: true),
                        CreateTextField("description", "Описание"),
                        CreateNumberField("cost", "Сумма"),
                        CreateTextField("duration", "Срок")
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateNumberColumn("code", "Код"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("description", "Описание"),
                        CreateNumberColumn("cost", "Сумма"),
                        CreateTextColumn("duration", "Срок")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/Status",
                    model: "Status",
                    title: "Статусы",
                    preset: "card",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true),
                        CreateNumberField("order", "Порядок", isRequired: true),
                        CreateTextField("description", "Описание")
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateNumberColumn("order", "Порядок"),
                        CreateTextColumn("description", "Описание")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/OrderStatus",
                    model: "OrderStatus",
                    title: "Статусы доставки",
                    preset: "edit",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true),
                        CreateNumberField("order", "Порядок", isRequired: true),
                        CreateTextField("description", "Описание")
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateNumberColumn("order", "Порядок"),
                        CreateTextColumn("description", "Описание")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/IsecurityTool",
                    model: "IsecurityTool",
                    title: "СЗИ",
                    preset: "edit",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true),
                        CreateTextField("unit", "Ед."),
                        CreateNumberField("priority", "Приоритет"),
                        CreateBooleanField("used", "Исп.")
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("unit", "Ед."),
                        CreateNumberColumn("priority", "Приоритет"),
                        CreateBooleanColumn("used", "Исп.")
                    ])
            ];
        }

        private static ReferenceDefinition CreateReferenceDefinition(
            string route,
            string model,
            string title,
            IReadOnlyList<ReferenceFieldDefinition> fields,
            IReadOnlyList<CbsTableColumnDefinition> columns,
            string preset = "item")
        {
            return new ReferenceDefinition
            {
                Route = route,
                Model = model,
                Title = title,
                Preset = preset,
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
                Filter = new CbsTableColumnFilterDefinition()
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

        private void ApplySavedWidths(ReferenceDefinition definition)
        {
            var settings = GetOrLoadSettings();
            if (!settings.Tables.TryGetValue(definition.Route, out var tableSettings))
            {
                return;
            }

            foreach (var column in definition.Columns)
            {
                if (tableSettings.Columns.TryGetValue(column.FieldKey, out var columnSettings)
                    && !string.IsNullOrWhiteSpace(columnSettings.Width))
                {
                    column.Width = columnSettings.Width;
                }
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
    }
}
