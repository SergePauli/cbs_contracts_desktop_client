using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.AppNotifications;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Net.Http.Headers;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Navigation;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;
using CbsContractsDesktopClient.ViewModels;
using CbsContractsDesktopClient.ViewModels.Shell;

namespace CbsContractsDesktopClient
{
    public partial class App : Application
    {
        private static readonly Uri PrimaryApiUri = new("http://serge-lenovo:5000/");
        private static readonly Uri DataQueryApiUri = new("http://serge-lenovo:8080/");

        public static IServiceProvider Services { get; private set; } = null!;

        public App()
        {
            this.InitializeComponent();
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<ICredentialManagerService, CredentialManagerService>();
            services.AddSingleton<IUserService, UserService>();
            services.AddSingleton<INavigationMenuService, NavigationMenuService>();
            services.AddSingleton<ILocalUserSettingsService, LocalUserSettingsService>();
            services.AddSingleton<IReferenceDefinitionService, ReferenceDefinitionService>();
            services.AddSingleton<AppShellViewModel>();
            services.AddSingleton<ReferencesContentViewModel>();
            services.AddSingleton<StatusTableViewModel>();
            services.AddHttpClient<IHolidayRecalculationService, HolidayRecalculationService>(client =>
            {
                client.BaseAddress = PrimaryApiUri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });
            services.AddHttpClient<IReferenceCrudService, ReferenceCrudService>(client =>
            {
                client.BaseAddress = PrimaryApiUri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });
            services.AddHttpClient<IDataQueryService, DataQueryService>(client =>
            {
                client.BaseAddress = DataQueryApiUri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });
            services.AddHttpClient<IAuthService, AuthService>(client =>
            {
                client.BaseAddress = PrimaryApiUri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainWindow>();

            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            TryRegisterAppNotifications();
            var window = Services.GetRequiredService<MainWindow>();
            window.Activate();
        }

        private static void TryRegisterAppNotifications()
        {
            try
            {
                AppNotificationManager.Default.Register();
            }
            catch (COMException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
