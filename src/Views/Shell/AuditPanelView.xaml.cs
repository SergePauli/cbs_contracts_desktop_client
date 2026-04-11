using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using CbsContractsDesktopClient.ViewModels.Shell;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class AuditPanelView : UserControl
    {
        public AppShellViewModel ViewModel { get; }

        public AuditPanelView()
        {
            ViewModel = App.Services.GetRequiredService<AppShellViewModel>();
            InitializeComponent();
        }
    }
}
