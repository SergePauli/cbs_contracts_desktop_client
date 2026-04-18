using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using CbsContractsDesktopClient.Models.Shell;
using CbsContractsDesktopClient.ViewModels.Shell;
using System.ComponentModel;

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
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        public string AuditPanelGlyph => ViewModel.IsAuditPanelOpen ? "\uE76C" : "\uE71D";

        public string AuditPanelToolTip => ViewModel.IsAuditPanelOpen
            ? "Свернуть"
            : "Аудит изменений";

        public Brush? AuditPanelIconBrush => ViewModel.IsAuditPanelOpen
            ? new SolidColorBrush(Microsoft.UI.Colors.White)
            : Application.Current.Resources["ShellSecondaryTextBrush"] as Brush;

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

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppShellViewModel.IsAuditPanelOpen))
            {
                Bindings.Update();
            }
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
