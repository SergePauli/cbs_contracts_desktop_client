using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CbsContractsDesktopClient.ViewModels.Shell;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class AppShellPage : Page
    {
        public event Action? LogoutRequested;

        private readonly AppShellViewModel _viewModel;
        private readonly GridLength _expandedSidebarWidth;
        private readonly GridLength _expandedAuditWidth;

        public AppShellPage()
        {
            _viewModel = App.Services.GetRequiredService<AppShellViewModel>();
            _viewModel.Reset();

            InitializeComponent();

            _expandedSidebarWidth = (GridLength)Application.Current.Resources["ShellSidebarWidth"];
            _expandedAuditWidth = (GridLength)Application.Current.Resources["ShellAuditWidth"];

            SidebarView.LogoutRequested += OnSidebarLogoutRequested;
            _viewModel.PropertyChanged += OnShellStateChanged;
            Unloaded += OnUnloaded;

            ApplyShellState();
        }

        private void OnSidebarLogoutRequested()
        {
            LogoutRequested?.Invoke();
        }

        private void OnShellStateChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppShellViewModel.IsSidebarVisible)
                || e.PropertyName == nameof(AppShellViewModel.IsAuditPanelOpen))
            {
                ApplyShellState();
            }
        }

        private void ApplyShellState()
        {
            SidebarView.Visibility = _viewModel.IsSidebarVisible ? Visibility.Visible : Visibility.Collapsed;
            SidebarColumn.Width = _viewModel.IsSidebarVisible ? _expandedSidebarWidth : new GridLength(0);

            AuditPanelControl.Visibility = _viewModel.IsAuditPanelOpen ? Visibility.Visible : Visibility.Collapsed;
            AuditColumn.Width = _viewModel.IsAuditPanelOpen ? _expandedAuditWidth : new GridLength(0);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            SidebarView.LogoutRequested -= OnSidebarLogoutRequested;
            _viewModel.PropertyChanged -= OnShellStateChanged;
            Unloaded -= OnUnloaded;
        }
    }
}
