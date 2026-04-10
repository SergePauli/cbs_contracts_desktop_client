using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class AppShellPage : Page
    {
        public event Action? LogoutRequested;

        public AppShellPage()
        {
            InitializeComponent();
            SidebarView.LogoutRequested += OnSidebarLogoutRequested;
            Unloaded += OnUnloaded;
        }

        private void OnSidebarLogoutRequested()
        {
            LogoutRequested?.Invoke();
        }

        private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            SidebarView.LogoutRequested -= OnSidebarLogoutRequested;
            Unloaded -= OnUnloaded;
        }
    }
}
