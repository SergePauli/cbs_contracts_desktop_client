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
        public partial FooterState FooterState { get; set; }

        [ObservableProperty]
        public partial AuditPanelState AuditPanelState { get; set; }

        public ObservableCollection<object> BreadcrumbItems { get; } = [];

        public ObservableCollection<NavigationMenuItem> ContextNavigationItems { get; } = [];

        public void Reset()
        {
            IsSidebarVisible = true;
            IsAuditPanelOpen = false;
            SelectedNavigationItem = null;
            FooterState = BuildFooterState();
            AuditPanelState = AuditPanelState.Empty;
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
            BreadcrumbItems.Clear();

            if (!string.IsNullOrWhiteSpace(item.SectionTitle))
            {
                BreadcrumbItems.Add(item.SectionTitle);
            }

            BreadcrumbItems.Add(item.Title);
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

        public void ResetAuditPanelState()
        {
            AuditPanelState = AuditPanelState.Empty;
        }

        private FooterState BuildFooterState()
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

            return new FooterState
            {
                DepartmentOrRole = departmentOrRole,
                UserName = userName,
                VersionText = "v1.0.0"
            };
        }
    }
}
