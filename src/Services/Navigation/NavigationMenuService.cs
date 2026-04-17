using System;
using System.Collections.Generic;
using System.Linq;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Models.Navigation;

namespace CbsContractsDesktopClient.Services.Navigation
{
    public class NavigationMenuService : INavigationMenuService
    {
        private const int OziDepartmentId = 1;
        private const int CommersDepartmentId = 2;
        private const int FinDepartmentId = 3;
        private const int LeadDepartmentId = 4;

        private const string ContractRoute = "/contract";
        private const string ContractsRoute = "/contracts";
        private const string StagesRoute = "/stages";
        private const string RevisionsRoute = "/revisions";
        private const string EmployeesRoute = "/employees";
        private const string ContragentsRoute = "/contragents";
        private const string ReferencesRoute = "/references";
        private const string HolidaysRoute = "/holidays";
        private const string UsersRoute = "/users";
        private const string ReportRoute = "/report";

        public IReadOnlyList<NavigationMenuSection> BuildMenu(User? user, string? currentRoute = null)
        {
            if (user == null)
            {
                return [];
            }

            if (HasRole(user, "intern"))
            {
                var internRoute = string.IsNullOrWhiteSpace(currentRoute) ? StagesRoute : currentRoute;
                return
                [
                    new NavigationMenuSection
                    {
                        Title = "База",
                        Items =
                        [
                            CreateItem("Этапы", "\uE7C1", StagesRoute, internRoute)
                        ]
                    },
                    BuildSessionSection(internRoute)
                ];
            }

            var departmentId = user.DepartmentId;
            var isAdmin = HasRole(user, "admin") || departmentId == LeadDepartmentId;
            var isCommer = departmentId == CommersDepartmentId;
            var isOzi = departmentId == OziDepartmentId;
            var isFin = departmentId == FinDepartmentId;
            var route = string.IsNullOrWhiteSpace(currentRoute)
                ? (isAdmin || isCommer ? ContractRoute : ContractsRoute)
                : currentRoute;

            var baseSection = new NavigationMenuSection
            {
                Title = "База",
                Items = []
            };

            if (isAdmin || isCommer)
            {
                baseSection.Items.Add(CreateItem("Контракт", "\uE8A5", ContractRoute, route));
            }

            baseSection.Items.Add(CreateItem("Контракты", "\uE8D2", ContractsRoute, route));
            baseSection.Items.Add(CreateItem("Этапы", "\uE7C1", StagesRoute, route));
            baseSection.Items.Add(CreateItem("ДС-ки", "\uE8A7", RevisionsRoute, route));

            var referencesSection = new NavigationMenuSection
            {
                Title = "Справочники",
                Items = [],
                IsCollapsible = true,
                IsExpanded = false
            };

            AddDistinct(referencesSection.Items, "Сотрудники", "\uE716", EmployeesRoute, route);
            AddDistinct(referencesSection.Items, "Контрагенты", "\uE821", ContragentsRoute, route);

            if (isAdmin)
            {
                AddDistinct(referencesSection.Items, "Регионы", "\uE707", $"{ReferencesRoute}/Area", route);
                AddDistinct(referencesSection.Items, "Календарь", "\uE787", HolidaysRoute, route);
                AddDistinct(referencesSection.Items, "Формы орг.", "\uE8D1", $"{ReferencesRoute}/Ownership", route);
                AddDistinct(referencesSection.Items, "Пользователи", "\uE77B", UsersRoute, route);
                AddDistinct(referencesSection.Items, "Отделы", "\uE902", $"{ReferencesRoute}/Department", route);
                AddDistinct(referencesSection.Items, "Статусы", "\uE8D2", $"{ReferencesRoute}/Status", route);
                AddDistinct(referencesSection.Items, "Доставка", "\uE806", $"{ReferencesRoute}/OrderStatus", route);
            }

            AddDistinct(referencesSection.Items, "Работы", "\uE90F", $"{ReferencesRoute}/TaskKind", route);
            AddDistinct(referencesSection.Items, "Должности", "\uE8EF", $"{ReferencesRoute}/Position", route);

            if (isOzi || isAdmin)
            {
                AddDistinct(referencesSection.Items, "СЗИ", "\uE72E", $"{ReferencesRoute}/IsecurityTool", route);
                AddDistinct(referencesSection.Items, "Статусы доставки", "\uE806", $"{ReferencesRoute}/OrderStatus", route);
            }

            if (isFin)
            {
                AddDistinct(referencesSection.Items, "Формы орг.", "\uE8D1", $"{ReferencesRoute}/Ownership", route);
            }

            return
            [
                baseSection,
                referencesSection,
                new NavigationMenuSection
                {
                    Title = "Отчеты",
                    Items =
                    [
                        CreateItem("Активность", "\uE7C4", ReportRoute, route)
                    ]
                },
                BuildSessionSection(route)
            ];
        }

        private static NavigationMenuSection BuildSessionSection(string currentRoute)
        {
            return new NavigationMenuSection
            {
                Title = "Сеанс",
                IsSessionSection = true,
                Items =
                [
                    CreateItem("Выход", "\uE8AC", "/logout", currentRoute, isAction: true)
                ]
            };
        }

        private static NavigationMenuItem CreateItem(string title, string glyph, string route, string currentRoute, bool isAction = false)
        {
            var isSelected = string.Equals(route, currentRoute, StringComparison.OrdinalIgnoreCase);
            return new NavigationMenuItem
            {
                Title = title,
                Glyph = glyph,
                Route = route,
                IsSelected = isSelected,
                IsAction = isAction,
                Background = isSelected ? "#FFD9ECFF" : "Transparent",
                Foreground = "#FF1F1F1F",
                IconForeground = isSelected ? "#FF2C85CC" : "#FF75879B",
                FontWeightValue = isSelected ? 600 : 400
            };
        }

        private static void AddDistinct(List<NavigationMenuItem> items, string title, string glyph, string route, string currentRoute)
        {
            if (items.Any(item => string.Equals(item.Route, route, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            items.Add(CreateItem(title, glyph, route, currentRoute));
        }

        private static bool HasRole(User user, string role)
        {
            return user.Role?.Contains(role, StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
