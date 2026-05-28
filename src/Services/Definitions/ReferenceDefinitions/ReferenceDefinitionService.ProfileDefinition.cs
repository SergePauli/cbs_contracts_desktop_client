// Contains the Profile/User reference definition and user table metadata.
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions
{
    public partial class ReferenceDefinitionService
    {
        private static ReferenceDefinition BuildProfileReferenceDefinition()
        {
            return CreateReferenceDefinition(
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
                    ]);
        }
    }
}