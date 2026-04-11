using System.Collections.ObjectModel;
using CbsContractsDesktopClient.Models.Contacts;

namespace CbsContractsDesktopClient.ViewModels.Shell
{
    public sealed class ContactsTableViewModel
    {
        public ObservableCollection<ContactTableRow> Contacts { get; } =
        [
            new()
            {
                Id = 1001,
                FullName = "Анна Ковалева",
                CompanyName = "ООО Альфа Поставка",
                DepartmentName = "Закупки",
                Email = "a.kovaleva@alpha.example",
                Status = "Активен"
            },
            new()
            {
                Id = 1002,
                FullName = "Сергей Ильин",
                CompanyName = "АО ТехИмпорт",
                DepartmentName = "Юридический отдел",
                Email = "s.ilin@techimport.example",
                Status = "На согласовании"
            },
            new()
            {
                Id = 1003,
                FullName = "Мария Соколова",
                CompanyName = "ООО СеверЛогистик",
                DepartmentName = "Логистика",
                Email = "m.sokolova@northlog.example",
                Status = "Активен"
            },
            new()
            {
                Id = 1004,
                FullName = "Игорь Беляев",
                CompanyName = "ПАО РегионЭнерго",
                DepartmentName = "Финансы",
                Email = "i.belyaev@regenergy.example",
                Status = "Заблокирован"
            },
            new()
            {
                Id = 1005,
                FullName = "Екатерина Миронова",
                CompanyName = "ООО КонтрактСервис",
                DepartmentName = "Продажи",
                Email = "e.mironova@contracts.example",
                Status = "Активен"
            }
        ];
    }
}
