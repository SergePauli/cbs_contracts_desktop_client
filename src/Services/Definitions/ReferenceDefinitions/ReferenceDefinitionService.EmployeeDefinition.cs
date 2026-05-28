// Contains the Employee reference definition and employee-specific table metadata.
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions
{
    public partial class ReferenceDefinitionService
    {
        private static ReferenceDefinition BuildEmployeeReferenceDefinition()
        {
            return CreateReferenceDefinition(
                    route: "/employees",
                    model: "Employee",
                    title: "Сотрудники",
                    preset: "card",
                    initialSortField: "id",
                    initialSortDirection: DataSortDirection.Descending,
                    editorKind: ReferenceEditorKind.Employee,
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "ФИО", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("contragent", "Контрагент", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("position", "Должность", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("contacts", "Контакты", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateBooleanField("used", "Уволен", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateNumberField("priority", "Пор.", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("description", "Описание", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true)
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
                            Header = "ФИО",
                            DisplayField = "person.full_name",
                            FilterField = "person.person_name.naming.fio",
                            SortField = "person.person_name.naming.surname",
                            DefaultWidth = "30rem",
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
                            FieldKey = "contragent",
                            Header = "Контрагент",
                            DisplayField = "contragent.name",
                            FilterField = "org.name_or_org.full_name",
                            SortField = "org.name_or_org.full_name",
                            DefaultWidth = "30rem",
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
                            FieldKey = "position",
                            Header = "Должность",
                            DisplayField = "position.name",
                            FilterField = "position.name",
                            SortField = "position.name",
                            DefaultWidth = "35rem",
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
                            FieldKey = "contacts",
                            Header = "Контакты",
                            DisplayField = "person.contacts.name",
                            FilterField = "person.person_contacts.contact.value",
                            DefaultWidth = "35rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            IsSortable = false,
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
                            FieldKey = "used",
                            Header = "Ув.",
                            ApiField = "used",
                            DisplayField = "used",
                            FilterField = "used",
                            SortField = "used",
                            DefaultWidth = "5rem",
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
                        },
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "priority",
                            Header = "Пор.",
                            ApiField = "priority",
                            DisplayField = "priority",
                            FilterField = "priority",
                            SortField = "priority",
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
                            FieldKey = "description",
                            Header = "Описание",
                            ApiField = "description",
                            DisplayField = "description",
                            FilterField = "description",
                            SortField = "description",
                            DefaultWidth = "40rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            IsFilterable = true,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                IsEnabled = true,
                                Mode = DataFilterMode.Text,
                                MatchMode = DataFilterMatchMode.Contains,
                                PlaceholderText = "\u2315"
                            }
                        }
                    ]);
        }
    }
}