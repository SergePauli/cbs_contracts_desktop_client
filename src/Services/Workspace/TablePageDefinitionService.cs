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
            ];
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
                    Columns = definition.Columns
                };
            }

            return definition;
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
