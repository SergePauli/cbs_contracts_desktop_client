using CbsContractsDesktopClient.ViewModels.Shell;
// Routes shell content routes to the concrete content host view.
using System;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class ContentHostRouterView : UserControl
    {
        private readonly AppShellViewModel _shellViewModel;
        private ContentHostRouteKind _currentRouteKind = ContentHostRouteKind.None;
        private HolidayHostView? _holidayHostView;
        private DiagnosticsHostView? _diagnosticsHostView;
        private ProfileHostView? _profileHostView;
        private EmployeeHostView? _employeeHostView;
        private ContragentHostView? _contragentHostView;
        private ContractHostView? _contractHostView;
        private RevisionHostView? _revisionHostView;
        private StageHostView? _stageHostView;
        private OrderHostView? _orderHostView;
        private StageOrderNeedsHostView? _stageOrderNeedsHostView;
        private ActivityReportHostView? _activityReportHostView;

        public ContentHostRouterView()
        {
            _shellViewModel = App.Services.GetRequiredService<AppShellViewModel>();

            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            ApplyRoute(_shellViewModel.CurrentRoute);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _shellViewModel.PropertyChanged += OnShellViewModelPropertyChanged;
            ApplyRoute(_shellViewModel.CurrentRoute);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _shellViewModel.PropertyChanged -= OnShellViewModelPropertyChanged;
        }

        private void OnShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppShellViewModel.CurrentRoute))
            {
                ApplyRoute(_shellViewModel.CurrentRoute);
            }
        }

        private void ApplyRoute(string? route)
        {
            var routeKind = ResolveRouteKind(route);
            if (routeKind == _currentRouteKind
                && routeKind != ContentHostRouteKind.Reference
                && HostContentControl.Content is not null)
            {
                UpdateCurrentHostRoute(routeKind, route);
                return;
            }

            _currentRouteKind = routeKind;
            HostContentControl.Content = routeKind switch
            {
                ContentHostRouteKind.Reference => GetReferenceHostView(route),
                ContentHostRouteKind.Holiday => GetHolidayHostView(route),
                ContentHostRouteKind.Diagnostics => GetDiagnosticsHostView(),
                ContentHostRouteKind.Profile => GetProfileHostView(route),
                ContentHostRouteKind.Employee => GetEmployeeHostView(route),
                ContentHostRouteKind.Contragent => GetContragentHostView(route),
                ContentHostRouteKind.Contract => GetContractHostView(route),
                ContentHostRouteKind.Revision => GetRevisionHostView(route),
                ContentHostRouteKind.Stage => GetStageHostView(route),
                ContentHostRouteKind.Order => GetOrderHostView(route),
                ContentHostRouteKind.Needs => GetStageOrderNeedsHostView(route),
                ContentHostRouteKind.ActivityReport => GetActivityReportHostView(route),
                _ => CreatePlaceholder()
            };
        }

        private void UpdateCurrentHostRoute(ContentHostRouteKind routeKind, string? route)
        {
            switch (routeKind)
            {
                case ContentHostRouteKind.Holiday:
                    if (_holidayHostView is not null)
                    {
                        _holidayHostView.Route = route;
                    }

                    break;
                case ContentHostRouteKind.Diagnostics:
                    break;
                case ContentHostRouteKind.Profile:
                    if (_profileHostView is not null)
                    {
                        _profileHostView.Route = route;
                    }

                    break;
                case ContentHostRouteKind.Employee:
                    if (_employeeHostView is not null)
                    {
                        _employeeHostView.Route = route;
                    }

                    break;
                case ContentHostRouteKind.Contragent:
                    if (_contragentHostView is not null)
                    {
                        _contragentHostView.Route = route;
                    }

                    break;
                case ContentHostRouteKind.Contract:
                    if (_contractHostView is not null)
                    {
                        _contractHostView.Route = route;
                    }

                    break;
                case ContentHostRouteKind.Revision:
                    if (_revisionHostView is not null)
                    {
                        _revisionHostView.Route = route;
                    }

                    break;
                case ContentHostRouteKind.Stage:
                    if (_stageHostView is not null)
                    {
                        _stageHostView.Route = route;
                    }

                    break;
                case ContentHostRouteKind.Order:
                    if (_orderHostView is not null)
                    {
                        _orderHostView.Route = route;
                    }

                    break;
                case ContentHostRouteKind.Needs:
                    if (_stageOrderNeedsHostView is not null)
                    {
                        _stageOrderNeedsHostView.Route = route;
                    }

                    break;
                case ContentHostRouteKind.ActivityReport:
                    if (_activityReportHostView is not null)
                    {
                        _activityReportHostView.Route = route;
                    }

                    break;
            }
        }

        private ContentHostRouteKind ResolveRouteKind(string? route)
        {
            if (string.Equals(route, "/holidays", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Holiday;
            }

            if (string.Equals(route, "/diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Diagnostics;
            }

            if (string.Equals(route, "/users", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Profile;
            }

            if (string.Equals(route, "/employees", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Employee;
            }

            if (string.Equals(route, "/contragents", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Contragent;
            }

            if (string.Equals(route, "/contracts", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Contract;
            }

            if (string.Equals(route, "/revisions", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Revision;
            }

            if (string.Equals(route, "/stages", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Stage;
            }

            if (string.Equals(route, "/orders", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Order;
            }

            if (string.Equals(route, "/needs", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Needs;
            }

            if (string.Equals(route, "/report", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.ActivityReport;
            }

            if (IsSimpleReferenceRoute(route))
            {
                return ContentHostRouteKind.Reference;
            }

            return ContentHostRouteKind.Placeholder;
        }

        private static bool IsSimpleReferenceRoute(string? route)
        {
            return !string.IsNullOrWhiteSpace(route)
                && route.StartsWith("/references/", StringComparison.OrdinalIgnoreCase);
        }

        private ReferenceHostView GetReferenceHostView(string? route)
        {
            return new ReferenceHostView(route ?? string.Empty);
        }

        private HolidayHostView GetHolidayHostView(string? route)
        {
            _holidayHostView ??= new HolidayHostView();
            _holidayHostView.Route = route;
            return _holidayHostView;
        }

        private DiagnosticsHostView GetDiagnosticsHostView()
        {
            _diagnosticsHostView ??= new DiagnosticsHostView();
            return _diagnosticsHostView;
        }

        private ProfileHostView GetProfileHostView(string? route)
        {
            _profileHostView ??= new ProfileHostView();
            _profileHostView.Route = route;
            return _profileHostView;
        }

        private EmployeeHostView GetEmployeeHostView(string? route)
        {
            _employeeHostView ??= new EmployeeHostView();
            _employeeHostView.Route = route;
            return _employeeHostView;
        }

        private ContragentHostView GetContragentHostView(string? route)
        {
            _contragentHostView ??= new ContragentHostView();
            _contragentHostView.Route = route;
            return _contragentHostView;
        }

        private ContractHostView GetContractHostView(string? route)
        {
            _contractHostView ??= new ContractHostView();
            _contractHostView.Route = route;
            return _contractHostView;
        }

        private RevisionHostView GetRevisionHostView(string? route)
        {
            _revisionHostView ??= new RevisionHostView();
            _revisionHostView.Route = route;
            return _revisionHostView;
        }

        private StageHostView GetStageHostView(string? route)
        {
            _stageHostView ??= new StageHostView();
            _stageHostView.Route = route;
            return _stageHostView;
        }

        private OrderHostView GetOrderHostView(string? route)
        {
            _orderHostView ??= new OrderHostView();
            _orderHostView.Route = route;
            return _orderHostView;
        }

        private StageOrderNeedsHostView GetStageOrderNeedsHostView(string? route)
        {
            _stageOrderNeedsHostView ??= new StageOrderNeedsHostView();
            _stageOrderNeedsHostView.Route = route;
            return _stageOrderNeedsHostView;
        }

        private ActivityReportHostView GetActivityReportHostView(string? route)
        {
            _activityReportHostView ??= new ActivityReportHostView();
            _activityReportHostView.Route = route;
            return _activityReportHostView;
        }

        private static FrameworkElement CreatePlaceholder()
        {
            return new Border
            {
                Margin = new Thickness(0),
                Background = Application.Current.Resources["ShellPanelBackgroundBrush"] as Microsoft.UI.Xaml.Media.Brush,
                BorderBrush = Application.Current.Resources["ShellPanelBorderBrush"] as Microsoft.UI.Xaml.Media.Brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = new TextBlock
                {
                    Margin = new Thickness(16),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Application.Current.Resources["ShellSecondaryTextBrush"] as Microsoft.UI.Xaml.Media.Brush,
                    Text = "Выберите поддерживаемый раздел в меню."
                }
            };
        }

        private enum ContentHostRouteKind
        {
            None,
            Placeholder,
            Reference,
            Holiday,
            Diagnostics,
            Profile,
            Employee,
            Contragent,
            Contract,
            Revision,
            Stage,
            Order,
            Needs,
            ActivityReport
        }
    }
}




