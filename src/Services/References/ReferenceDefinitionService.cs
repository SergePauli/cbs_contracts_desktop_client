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
                definition = ApplySavedSettings(storedDefinition.Clone());
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
                }
                else
                {
                    tableSettings.Columns[settings.FieldKey] = new LocalTableColumnSettings
                    {
                        Width = settings.Width
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

        public async Task SaveSortAsync(
            ReferenceTableSortSettings settings,
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
                if (!localSettings.Tables.TryGetValue(settings.Route, out var tableSettings))
                {
                    tableSettings = new LocalTableSettings();
                    localSettings.Tables[settings.Route] = tableSettings;
                }

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
                    route: "/users",
                    model: "Profile",
                    title: "Пользователи",
                    navigationDescription: "Профили пользователей",
                    preset: "edit",
                    editorKind: ReferenceEditorKind.Profile,
                    fields:
                    [
                        new ReferenceFieldDefinition
                        {
                            FieldKey = "id",
                            Label = "ID",
                            ApiField = "id",
                            EditorType = ReferenceFieldEditorType.Number,
                            IsRequired = true,
                            IsReadOnlyOnCreate = true,
                            IsReadOnlyOnEdit = true
                        },
                        new ReferenceFieldDefinition
                        {
                            FieldKey = "name",
                            Label = "Логин",
                            ApiField = "user.name",
                            EditorType = ReferenceFieldEditorType.Text,
                            IsRequired = true
                        },
                        new ReferenceFieldDefinition
                        {
                            FieldKey = "email",
                            Label = "Email",
                            ApiField = "user.person.person_contacts.contact.value",
                            EditorType = ReferenceFieldEditorType.Text
                        },
                        new ReferenceFieldDefinition
                        {
                            FieldKey = "person",
                            Label = "ФИО",
                            ApiField = "user.person.person_name.naming.fio",
                            EditorType = ReferenceFieldEditorType.Text,
                            IsReadOnlyOnCreate = true,
                            IsReadOnlyOnEdit = true
                        },
                        new ReferenceFieldDefinition
                        {
                            FieldKey = "role",
                            Label = "Роль",
                            ApiField = "user.role",
                            EditorType = ReferenceFieldEditorType.Text
                        },
                        new ReferenceFieldDefinition
                        {
                            FieldKey = "position",
                            Label = "Должность",
                            ApiField = "position.name",
                            EditorType = ReferenceFieldEditorType.Text
                        },
                        new ReferenceFieldDefinition
                        {
                            FieldKey = "department",
                            Label = "Отдел",
                            ApiField = "department.name",
                            EditorType = ReferenceFieldEditorType.Text
                        },
                        new ReferenceFieldDefinition
                        {
                            FieldKey = "used",
                            Label = "Активен",
                            ApiField = "user.activated",
                            EditorType = ReferenceFieldEditorType.Boolean
                        },
                        new ReferenceFieldDefinition
                        {
                            FieldKey = "last_login",
                            Label = "Последний вход",
                            ApiField = "user.last_login",
                            EditorType = ReferenceFieldEditorType.Text,
                            IsReadOnlyOnCreate = true,
                            IsReadOnlyOnEdit = true
                        }
                    ],
                    columns:
                    [
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "id",
                            Header = "ID",
                            ApiField = "id",
                            DisplayField = "id",
                            FilterField = "id",
                            SortField = "id",
                            DefaultWidth = "5rem",
                            Alignment = CbsTableColumnAlignment.Right,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                Mode = DataFilterMode.Numeric,
                                MatchMode = DataFilterMatchMode.Equals,
                                PlaceholderText = "\u2315"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "name",
                            Header = "Логин",
                            DisplayField = "user.name",
                            FilterField = "user.name",
                            SortField = "user.name",
                            DefaultWidth = "10rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                Mode = DataFilterMode.Text,
                                MatchMode = DataFilterMatchMode.Contains,
                                PlaceholderText = "\u2315"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "email",
                            Header = "Email",
                            DisplayField = "user.email.name",
                            FilterField = "user.person.person_contacts.contact.value",
                            SortField = "user.person.person_contacts.contact.value",
                            DefaultWidth = "14rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                Mode = DataFilterMode.Text,
                                MatchMode = DataFilterMatchMode.Contains,
                                PlaceholderText = "\u2315"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "person",
                            Header = "ФИО",
                            DisplayField = "user.person.full_name",
                            FilterField = "user.person.person_name.naming.fio",
                            SortField = "user.person.person_name.naming.surname",
                            DefaultWidth = "12rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                Mode = DataFilterMode.Text,
                                MatchMode = DataFilterMatchMode.Contains,
                                PlaceholderText = "\u2315"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "role",
                            Header = "Роль",
                            DisplayField = "user.role",
                            DefaultWidth = "10rem",
                            Alignment = CbsTableColumnAlignment.Left
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "position",
                            Header = "Должность",
                            DisplayField = "position.name",
                            FilterField = "position.name",
                            SortField = "position",
                            DefaultWidth = "12rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                Mode = DataFilterMode.Text,
                                MatchMode = DataFilterMatchMode.Contains,
                                PlaceholderText = "\u2315"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "department",
                            Header = "Отдел",
                            DisplayField = "department.name",
                            FilterField = "department_id",
                            SortField = "department.name",
                            DefaultWidth = "12rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                EditorKind = CbsTableFilterEditorKind.MultiSelect,
                                Mode = DataFilterMode.Text,
                                MatchMode = DataFilterMatchMode.In,
                                OptionsSourceKey = "Department",
                                EmptySelectionText = "Все"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "last_login",
                            Header = "Входил",
                            DisplayField = "user.last_login",
                            FilterField = "user.last_login",
                            SortField = "last_login",
                            DefaultWidth = "10rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                EditorKind = CbsTableFilterEditorKind.Text,
                                Mode = DataFilterMode.DateTime,
                                MatchMode = DataFilterMatchMode.GreaterThanOrEqual,
                                PlaceholderText = "\u2315"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "used",
                            Header = "Акт-ан",
                            DisplayField = "user.activated",
                            FilterField = "user.activated",
                            SortField = "used",
                            DefaultWidth = "3rem",
                            Alignment = CbsTableColumnAlignment.Center,
                            BodyMode = CbsTableBodyMode.BooleanIcon,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                EditorKind = CbsTableFilterEditorKind.Boolean,
                                Mode = DataFilterMode.Text,
                                MatchMode = DataFilterMatchMode.Equals
                            }
                        }
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
            string preset = "item",
            string? navigationDescription = null,
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
            var settings = GetOrLoadSettings();
            if (!settings.Tables.TryGetValue(definition.Route, out var tableSettings))
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

        private static void RemoveTableIfEmpty(
            LocalUserSettings localSettings,
            string route,
            LocalTableSettings tableSettings)
        {
            if (tableSettings.Columns.Count == 0 && tableSettings.Sort is null)
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
    }
}
