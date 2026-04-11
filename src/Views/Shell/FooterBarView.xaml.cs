using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using CbsContractsDesktopClient.ViewModels.Shell;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class FooterBarView : UserControl
    {
        public AppShellViewModel ViewModel { get; }

        public FooterBarView()
        {
            ViewModel = App.Services.GetRequiredService<AppShellViewModel>();
            InitializeComponent();
        }
    }
}
