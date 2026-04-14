using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.References
{
    public class ReferenceDefinitionService : IReferenceDefinitionService
    {
        private readonly IReadOnlyDictionary<string, ReferenceDefinition> _definitions;

        public ReferenceDefinitionService()
        {
            _definitions = BuildDefinitions()
                .ToDictionary(static item => item.Route, StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetByRoute(string? route, out ReferenceDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(route) && _definitions.TryGetValue(route, out definition!))
            {
                return true;
            }

            definition = null!;
            return false;
        }

        private static IReadOnlyList<ReferenceDefinition> BuildDefinitions()
        {
            return
            [
                new ReferenceDefinition
                {
                    Route = "/references/Area",
                    Model = "Area",
                    Title = "Регионы",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID"),
                        CreateTextColumn("name", "Наименование")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/Position",
                    Model = "Position",
                    Title = "Должности",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID"),
                        CreateTextColumn("name", "Наименование")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/Ownership",
                    Model = "Ownership",
                    Title = "Формы орг.",
                    Preset = "card",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("okopf", "ОКОПФ"),
                        CreateTextColumn("full_name", "Полное наименование")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/Department",
                    Model = "Department",
                    Title = "Отделы",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID"),
                        CreateTextColumn("name", "Наименование")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/TaskKind",
                    Model = "TaskKind",
                    Title = "Работы",
                    Preset = "card",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID"),
                        CreateNumberColumn("code", "Код"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("description", "Описание"),
                        CreateNumberColumn("cost", "Сумма"),
                        CreateTextColumn("duration", "Срок")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/Status",
                    Model = "Status",
                    Title = "Статусы",
                    Preset = "card",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID"),
                        CreateTextColumn("name", "Наименование"),
                        CreateNumberColumn("order", "Порядок"),
                        CreateTextColumn("description", "Описание")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/OrderStatus",
                    Model = "OrderStatus",
                    Title = "Статусы доставки",
                    Preset = "edit",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID"),
                        CreateTextColumn("name", "Наименование"),
                        CreateNumberColumn("order", "Порядок"),
                        CreateTextColumn("description", "Описание")
                    ]
                },
                new ReferenceDefinition
                {
                    Route = "/references/IsecurityTool",
                    Model = "IsecurityTool",
                    Title = "СЗИ",
                    Preset = "edit",
                    Columns =
                    [
                        CreateNumberColumn("id", "ID"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("unit", "Ед."),
                        CreateNumberColumn("priority", "Приоритет"),
                        CreateBooleanColumn("used", "Исп.")
                    ]
                }
            ];
        }

        private static CbsTableColumnDefinition CreateTextColumn(string key, string header)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = key,
                Header = header,
                ApiField = key,
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    PlaceholderText = "Фильтр содержит..."
                }
            };
        }

        private static CbsTableColumnDefinition CreateNumberColumn(string key, string header)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = key,
                Header = header,
                ApiField = key,
                Filter = new CbsTableColumnFilterDefinition()
            };
        }

        private static CbsTableColumnDefinition CreateBooleanColumn(string key, string header)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = key,
                Header = header,
                ApiField = key,
                BodyMode = CbsTableBodyMode.BooleanIcon,
                Filter = new CbsTableColumnFilterDefinition()
            };
        }
    }
}
