// Contains the Revisions functional table definition and revision-specific column metadata.
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;

namespace CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions
{
    public sealed partial class TablePageDefinitionService
    {
        private static TablePageDefinition BuildRevisionsDefinition()
        {
            return new TablePageDefinition
                {
                    Route = "/revisions",
                    Model = "Revision",
                    Title = "ДСоглашения",
                    NavigationDescription = "Выборка в разрезе Дополнительных соглашений",
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
    }
}
