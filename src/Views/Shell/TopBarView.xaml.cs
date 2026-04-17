using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CbsContractsDesktopClient.Models.Shell;
using CbsContractsDesktopClient.ViewModels.Shell;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class TopBarView : UserControl
    {
        public event Action<object>? BreadcrumbItemInvoked;
        public AppShellViewModel ViewModel { get; }

        public TopBarView()
        {
            ViewModel = App.Services.GetRequiredService<AppShellViewModel>();
            InitializeComponent();
        }

        public UIElement? RightContent
        {
            get => (UIElement?)GetValue(RightContentProperty);
            set => SetValue(RightContentProperty, value);
        }

        public static readonly DependencyProperty RightContentProperty =
            DependencyProperty.Register(
                nameof(RightContent),
                typeof(UIElement),
                typeof(TopBarView),
                new PropertyMetadata(null));

        private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleSidebar();
        }

        private void AuditPanelToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleAuditPanel();
        }

        private void TopBreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            if (args.Item is BreadcrumbItemState breadcrumbItem && breadcrumbItem.IsNavigable)
            {
                BreadcrumbItemInvoked?.Invoke(breadcrumbItem);
            }
        }
    }
}
