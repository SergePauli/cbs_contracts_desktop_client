using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.ViewModels;
using CbsContractsDesktopClient.Views;
using CbsContractsDesktopClient.Views.Shell;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.IO;
using Windows.Graphics;

namespace CbsContractsDesktopClient
{
    public sealed partial class MainWindow : Window
    {
        private static readonly SizeInt32 PreferredWindowSize = new(1440, 900);
        private const int WindowPadding = 24;

        private readonly IAuthService _authService;
        private readonly LoginViewModel _loginViewModel;

        public MainWindow(LoginViewModel loginViewModel, IAuthService authService)
        {
            InitializeComponent();
            _loginViewModel = loginViewModel;
            _authService = authService;

            ConfigureWindowIcon();
            ConfigureWindowBounds();
            ShowLoginPage();
        }

        private void ConfigureWindowBounds()
        {
            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            var targetWidth = Math.Min(PreferredWindowSize.Width, Math.Max(960, workArea.Width - WindowPadding * 2));
            var targetHeight = Math.Min(PreferredWindowSize.Height, Math.Max(700, workArea.Height - WindowPadding * 2));

            var size = new SizeInt32(targetWidth, targetHeight);
            AppWindow.Resize(size);

            var x = workArea.X + Math.Max(0, (workArea.Width - size.Width) / 2);
            var y = workArea.Y + Math.Max(0, (workArea.Height - size.Height) / 2);
            AppWindow.Move(new PointInt32(x, y));
        }

        private void ConfigureWindowIcon()
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "favicon.ico");
            if (File.Exists(iconPath))
            {
                AppWindow.SetIcon(iconPath);
            }
        }

        private void ShowLoginPage()
        {
            RootGrid.Children.Clear();

            var loginPage = new LoginPage(_loginViewModel);
            loginPage.LoginSucceeded += OnLoginSucceeded;
            RootGrid.Children.Add(loginPage);
        }

        private void ShowShellPage()
        {
            RootGrid.Children.Clear();

            var appShellPage = new AppShellPage();
            appShellPage.LogoutRequested += OnLogoutRequested;
            RootGrid.Children.Add(appShellPage);
        }

        private void OnLoginSucceeded()
        {
            ShowShellPage();
        }

        private void OnLogoutRequested()
        {
            _authService.Logout();
            ShowLoginPage();
        }
    }
}
