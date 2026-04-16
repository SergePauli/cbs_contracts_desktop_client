using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Settings;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.Settings;
using System.Threading;

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
                new ReferenceDefinition
                {
                    Route = "/references/Area",
                    Model = "Area",
                    Title = "Регионы",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/Position",
                    Model = "Position",
                    Title = "Должности",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/Ownership",
                    Model = "Ownership",
                    Title = "Формы орг.",
                    Preset = "card",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("okopf", "ОКОПФ"),
                        CreateTextColumn("full_name", "Полное наименование")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/Department",
                    Model = "Department",
                    Title = "Отделы",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/TaskKind",
                    Model = "TaskKind",
                    Title = "Работы",
                    Preset = "card",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateNumberColumn("code", "Код"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("description", "Описание"),
                        CreateNumberColumn("cost", "Сумма"),
                        CreateTextColumn("duration", "Срок")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/Status",
                    Model = "Status",
                    Title = "Статусы",
                    Preset = "card",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateNumberColumn("order", "Порядок"),
                        CreateTextColumn("description", "Описание")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/OrderStatus",
                    Model = "OrderStatus",
                    Title = "Статусы доставки",
                    Preset = "edit",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateNumberColumn("order", "Порядок"),
                        CreateTextColumn("description", "Описание")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/IsecurityTool",
                    Model = "IsecurityTool",
                    Title = "СЗИ",
                    Preset = "edit",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("unit", "Ед."),
                        CreateNumberColumn("priority", "Приоритет"),
                        CreateBooleanColumn("used", "Исп.")
                    ]
                }
            ];
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
                    PlaceholderText = "⌕"
                }
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
                    PlaceholderText = "⌕"
                }
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
