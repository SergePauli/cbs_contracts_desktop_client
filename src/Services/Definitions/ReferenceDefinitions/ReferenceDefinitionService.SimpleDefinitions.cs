// Contains simple scalar reference definitions used by the shared reference workspace.
using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions
{
    public partial class ReferenceDefinitionService
    {
        private static IReadOnlyList<ReferenceDefinition> BuildSimpleReferenceDefinitions()
        {
            return
            [
                CreateReferenceDefinition(
                    route: "/references/Area",
                    model: "Area",
                    title: "Регионы",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true)
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/Position",
                    model: "Position",
                    title: "Должности",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true)
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/Ownership",
                    model: "Ownership",
                    title: "Формы орг.",
                    navigationDescription: "Формы собственности организаций",
                    preset: "card",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true),
                        CreateTextField("okopf", "ОКОПФ"),
                        CreateTextField("full_name", "Полное наименование")
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("okopf", "ОКОПФ"),
                        CreateTextColumn("full_name", "Полное наименование")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/Department",
                    model: "Department",
                    title: "Отделы",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true)
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/TaskKind",
                    model: "TaskKind",
                    title: "Работы",
                    preset: "card",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateNumberField("code", "Код", isRequired: true),
                        CreateTextField("name", "Наименование", isRequired: true),
                        CreateTextField("description", "Описание"),
                        CreateNumberField("cost", "Сумма"),
                        CreateTextField("duration", "Срок")
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateNumberColumn("code", "Код"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("description", "Описание"),
                        CreateNumberColumn("cost", "Сумма"),
                        CreateTextColumn("duration", "Срок")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/Status",
                    model: "Status",
                    title: "Статусы",
                    preset: "card",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true),
                        CreateNumberField("order", "Порядок", isRequired: true),
                        CreateTextField("description", "Описание")
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateNumberColumn("order", "Порядок"),
                        CreateTextColumn("description", "Описание")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/OrderStatus",
                    model: "OrderStatus",
                    title: "Статусы доставки",
                    preset: "card",
                    includeIdOnCreate: true,
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true),
                        CreateNumberField("order", "Порядок", isRequired: true),
                        CreateTextField("description", "Описание")
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateNumberColumn("order", "Порядок"),
                        CreateTextColumn("description", "Описание")
                    ]),
                CreateReferenceDefinition(
                    route: "/references/IsecurityTool",
                    model: "IsecurityTool",
                    title: "СЗИ",
                    preset: "card",
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateTextField("name", "Наименование", isRequired: true),
                        CreateTextField("unit", "Ед."),
                        CreateNumberField("default_cost", "Стоимость по умолчанию"),
                        CreateEnumField(
                            "kind",
                            "Тип товара",
                            Enum.GetValues<IsecurityToolKind>()
                                .Select(static kind => new ReferenceEnumOption((long)kind, IsecurityToolKindText.GetLabel(kind)))
                                .ToList(),
                            isRequired: true)
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateTextColumn("name", "Наименование"),
                        CreateTextColumn("unit", "Ед."),
                        CreateNumberColumn("default_cost", "Стоимость по умолчанию"),
                        CreateNumberColumn("kind", "Тип товара", width: "9rem", bodyTemplateKey: "IsecurityToolKind")
                    ])
            ];
        }
    }
}
