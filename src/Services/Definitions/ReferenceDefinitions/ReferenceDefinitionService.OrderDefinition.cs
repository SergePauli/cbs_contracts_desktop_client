using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions
{
    public partial class ReferenceDefinitionService
    {
        private static ReferenceDefinition BuildOrderReferenceDefinition()
        {
            return CreateReferenceDefinition(
                route: "/orders",
                model: "Order",
                title: "Заказы",
                preset: "list",
                initialSortField: "id",
                initialSortDirection: DataSortDirection.Descending,
                fields:
                [
                    CreateNumberField("id", "ID", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                    CreateTextField("order_number", "Номер", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                    CreateTextField("supplier", "Поставщик", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                    CreateTextField("status", "Статус", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                    CreateNumberField("cost", "Стоимость", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                    CreateDateField("requested_at", "Запрошен", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                    CreateDateField("ordered_at", "Заказан", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                    CreateDateField("payment_at", "Оплачен", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                    CreateDateField("received_at", "Получен", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                    CreateTextField("description", "Описание", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                    CreateDateField("created_at", "Создан", isReadOnlyOnCreate: true, isReadOnlyOnEdit: true)
                ],
                columns:
                [
                    CreateNumberColumn("id", "ID", width: "5rem"),
                    CreateTextColumn("order_number", "Номер", width: "12rem"),
                    CreateOrderRelationColumn("supplier", "Поставщик", "supplier.name", "18rem"),
                    CreateOrderRelationColumn("status", "Статус", "status.name", "10rem"),
                    CreateNumberColumn("cost", "Стоимость", width: "10rem"),
                    CreateDateColumn("requested_at", "Запрошен"),
                    CreateDateColumn("ordered_at", "Заказан"),
                    CreateDateColumn("payment_at", "Оплачен"),
                    CreateDateColumn("received_at", "Получен"),
                    CreateTextColumn("description", "Описание", width: "20rem"),
                    CreateDateColumn("created_at", "Создан")
                ]);
        }

        private static CbsTableColumnDefinition CreateOrderRelationColumn(
            string key,
            string header,
            string field,
            string width)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = key,
                Header = header,
                ApiField = field,
                DisplayField = field,
                FilterField = field,
                SortField = field,
                DefaultWidth = width,
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
    }
}
