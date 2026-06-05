// Contains the Stages functional table definition and stage-specific column metadata.
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;

namespace CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions
{
    public sealed partial class TablePageDefinitionService
    {
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
                    | TablePageCapabilities.PersistFilters
                    | TablePageCapabilities.Audit
                    | TablePageCapabilities.ConfigureColumns,
                InitialSortField = "id",
                InitialSortDirection = DataSortDirection.Descending,
                RowStyleKey = CbsTableRowStyleKey.StageDeadline,
                Columns =
                [
                    CreateStageNumberColumn("id", "ID", "id", "4rem", immutable: true),
                    CreateStageTaskColumn(),
                    CreateStageTextColumn("name", "Номер", "name", "contract.name", "name", "5rem", immutable: true),
                    CreateStageDateColumn("start_at", "Старт", "start_at", "start_at", "6rem", immutable: true),
                    CreateStageDateColumn("deadline_at", "Срок", "deadline_at", "deadline_at", "6rem", immutable: true),
                    CreateStageTextColumn("contragent", "Контрагент", "contract.contragent.name", "contract.contragent.org.name_or_contract.contragent.org.full_name", "contract.contragent.org.name", "19rem", immutable: true),
                    CreateStageTextColumn("region", "Регион", "contract.contragent.region.name", "contract.contragent.real_addr.address.area_id", "contract.contragent.real_addr.address.area.name", "10rem", bodyTemplateKey: "StageRegion"),
                    CreateStageNumberColumn("cost", "Сумма", "cost", "7rem", immutable: true, bodyTemplateKey: "StageCost"),
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
    }
}
