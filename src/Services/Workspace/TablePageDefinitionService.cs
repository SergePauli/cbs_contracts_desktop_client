using System.Threading;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Settings;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;

namespace CbsContractsDesktopClient.Services.Workspace
{
    public sealed class TablePageDefinitionService : ITablePageDefinitionService
    {
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly ILocalUserSettingsService _localUserSettingsService;
        private readonly IReadOnlyDictionary<string, TablePageDefinition> _definitions;
        private readonly SemaphoreSlim _settingsGate = new(1, 1);
        private LocalUserSettings? _cachedSettings;

        public TablePageDefinitionService(
            IReferenceDefinitionService referenceDefinitionService,
            ILocalUserSettingsService localUserSettingsService)
        {
            _referenceDefinitionService = referenceDefinitionService;
            _localUserSettingsService = localUserSettingsService;
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
                definition = referenceDefinition.ToTablePageDefinition();
                return true;
            }

            definition = null!;
            return false;
        }

        public async Task SaveColumnWidthAsync(
            ReferenceTableColumnWidthSettings settings,
            CancellationToken cancellationToken = default)
        {
            await _referenceDefinitionService.SaveColumnWidthAsync(settings, cancellationToken);
            _cachedSettings = null;
        }

        public async Task SaveSortAsync(
            ReferenceTableSortSettings settings,
            CancellationToken cancellationToken = default)
        {
            await _referenceDefinitionService.SaveSortAsync(settings, cancellationToken);
            _cachedSettings = null;
        }

        public async Task SaveColumnLayoutAsync(
            ReferenceTableColumnLayoutSettings settings,
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
                var localSettings = await _localUserSettingsService.GetAsync(cancellationToken);
                if (!localSettings.Tables.TryGetValue(settings.Route, out var tableSettings))
                {
                    tableSettings = new LocalTableSettings();
                    localSettings.Tables[settings.Route] = tableSettings;
                }

                tableSettings.ColumnOrder = settings.OrderedFieldKeys
                    .Where(static key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var visibleKeys = settings.VisibleFieldKeys
                    .Where(static key => !string.IsNullOrWhiteSpace(key))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var fieldKey in tableSettings.ColumnOrder)
                {
                    if (!tableSettings.Columns.TryGetValue(fieldKey, out var columnSettings))
                    {
                        columnSettings = new LocalTableColumnSettings();
                        tableSettings.Columns[fieldKey] = columnSettings;
                    }

                    columnSettings.IsVisible = visibleKeys.Contains(fieldKey);
                }

                RemoveTableIfEmpty(localSettings, settings.Route, tableSettings);

                await _localUserSettingsService.SaveAsync(localSettings, cancellationToken);
                _cachedSettings = null;
            }
            finally
            {
                _settingsGate.Release();
            }
        }

        private static IReadOnlyList<TablePageDefinition> BuildDefinitions()
        {
            return
            [
                new TablePageDefinition
                {
                    Route = "/revisions",
                    Model = "Revision",
                    Title = "Дополнительные соглашения",
                    NavigationDescription = "Дополнительные соглашения контрактов",
                    Preset = "list",
                    Kind = TablePageKind.Functional,
                    Capabilities =
                        TablePageCapabilities.RowSelection
                        | TablePageCapabilities.Edit
                        | TablePageCapabilities.ResetFilters
                        | TablePageCapabilities.PersistColumnWidths
                        | TablePageCapabilities.PersistSort
                        | TablePageCapabilities.Audit
                        | TablePageCapabilities.DetailFooter,
                    InitialSortField = "contract.id",
                    InitialSortDirection = DataSortDirection.Descending,
                    InitialFilters =
                    [
                        new DataFilterCriterion
                        {
                            FieldKey = "priority",
                            FilterMode = DataFilterMode.Numeric,
                            MatchMode = DataFilterMatchMode.GreaterThan,
                            Value = 0
                        }
                    ],
                    Columns =
                    [
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "contract.id",
                            Header = "CID",
                            DisplayField = "contract.id",
                            FilterField = "contract_id",
                            SortField = "contract_id",
                            DefaultWidth = "4rem",
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
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "contract.name",
                            Header = "Контракт",
                            DisplayField = "contract.name",
                            FilterField = "contract.name",
                            SortField = "contract.name",
                            DefaultWidth = "8rem",
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
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "priority",
                            Header = "№",
                            DisplayField = "priority",
                            FilterField = "priority",
                            SortField = "priority",
                            DefaultWidth = "4rem",
                            Alignment = CbsTableColumnAlignment.Center,
                            BodyTemplateKey = "RevisionPriority",
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                EditorKind = CbsTableFilterEditorKind.Numeric,
                                Mode = DataFilterMode.Numeric,
                                MatchMode = DataFilterMatchMode.GreaterThan,
                                PlaceholderText = "\u2315"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "contract.signed_at",
                            Header = "Дата",
                            DisplayField = "contract.signed_at",
                            FilterField = "contract.signed_at",
                            SortField = "contract.signed_at",
                            DefaultWidth = "6rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                EditorKind = CbsTableFilterEditorKind.Text,
                                Mode = DataFilterMode.Date,
                                MatchMode = DataFilterMatchMode.Equals,
                                PlaceholderText = "\u2315"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "contract.contragent.name",
                            Header = "Контрагент",
                            DisplayField = "contract.contragent.name",
                            FilterField = "contract.contragent.org.name_or_contract.contragent.org.full_name",
                            SortField = "contract.contragent.org.name",
                            DefaultWidth = "20rem",
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
                        },
                        CreateRevisionBooleanColumn("is_signed", "Подписан", "is_signed"),
                        CreateRevisionBooleanColumn("is_present", "ВНЛ", "is_present"),
                        CreateRevisionBooleanColumn("contract.governmental", "ГК", "contract.governmental")
                    ]
                }
                ,
                BuildStagesDefinition()
            ];
        }

        private static TablePageDefinition BuildStagesDefinition()
        {
            return new TablePageDefinition
            {
                Route = "/stages",
                Model = "Stage",
                Title = "Этапы контрактов",
                NavigationDescription = "Этапы контрактов",
                Preset = "list",
                Kind = TablePageKind.Functional,
                Capabilities =
                    TablePageCapabilities.RowSelection
                    | TablePageCapabilities.Edit
                    | TablePageCapabilities.ResetFilters
                    | TablePageCapabilities.PersistColumnWidths
                    | TablePageCapabilities.PersistSort
                    | TablePageCapabilities.Audit
                    | TablePageCapabilities.ConfigureColumns,
                InitialSortField = "id",
                InitialSortDirection = DataSortDirection.Descending,
                RowStyleKey = CbsTableRowStyleKey.StageDeadline,
                Columns =
                [
                    CreateStageNumberColumn("id", "ID", "id", "4rem", immutable: true),
                    CreateStageTaskColumn(),
                    CreateStageTextColumn("name", "Номер", "name", "name", "name", "5rem", immutable: true),
                    CreateStageDateColumn("start_at", "Старт", "start_at", "start_at", "6rem", immutable: true),
                    CreateStageDateColumn("deadline_at", "Срок", "deadline_at", "deadline_at", "6rem", immutable: true),
                    CreateStageTextColumn("contragent", "Контрагент", "contract.contragent.name", "contract.contragent.org.name_or_contract.contragent.org.full_name", "contract.contragent.org.name", "19rem", immutable: true),
                    CreateStageTextColumn("region", "Регион", "contract.contragent.region.name", "contract.contragent.real_addr.address.area_id", "contract.contragent.real_addr.address.area.name", "10rem", bodyTemplateKey: "StageRegion"),
                    CreateStageNumberColumn("cost", "Сумма", "cost", "7rem", immutable: true),
                    CreateStageStatusColumn(),
                    CreateStageBooleanColumn("is_funded", "БЗ", "is_funded"),
                    CreateStageBooleanColumn("governmental", "ГК", "contract.governmental", "contract.governmental", "contract.governmental"),
                    CreateStageBooleanColumn("is_present", "ВНЛ", "contract.is_present", "contract.revision.is_present", "contract.revision.is_present"),
                    CreateStageTextColumn("external_number", "Внешний №", "contract.external_number", "contract.external_number", "contract.external_number", "10rem"),
                    CreateStageNumberColumn("duration", "Дней", "duration", "5rem", bodyTemplateKey: "StageDuration"),
                    CreateStageDateColumn("prepayment_at", "ПрОпл", "prepayment_at", "prepayment_at", "6rem"),
                    CreateStageBooleanColumn("is_ride_out", "В-зд", "is_ride_out"),
                    CreateStageBooleanColumn("is_sended", "Отпр", "is_sended"),
                    CreateStageDateColumn("completed_at", "ДЗав", "completed_at", "completed_at", "6rem"),
                    CreateStageDateColumn("funded_at", "БЗакр", "funded_at", "funded_at", "6rem"),
                    CreateStageDateColumn("invoice_at", "ДСчета", "invoice_at", "invoice_at", "6rem"),
                    CreateStageDateColumn("payment_deadline_at", "СрокОп", "payment_deadline_at", "payment_deadline_at", "6rem"),
                    CreateStageDateColumn("payment_at", "Оплата", "payment_at", "payment_at", "6rem"),
                    CreateStageBooleanColumn("szi", "СЗИ", "tasks.task_kind_id", "tasks.task_kind_id", "tasks.task_kind_id", bodyTemplateKey: "StageSzi"),
                    CreateStageTextColumn("register", "Реестр", "register", "registry_quarter_or_registry_year", "registry_year", "5rem", bodyTemplateKey: "StageRegister")
                ]
            };
        }

        private static CbsTableColumnDefinition CreateRevisionBooleanColumn(
            string fieldKey,
            string header,
            string apiKey)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = fieldKey,
                Header = header,
                DisplayField = fieldKey,
                FilterField = apiKey,
                SortField = apiKey,
                DefaultWidth = "4rem",
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
            };
        }

        private static CbsTableColumnDefinition CreateStageTextColumn(
            string fieldKey,
            string header,
            string displayField,
            string filterField,
            string sortField,
            string width,
            bool immutable = false,
            string? bodyTemplateKey = null)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = fieldKey,
                Header = header,
                DisplayField = displayField,
                FilterField = filterField,
                SortField = sortField,
                DefaultWidth = width,
                Alignment = CbsTableColumnAlignment.Left,
                IsImmutable = immutable,
                BodyTemplateKey = bodyTemplateKey,
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

        private static CbsTableColumnDefinition CreateStageNumberColumn(
            string fieldKey,
            string header,
            string apiField,
            string width,
            bool immutable = false,
            string? bodyTemplateKey = null)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = fieldKey,
                Header = header,
                DisplayField = apiField,
                FilterField = apiField,
                SortField = apiField,
                DefaultWidth = width,
                Alignment = CbsTableColumnAlignment.Right,
                IsImmutable = immutable,
                BodyTemplateKey = bodyTemplateKey,
                IsFilterable = bodyTemplateKey is not "StageDuration",
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = bodyTemplateKey is not "StageDuration",
                    EditorKind = CbsTableFilterEditorKind.Numeric,
                    Mode = DataFilterMode.Numeric,
                    MatchMode = DataFilterMatchMode.Equals,
                    PlaceholderText = "\u2315"
                }
            };
        }

        private static CbsTableColumnDefinition CreateStageDateColumn(
            string fieldKey,
            string header,
            string displayField,
            string apiField,
            string width,
            bool immutable = false)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = fieldKey,
                Header = header,
                DisplayField = displayField,
                FilterField = apiField,
                SortField = apiField,
                DefaultWidth = width,
                Alignment = CbsTableColumnAlignment.Center,
                IsImmutable = immutable,
                IsFilterable = true,
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    EditorKind = CbsTableFilterEditorKind.Text,
                    Mode = DataFilterMode.Date,
                    MatchMode = DataFilterMatchMode.Equals,
                    PlaceholderText = "\u2315"
                }
            };
        }

        private static CbsTableColumnDefinition CreateStageBooleanColumn(
            string fieldKey,
            string header,
            string displayField,
            string? filterField = null,
            string? sortField = null,
            string? bodyTemplateKey = null)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = fieldKey,
                Header = header,
                DisplayField = displayField,
                FilterField = filterField ?? displayField,
                SortField = sortField ?? filterField ?? displayField,
                DefaultWidth = "3rem",
                Alignment = CbsTableColumnAlignment.Center,
                BodyMode = bodyTemplateKey is null ? CbsTableBodyMode.BooleanIcon : CbsTableBodyMode.Text,
                BodyTemplateKey = bodyTemplateKey,
                IsFilterable = true,
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    EditorKind = CbsTableFilterEditorKind.Boolean,
                    Mode = DataFilterMode.Text,
                    MatchMode = DataFilterMatchMode.Equals
                }
            };
        }

        private static CbsTableColumnDefinition CreateStageTaskColumn()
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = "task",
                Header = "Тип",
                DisplayField = "task_kind.code",
                FilterField = "task_kind.id_or_tasks.task_kind_id",
                SortField = "task_kind.id_or_tasks.task_kind_id",
                DefaultWidth = "3rem",
                Alignment = CbsTableColumnAlignment.Right,
                IsImmutable = true,
                IsFilterable = true,
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    EditorKind = CbsTableFilterEditorKind.MultiSelect,
                    Mode = DataFilterMode.Numeric,
                    MatchMode = DataFilterMatchMode.In,
                    OptionsSourceKey = "TaskKind"
                }
            };
        }

        private static CbsTableColumnDefinition CreateStageStatusColumn()
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = "status",
                Header = "Статус",
                DisplayField = "status.name",
                FilterField = "status_id",
                SortField = "status_id",
                DefaultWidth = "7rem",
                Alignment = CbsTableColumnAlignment.Center,
                IsImmutable = true,
                BodyTemplateKey = "StatusBadge",
                IsFilterable = true,
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    EditorKind = CbsTableFilterEditorKind.MultiSelect,
                    Mode = DataFilterMode.Numeric,
                    MatchMode = DataFilterMatchMode.In,
                    OptionsSourceKey = "StageStatus"
                }
            };
        }

        private TablePageDefinition ApplySavedSettings(TablePageDefinition definition)
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

        private static void RemoveTableIfEmpty(
            LocalUserSettings localSettings,
            string route,
            LocalTableSettings tableSettings)
        {
            if (tableSettings.Columns.Count == 0 && tableSettings.ColumnOrder.Count == 0 && tableSettings.Sort is null)
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
    }
}
