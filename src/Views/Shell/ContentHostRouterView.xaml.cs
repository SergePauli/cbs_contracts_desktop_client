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
        private ReferenceHostView? _referenceHostView;
        private HolidayHostView? _holidayHostView;
        private ProfileHostView? _profileHostView;
        private EmployeeHostView? _employeeHostView;
        private ContragentHostView? _contragentHostView;
        private RevisionHostView? _revisionHostView;
        private StageHostView? _stageHostView;

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
            if (routeKind == _currentRouteKind && HostContentControl.Content is not null)
            {
                UpdateCurrentHostRoute(routeKind, route);
                return;
            }

            _currentRouteKind = routeKind;
            HostContentControl.Content = routeKind switch
            {
                ContentHostRouteKind.Reference => GetReferenceHostView(route),
                ContentHostRouteKind.Holiday => GetHolidayHostView(route),
                ContentHostRouteKind.Profile => GetProfileHostView(route),
                ContentHostRouteKind.Employee => GetEmployeeHostView(route),
                ContentHostRouteKind.Contragent => GetContragentHostView(route),
                ContentHostRouteKind.Revision => GetRevisionHostView(route),
                ContentHostRouteKind.Stage => GetStageHostView(route),
                _ => CreatePlaceholder()
            };
        }

        private void UpdateCurrentHostRoute(ContentHostRouteKind routeKind, string? route)
        {
            switch (routeKind)
            {
                case ContentHostRouteKind.Reference:
                    if (_referenceHostView is not null)
                    {
                        _referenceHostView.Route = route;
                    }

                    break;
                case ContentHostRouteKind.Holiday:
                    if (_holidayHostView is not null)
                    {
                        _holidayHostView.Route = route;
                    }

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
            }
        }

        private ContentHostRouteKind ResolveRouteKind(string? route)
        {
            if (string.Equals(route, "/holidays", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Holiday;
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

            if (string.Equals(route, "/revisions", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Revision;
            }

            if (string.Equals(route, "/stages", StringComparison.OrdinalIgnoreCase))
            {
                return ContentHostRouteKind.Stage;
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
            _referenceHostView ??= new ReferenceHostView();
            _referenceHostView.Route = route;
            return _referenceHostView;
        }

        private HolidayHostView GetHolidayHostView(string? route)
        {
            _holidayHostView ??= new HolidayHostView();
            _holidayHostView.Route = route;
            return _holidayHostView;
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
            Profile,
            Employee,
            Contragent,
            Revision,
            Stage
        }
    }
}




