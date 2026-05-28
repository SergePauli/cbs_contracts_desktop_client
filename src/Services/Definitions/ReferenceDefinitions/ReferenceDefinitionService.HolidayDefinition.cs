// Contains the Holiday reference definition and its table metadata.
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions
{
    public partial class ReferenceDefinitionService
    {
        private static ReferenceDefinition BuildHolidayReferenceDefinition()
        {
            return CreateReferenceDefinition(
                    route: "/holidays",
                    model: "Holiday",
                    title: "Календарь",
                    navigationDescription: "Календарь выходных",
                    preset: "card",
                    initialSortField: "begin_at",
                    initialSortDirection: DataSortDirection.Descending,
                    fields:
                    [
                        CreateNumberField("id", "ID", isRequired: true, isReadOnlyOnCreate: true, isReadOnlyOnEdit: true),
                        CreateDateField("begin_at", "Начало", isRequired: true),
                        CreateDateField("end_at", "Окончание"),
                        CreateTextField("name", "Описание", isRequired: true),
                        CreateBooleanField("work", "Рабочий день")
                    ],
                    columns:
                    [
                        CreateNumberColumn("id", "ID", width: "5rem"),
                        CreateDateColumn("begin_at", "Начало", width: "10rem", matchMode: DataFilterMatchMode.GreaterThanOrEqual),
                        new CbsTableColumnDefinition
                        {
                            FieldKey = "end_at",
                            Header = "Окончание",
                            ApiField = "end_at",
                            SortField = "end_at",
                            DefaultWidth = "10rem",
                            Alignment = CbsTableColumnAlignment.Left,
                            Filter = new CbsTableColumnFilterDefinition
                            {
                                Mode = DataFilterMode.Date
                            }
                        },
                        CreateTextColumn("name", "Описание"),
                        CreateBooleanColumn("work", "Рабочий", width: "4rem")
                    ],
                    isAuditEnabled: true);
        }
    }
}