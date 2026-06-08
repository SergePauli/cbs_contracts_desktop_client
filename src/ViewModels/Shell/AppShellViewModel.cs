using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Navigation;
using CbsContractsDesktopClient.Models.Shell;
using CbsContractsDesktopClient.Services;

namespace CbsContractsDesktopClient.ViewModels.Shell
{
    public partial class AppShellViewModel : ObservableObject
    {
        private readonly IUserService _userService;

        public AppShellViewModel(IUserService userService)
        {
            _userService = userService;
            FooterState = BuildFooterState();
            AuditPanelState = AuditPanelState.Empty;
        }

        [ObservableProperty]
        public partial bool IsSidebarVisible { get; set; } = true;

        [ObservableProperty]
        public partial bool IsAuditPanelOpen { get; set; }

        [ObservableProperty]
        public partial NavigationMenuItem? SelectedNavigationItem { get; set; }

        [ObservableProperty]
        public partial string CurrentRoute { get; set; } = string.Empty;

        [ObservableProperty]
        public partial FooterState FooterState { get; set; }

        [ObservableProperty]
        public partial AuditPanelState AuditPanelState { get; set; }

        [ObservableProperty]
        public partial string AuditPanelText { get; set; } = string.Empty;

        public ObservableCollection<BreadcrumbItemState> BreadcrumbItems { get; } = [];

        public ObservableCollection<NavigationMenuItem> ContextNavigationItems { get; } = [];

        public void Reset()
        {
            IsSidebarVisible = true;
            IsAuditPanelOpen = false;
            SelectedNavigationItem = null;
            CurrentRoute = string.Empty;
            FooterState = BuildFooterState(string.Empty);
            AuditPanelState = AuditPanelState.Empty;
            AuditPanelText = string.Empty;
            BreadcrumbItems.Clear();
            ContextNavigationItems.Clear();
        }

        public void ToggleSidebar()
        {
            IsSidebarVisible = !IsSidebarVisible;
        }

        public void ToggleAuditPanel()
        {
            IsAuditPanelOpen = !IsAuditPanelOpen;
        }

        public void SetSelectedNavigationItem(NavigationMenuItem item)
        {
            SelectedNavigationItem = item;
            CurrentRoute = item.Route;
            BreadcrumbItems.Clear();

            if (!string.IsNullOrWhiteSpace(item.SectionTitle))
            {
                BreadcrumbItems.Add(new BreadcrumbItemState(item.SectionTitle, string.Empty));
            }

            BreadcrumbItems.Add(new BreadcrumbItemState(item.Title, item.Route));
        }

        public void SetContextNavigationItems(IEnumerable<NavigationMenuItem> items)
        {
            ContextNavigationItems.Clear();

            foreach (var item in items)
            {
                item.SectionTitle = "Контекст";
                ContextNavigationItems.Add(item);
            }
        }

        public void ClearContextNavigationItems()
        {
            ContextNavigationItems.Clear();
        }

        public void SetAuditPanelState(AuditPanelState state)
        {
            AuditPanelState = state;
        }

        public void SetAuditPanelText(string text)
        {
            AuditPanelText = text;
        }

        public void SetFooterTableStats(string totalCountValue, string selectedRecordText = "")
        {
            FooterState = BuildFooterState(totalCountValue, selectedRecordText);
        }

        public void ResetAuditPanelState()
        {
            AuditPanelState = AuditPanelState.Empty;
            AuditPanelText = string.Empty;
        }

        private FooterState BuildFooterState(
            string totalCountValue = "",
            string selectedRecordText = "")
        {
            var user = _userService.CurrentUser;

            if (user is null)
            {
                return FooterState.Empty;
            }

            var departmentOrRole = !string.IsNullOrWhiteSpace(user.DepartmentName)
                ? user.DepartmentName
                : !string.IsNullOrWhiteSpace(user.Role)
                    ? user.Role
                    : "Роль не определена";

            var userName = !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName
                : !string.IsNullOrWhiteSpace(user.Username)
                    ? user.Username
                    : "Пользователь не определен";

            var selectedRecordParts = SplitFooterSelectedRecordText(selectedRecordText);

            return new FooterState
            {
                DepartmentOrRole = departmentOrRole,
                UserName = userName,
                TotalCountValue = totalCountValue,
                SelectedRecordText = selectedRecordParts.MainText,
                SelectedRecordFooterText = selectedRecordParts.FooterText,
                SelectedRecordTasksText = selectedRecordParts.TasksText,
                SelectedRecordPerformersText = selectedRecordParts.PerformersText,
                VersionText = "v1.0.0"
            };
        }

        private static (string MainText, string FooterText, string TasksText, string PerformersText) SplitFooterSelectedRecordText(string value)
        {
            var parts = value.Split('|', 3, StringSplitOptions.TrimEntries);
            if (parts.Length == 1)
            {
                return (value, string.Empty, string.Empty, string.Empty);
            }

            if (parts.Length == 2)
            {
                return (parts[0], parts[1], string.Empty, string.Empty);
            }

            return (parts[0], string.Empty, parts[1], parts[2]);
        }
    }
}





