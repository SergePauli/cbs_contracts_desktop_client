using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;

namespace CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions
{
    public sealed partial class TablePageDefinitionService
    {
        private static TablePageDefinition BuildNeedsDefinition()
        {
            return new TablePageDefinition
            {
                Route = "/needs",
                Model = "StageOrder",
                Title = "Потребность",
                NavigationDescription = "Непривязанные позиции поставки",
                Preset = "card",
                Kind = TablePageKind.Functional,
                Capabilities =
                    TablePageCapabilities.RowSelection
                    | TablePageCapabilities.ResetFilters
                    | TablePageCapabilities.PersistColumnWidths
                    | TablePageCapabilities.PersistSort
                    | TablePageCapabilities.PersistFilters
                    | TablePageCapabilities.ConfigureColumns
                    | TablePageCapabilities.Audit,
                InitialSortField = "isecurity_tool_id",
                InitialSortDirection = DataSortDirection.Ascending,
                InitialFilters =
                [
                    new DataFilterCriterion
                    {
                        FieldKey = "order_id",
                        FilterMode = DataFilterMode.Numeric,
                        MatchMode = DataFilterMatchMode.In,
                        Value = new object?[] { null }
                    },
                    new DataFilterCriterion
                    {
                        FieldKey = "severity",
                        FilterMode = DataFilterMode.Numeric,
                        MatchMode = DataFilterMatchMode.In,
                        Value = new long[] { 0, 2, 3 }
                    }
                ],
                Columns =
                [
                    Column("id", "ID", "id", "id", "5rem", CbsTableColumnAlignment.Right, DataFilterMode.Numeric),
                    Column("isecurity_tool_id", "ID товара", "isecurity_tool.id", "isecurity_tool_id", "6rem", CbsTableColumnAlignment.Right, DataFilterMode.Numeric),
                    Column("isecurity_tool", "Товар", "isecurity_tool.name", "isecurity_tool.name", "20rem"),
                    Column("unit", "Ед.", "isecurity_tool.unit", "isecurity_tool.unit", "5rem"),
                    Column("amount", "Кол-во", "amount", "amount", "5rem", CbsTableColumnAlignment.Right, DataFilterMode.Numeric),
                    IsecurityToolKindColumn(),
                    OrderColumn("order_number", "Номер счета", "order.order_number", "10rem"),
                    OrderColumn("supplier", "Поставщик", "order.supplier.name", "16rem"),
                    OrderStatusColumn(),
                    Column("stage", "Этап", "stage.name", "stage.name", "12rem"),
                    StageStatusColumn(),
                    Column("stage_deadline_at", "Срок", "stage.deadline_at", "stage.deadline_at", "7rem", CbsTableColumnAlignment.Left, DataFilterMode.Date),
                    Column("contragent", "Контрагент", "stage.contragent", "stage.contragent", "18rem"),
                    SeverityColumn(),
                    Column("price_cost", "Цена", "price_cost", "price_cost", "9rem", CbsTableColumnAlignment.Right, DataFilterMode.Numeric),
                    Column("cost", "Сумма", "cost", "cost", "10rem", CbsTableColumnAlignment.Right, DataFilterMode.Numeric),
                    Column("description", "Описание", "description", "description", "20rem"),
                    new CbsTableColumnDefinition
                    {
                        FieldKey = "order_id",
                        Header = "Заказ",
                        ApiField = "order_id",
                        FilterField = "order_id",
                        SortField = "order_id",
                        IsVisible = false,
                        IsImmutable = true
                    }
                ]
            };
        }

        private static CbsTableColumnDefinition Column(
            string fieldKey,
            string header,
            string displayField,
            string apiField,
            string width,
            CbsTableColumnAlignment alignment = CbsTableColumnAlignment.Left,
            DataFilterMode filterMode = DataFilterMode.Text,
            string? bodyTemplateKey = null)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = fieldKey,
                Header = header,
                ApiField = apiField,
                DisplayField = displayField,
                FilterField = apiField,
                SortField = apiField,
                DefaultWidth = width,
                Alignment = alignment,
                BodyTemplateKey = bodyTemplateKey,
                IsFilterable = true,
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    EditorKind = filterMode switch
                    {
                        DataFilterMode.Numeric => CbsTableFilterEditorKind.Numeric,
                        _ => CbsTableFilterEditorKind.Text
                    },
                    Mode = filterMode,
                    MatchMode = filterMode switch
                    {
                        DataFilterMode.Text => DataFilterMatchMode.Contains,
                        DataFilterMode.Date => DataFilterMatchMode.GreaterThanOrEqual,
                        _ => DataFilterMatchMode.Equals
                    },
                    PlaceholderText = "\u2315"
                }
            };
        }

        private static CbsTableColumnDefinition SeverityColumn()
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = "severity",
                Header = "Важность",
                ApiField = "severity",
                DisplayField = "severity",
                FilterField = "severity",
                SortField = "severity",
                DefaultWidth = "10rem",
                Alignment = CbsTableColumnAlignment.Left,
                BodyTemplateKey = "StageOrderSeverity",
                IsFilterable = true,
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    EditorKind = CbsTableFilterEditorKind.MultiSelect,
                    Mode = DataFilterMode.Numeric,
                    MatchMode = DataFilterMatchMode.In,
                    StaticOptions = Enum.GetValues<StageOrderSeverity>()
                        .Select(static severity => new CbsTableFilterOptionDefinition
                        {
                            Value = (long)severity,
                            Label = StageOrderSeverityText.GetLabel(severity)
                        })
                        .ToList()
                }
            };
        }

        private static CbsTableColumnDefinition IsecurityToolKindColumn()
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = "isecurity_tool_kind",
                Header = "Тип товара",
                DisplayField = "isecurity_tool.kind",
                FilterField = "isecurity_tool.kind",
                SortField = "isecurity_tool.kind",
                DefaultWidth = "9rem",
                Alignment = CbsTableColumnAlignment.Left,
                BodyTemplateKey = "IsecurityToolKind",
                IsFilterable = true,
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    EditorKind = CbsTableFilterEditorKind.MultiSelect,
                    Mode = DataFilterMode.Numeric,
                    MatchMode = DataFilterMatchMode.In,
                    StaticOptions = Enum.GetValues<IsecurityToolKind>()
                        .Select(static kind => new CbsTableFilterOptionDefinition
                        {
                            Value = (long)kind,
                            Label = IsecurityToolKindText.GetLabel(kind)
                        })
                        .ToList()
                }
            };
        }

        private static CbsTableColumnDefinition OrderColumn(
            string fieldKey,
            string header,
            string apiField,
            string width,
            string? bodyTemplateKey = null)
        {
            var column = Column(fieldKey, header, apiField, apiField, width, bodyTemplateKey: bodyTemplateKey);
            column.IsVisible = false;
            return column;
        }

        private static CbsTableColumnDefinition OrderStatusColumn()
        {
            var column = StatusColumn(
                "order_status",
                "Статус заказа",
                "order.status.name",
                "order.order_status_id",
                "OrderStatus",
                "OrderDeliveryStatus");
            column.IsVisible = false;
            return column;
        }

        private static CbsTableColumnDefinition StageStatusColumn() =>
            StatusColumn(
                "stage_status",
                "Статус этапа",
                "stage.status.name",
                "stage.status_id",
                "StageStatus");

        private static CbsTableColumnDefinition StatusColumn(
            string fieldKey,
            string header,
            string displayField,
            string filterField,
            string optionsSourceKey,
            string? bodyTemplateKey = null) => new()
        {
            FieldKey = fieldKey,
            Header = header,
            ApiField = displayField,
            DisplayField = displayField,
            FilterField = filterField,
            SortField = filterField,
            DefaultWidth = "10rem",
            Alignment = CbsTableColumnAlignment.Left,
            BodyTemplateKey = bodyTemplateKey,
            IsFilterable = true,
            Filter = new CbsTableColumnFilterDefinition
            {
                IsEnabled = true,
                EditorKind = CbsTableFilterEditorKind.MultiSelect,
                Mode = DataFilterMode.Numeric,
                MatchMode = DataFilterMatchMode.In,
                OptionsSourceKey = optionsSourceKey
            }
        };
    }
}
