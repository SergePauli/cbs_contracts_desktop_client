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
using CbsContractsDesktopClient.Services.Workspace;
using CbsContractsDesktopClient.ViewModels;
using CbsContractsDesktopClient.ViewModels.Shell;
using CbsContractsDesktopClient.ViewModels.Workflow;

namespace CbsContractsDesktopClient
{
    public partial class App : Application
    {
        private static readonly Uri PrimaryApiUri = new("http://serge-lenovo:5000/");
        private static readonly Uri DataQueryApiUri = new("http://serge-lenovo:8080/");
        private static readonly Uri FnsApiUri = new("https://api-fns.ru/api/");

        public static IServiceProvider Services { get; private set; } = null!;

        public static MainWindow? CurrentWindow { get; private set; }

        public App()
        {
            DiagnosticsFileLogger.Clear();
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
            services.AddSingleton<ITablePageDefinitionService, TablePageDefinitionService>();
            services.AddSingleton<IReferenceLookupCacheService, ReferenceLookupCacheService>();
            services.AddSingleton<AppShellViewModel>();
            services.AddSingleton<ReferencesContentViewModel>();
            services.AddSingleton<ContractWorkflowStore>();
            services.AddSingleton<StatusTableViewModel>();
            services.AddHttpClient(nameof(HolidayRecalculationService), client =>
            {
                client.BaseAddress = PrimaryApiUri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });
            services.AddSingleton<IHolidayRecalculationService>(provider =>
                new HolidayRecalculationService(
                    provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HolidayRecalculationService)),
                    provider.GetRequiredService<IUserService>()));
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
            services.AddHttpClient<IFnsContragentService, FnsContragentService>(client =>
            {
                client.BaseAddress = FnsApiUri;
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
            CurrentWindow = window;
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
