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
            if (!string.IsNullOrWhiteSpace(route) && _definitions.TryGetValue(route, out var storedDefinition))
            {
                definition = storedDefinition.Clone();
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
                        CreateNumberColumn("id", "ID", width: "5rem"),
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
                        CreateNumberColumn("id", "ID", width: "5rem"),
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
                        CreateNumberColumn("id", "ID", width: "5rem"),
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
                        CreateNumberColumn("id", "ID", width: "5rem"),
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
                        CreateNumberColumn("id", "ID", width: "5rem"),
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
                        CreateNumberColumn("id", "ID", width: "5rem"),
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
                        CreateNumberColumn("id", "ID", width: "5rem"),
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
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("unit", "Ед."),
                        CreateNumberColumn("priority", "Приоритет"),
                        CreateBooleanColumn("used", "Исп.")
                    ]
                }
            ];
        }

        private static CbsTableColumnDefinition CreateTextColumn(string key, string header, string? width = null)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = key,
                Header = header,
                ApiField = key,
                DefaultWidth = width ?? GetDefaultTextWidth(key),
                Filter = new CbsTableColumnFilterDefinition
                {
                    IsEnabled = true,
                    PlaceholderText = "Фильтр содержит..."
                }
            };
        }

        private static CbsTableColumnDefinition CreateNumberColumn(string key, string header, string? width = null)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = key,
                Header = header,
                ApiField = key,
                DefaultWidth = width ?? GetDefaultNumberWidth(key),
                Filter = new CbsTableColumnFilterDefinition()
            };
        }

        private static CbsTableColumnDefinition CreateBooleanColumn(string key, string header, string? width = null)
        {
            return new CbsTableColumnDefinition
            {
                FieldKey = key,
                Header = header,
                ApiField = key,
                DefaultWidth = width ?? "3rem",
                BodyMode = CbsTableBodyMode.BooleanIcon,
                Filter = new CbsTableColumnFilterDefinition()
            };
        }

        private static string GetDefaultTextWidth(string key)
        {
            return key switch
            {
                "okopf" => "5rem",
                "unit" => "3rem",
                "duration" => "7rem",
                _ => "16rem"
            };
        }

        private static string GetDefaultNumberWidth(string key)
        {
            return key switch
            {
                "id" => "5rem",
                "code" => "3rem",
                "order" => "3rem",
                "priority" => "7rem",
                "cost" => "7rem",
                _ => "6rem"
            };
        }
    }
}
