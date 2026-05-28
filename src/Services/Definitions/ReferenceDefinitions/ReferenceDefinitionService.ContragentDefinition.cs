// Contains the Contragent reference definition and contragent-specific table metadata.
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions
{
    public partial class ReferenceDefinitionService
    {
        private static ReferenceDefinition BuildContragentReferenceDefinition()
        {
            return CreateReferenceDefinition(
                    route: "/contragents",
                    model: "Contragent",
                    title: "Контрагенты",
                    preset: "card",
                    initialSortField: "id",
                    initialSortDirection: DataSortDirection.Descending,
                    editorKind: ReferenceEditorKind.Contragent,
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("inn", "ИНН", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("ownership", "Тип", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("region", "Регион", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("full_name", "Полное наименование", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("address", "Адрес", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true)
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
                                EditorKind = CbsTableFilterEditorKind.Numeric,
                                Mode = DataFilterMode.Numeric,
                                MatchMode = DataFilterMatchMode.Equals,
                                PlaceholderText = "\u2315"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "name",
                            Header = "Наименование",
                            DisplayField = "requisites.organization.name",
                            FilterField = "org.name",
                            SortField = "org.name",
                            DefaultWidth = "13rem",
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
                            FieldKey = "inn",
                            Header = "ИНН",
                            DisplayField = "requisites.organization.inn",
                            FilterField = "org.inn",
                            SortField = "org.inn",
                            DefaultWidth = "6rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                Mode = DataFilterMode.Text,
                                MatchMode = DataFilterMatchMode.StartsWith,
                                PlaceholderText = "\u2315"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "ownership",
                            Header = "Тип",
                            DisplayField = "requisites.organization.ownership.name",
                            FilterField = "org.ownership_id",
                            SortField = "org.ownership_id",
                            DefaultWidth = "5rem",
                            Alignment = CbsTableColumnAlignment.Center,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                EditorKind = CbsTableFilterEditorKind.MultiSelect,
                                Mode = DataFilterMode.Text,
                                MatchMode = DataFilterMatchMode.In,
                                OptionsSourceKey = "Ownership",
                                EmptySelectionText = "Все"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "region",
                            Header = "Регион",
                            DisplayField = "region.name",
                            FilterField = "real_addr.address.area_id",
                            SortField = "real_addr.address.area_id",
                            DefaultWidth = "9rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                EditorKind = CbsTableFilterEditorKind.MultiSelect,
                                Mode = DataFilterMode.Text,
                                MatchMode = DataFilterMatchMode.In,
                                OptionsSourceKey = "Area",
                                EmptySelectionText = "Все"
                            }
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "full_name",
                            Header = "Полное наименование",
                            DisplayField = "requisites.organization.full_name",
                            FilterField = "org.full_name",
                            SortField = "org.full_name",
                            DefaultWidth = "25rem",
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
                            FieldKey = "address",
                            Header = "Адрес",
                            DisplayField = "real_addr.address.value",
                            DefaultWidth = "24rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            IsSortable = false,
                            IsFilterable = false
                        }
                    ]);
        }
    }
}