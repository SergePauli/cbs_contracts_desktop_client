// Contains the Contracts functional table definition and contract-specific column metadata.
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;

namespace CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions
{
    public sealed partial class TablePageDefinitionService
    {
        private static TablePageDefinition BuildContractsDefinition()
        {
            return new TablePageDefinition
            {
                Route = "/contracts",
                Model = "Contract",
                Title = "Контракты",
                NavigationDescription = "Выборка в разрезе договоров",
                Preset = "list",
                Kind = TablePageKind.Functional,
                Capabilities =
                    TablePageCapabilities.RowSelection
                    | TablePageCapabilities.Create
                    | TablePageCapabilities.Edit
                    | TablePageCapabilities.ResetFilters
                    | TablePageCapabilities.PersistColumnWidths
                    | TablePageCapabilities.PersistSort
                    | TablePageCapabilities.PersistFilters
                    | TablePageCapabilities.Audit
                    | TablePageCapabilities.ConfigureColumns,
                InitialSortField = "id",
                InitialSortDirection = DataSortDirection.Descending,
                RowStyleKey = CbsTableRowStyleKey.ContractDeadline,
                Columns =
                [
                    CreateContractNumberColumn("id", "ID", "id", "4rem"),
                    CreateContractTextColumn("name", "Номер", "name", "name", "order", "5rem", immutable: true),
                    CreateContractDateColumn("signed_at", "Дата", "signed_at", "signed_at", "6rem", immutable: true),
                    CreateContractDateColumn("expired_at", "Срок", "expired_at", "expired_at", "6rem", immutable: true),
                    CreateContractTextColumn("dsp", "ДСП", "revision", "revision", "revision", "3rem", bodyTemplateKey: "ContractDsp"),
                    CreateContractTextColumn("contragent", "Контрагент", "contragent.name", "contragent.org.name_or_contragent.org.full_name", "contragent.org.name", "19rem", immutable: true),
                    CreateContractMultiSelectColumn("region", "Регион", "contragent.region.name", "contragent.real_addr.address.area_id", "contragent.real_addr.address.area.name", "10rem", "Area", DataFilterMode.Numeric, bodyTemplateKey: "ContractRegion"),
                    CreateContractNumberColumn("cost", "Сумма", "cost", "7rem", immutable: true, bodyTemplateKey: "ContractCost"),
                    CreateContractStatusColumn(),
                    CreateContractBooleanColumn("is_funded", "БЗ", "is_funded", bodyTemplateKey: "ContractFunded"),
                    CreateContractDateColumn("funded_at", "БЗакр", "funded_at", "funded_at", "6rem"),
                    CreateContractBooleanColumn("governmental", "ГК", "governmental"),
                    CreateContractBooleanColumn("is_present", "ВНал", "is_present", "revision.is_present", "revision.is_present"),
                    CreateContractTextColumn("external_number", "Внешний №", "external_number", "external_number", "external_number", "10rem"),
                    CreateContractDateColumn("closed_at", "ДЗав", "closed_at", "closed_at", "6rem"),
                    CreateContractMultiSelectColumn("code", "Тип", "code", "code", "code", "4rem", "TaskKind", DataFilterMode.Text)
                ]
            };
        }

        private static CbsTableColumnDefinition CreateContractTextColumn(
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
                IsFilterable = bodyTemplateKey is not "ContractDsp",
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = bodyTemplateKey is not "ContractDsp",
                    EditorKind = CbsTableFilterEditorKind.Text,
                    Mode = DataFilterMode.Text,
                    MatchMode = DataFilterMatchMode.Contains,
                    PlaceholderText = "\u2315"
                }
            };
        }

        private static CbsTableColumnDefinition CreateContractNumberColumn(
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

        private static CbsTableColumnDefinition CreateContractDateColumn(
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

        private static CbsTableColumnDefinition CreateContractBooleanColumn(
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

        private static CbsTableColumnDefinition CreateContractMultiSelectColumn(
            string fieldKey,
            string header,
            string displayField,
            string filterField,
            string sortField,
            string width,
            string optionsSourceKey,
            DataFilterMode filterMode,
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
                BodyTemplateKey = bodyTemplateKey,
                IsFilterable = true,
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    EditorKind = CbsTableFilterEditorKind.MultiSelect,
                    Mode = filterMode,
                    MatchMode = DataFilterMatchMode.In,
                    OptionsSourceKey = optionsSourceKey
                }
            };
        }

        private static CbsTableColumnDefinition CreateContractStatusColumn()
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
                    OptionsSourceKey = "ContractStatus"
                }
            };
        }
    }
}
