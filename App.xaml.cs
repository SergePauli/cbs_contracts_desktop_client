using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.AppNotifications;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Net.Http.Headers;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Navigation;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;
using CbsContractsDesktopClient.Services.Workspace;
using CbsContractsDesktopClient.Services.Orders;
using CbsContractsDesktopClient.ViewModels;
using CbsContractsDesktopClient.Stores.Contragents;
using CbsContractsDesktopClient.Stores.Table;
using CbsContractsDesktopClient.Stores.Orders;
using CbsContractsDesktopClient.ViewModels.Shell;
using CbsContractsDesktopClient.ViewModels.Workflow;

namespace CbsContractsDesktopClient
{
    public partial class App : Application
    {
        public const string API_SERVER = "serge-lenovo";

        public static Uri PrimaryApiUri { get; } = new($"http://{API_SERVER}:5000/");
        public static Uri DataQueryApiUri { get; } = new($"http://{API_SERVER}:8080/");
        public static Uri FnsApiUri { get; } = new("https://api-fns.ru/api/");

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
            services.AddSingleton<ITableSettingsService, TableSettingsService>();
            services.AddSingleton<IReferenceDefinitionService, ReferenceDefinitionService>();
            services.AddSingleton<ITablePageDefinitionService, TablePageDefinitionService>();
            services.AddSingleton<IReferenceLookupCacheService, ReferenceLookupCacheService>();
            services.AddSingleton<IsecurityToolCatalogService>();
            services.AddSingleton<IsecurityToolEditWorkflow>();
            services.AddSingleton<IContragentLookupService, ContragentLookupService>();
            services.AddSingleton<IEmployeeEditWorkflow, EmployeeEditWorkflow>();
            services.AddSingleton<IContragentFnsWorkflow, ContragentFnsWorkflow>();
            services.AddSingleton<AppShellViewModel>();
            services.AddSingleton<AuditStore>();
            services.AddSingleton<ContragentDetailStore>();
            services.AddSingleton<TablePageStore>();
            services.AddSingleton<ContractWorkflowStore>();
            services.AddSingleton<OrderWorkflowStore>();
            services.AddSingleton<OrderPositionsStore>();
            services.AddSingleton<OrderWorkflowFactory>();
            services.AddSingleton<OrderEditWorkflow>();
            services.AddSingleton<StageOrderEditWorkflow>();
            services.AddSingleton<StageSupplyEditWorkflow>();
            services.AddSingleton<ContractWorkflowFactory>();
            services.AddSingleton<ContractCommentWorkflow>();
            services.AddSingleton<StatusTableViewModel>();
            services.AddHttpClient(nameof(AuthService), client =>
            {
                client.BaseAddress = PrimaryApiUri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });
            services.AddSingleton(provider =>
                new AuthService(
                    provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(AuthService)),
                    provider.GetRequiredService<IUserService>()));
            services.AddSingleton<IAuthService>(provider => provider.GetRequiredService<AuthService>());
            services.AddSingleton<IAccessTokenRefreshService>(provider => provider.GetRequiredService<AuthService>());
            services.AddHttpClient(nameof(HolidayRecalculationService), client =>
            {
                client.BaseAddress = PrimaryApiUri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });
            services.AddSingleton<IHolidayRecalculationService>(provider =>
                new HolidayRecalculationService(
                    provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(HolidayRecalculationService)),
                    provider.GetRequiredService<IUserService>(),
                    provider.GetRequiredService<IAccessTokenRefreshService>()));
            services.AddHttpClient<IModelMutationService, ModelMutationService>(client =>
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




