using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.AppNotifications;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections;
using System.Text;
using System.Threading;
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
using CbsContractsDesktopClient.ViewModels.Reports;

namespace CbsContractsDesktopClient
{
    public partial class App : Application
    {
        public const string API_SERVER = "192.168.0.251";

        public static Uri PrimaryApiUri { get; } = new($"http://{API_SERVER}:5000/");
        public static Uri DataQueryApiUri { get; } = new($"http://{API_SERVER}:8085/");
        public static Uri FnsApiUri { get; } = new("https://api-fns.ru/api/");

        public static IServiceProvider Services { get; private set; } = null!;

        public static MainWindow? CurrentWindow { get; private set; }

        public App()
        {
            DiagnosticsFileLogger.Clear();
            UnhandledException += OnApplicationUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            this.InitializeComponent();
            Services = ConfigureServices();
        }

        private static void OnApplicationUnhandledException(
            object sender,
            Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
        {
            DiagnosticsFileLogger.AppendBlock(
                "GLOBAL XAML UNHANDLED EXCEPTION",
                BuildXamlUnhandledExceptionDiagnostic(sender, args));
        }

        private static string BuildXamlUnhandledExceptionDiagnostic(
            object sender,
            Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"eventArgsType={args.GetType().AssemblyQualifiedName}");
            builder.AppendLine($"senderType={sender.GetType().AssemblyQualifiedName}");
            builder.AppendLine($"handled={args.Handled}");
            builder.AppendLine($"message={args.Message}");
            builder.AppendLine($"threadId={Environment.CurrentManagedThreadId}");
            builder.AppendLine($"threadName={Thread.CurrentThread.Name ?? "<null>"}");
            builder.AppendLine($"threadState={Thread.CurrentThread.ThreadState}");
            builder.AppendLine($"apartmentState={Thread.CurrentThread.GetApartmentState()}");
            builder.AppendLine(
                $"synchronizationContext={SynchronizationContext.Current?.GetType().AssemblyQualifiedName ?? "<null>"}");
            builder.AppendLine($"debuggerAttached={System.Diagnostics.Debugger.IsAttached}");

            AppendExceptionDiagnostic(builder, args.Exception, depth: 0);

            builder.AppendLine("handlerStackTrace:");
            builder.AppendLine(Environment.StackTrace);
            return builder.ToString().TrimEnd();
        }

        private static void AppendExceptionDiagnostic(
            StringBuilder builder,
            Exception? exception,
            int depth)
        {
            var prefix = depth == 0 ? "exception" : $"innerException[{depth}]";
            if (exception is null)
            {
                builder.AppendLine($"{prefix}=<null>");
                return;
            }

            builder.AppendLine($"{prefix}.type={exception.GetType().AssemblyQualifiedName}");
            builder.AppendLine($"{prefix}.message={exception.Message}");
            builder.AppendLine($"{prefix}.hResult=0x{exception.HResult:X8}");
            builder.AppendLine($"{prefix}.source={exception.Source ?? "<null>"}");
            builder.AppendLine($"{prefix}.targetSite={exception.TargetSite?.ToString() ?? "<null>"}");
            builder.AppendLine($"{prefix}.helpLink={exception.HelpLink ?? "<null>"}");
            builder.AppendLine($"{prefix}.stackTrace:");
            builder.AppendLine(exception.StackTrace ?? "<null>");
            builder.AppendLine($"{prefix}.data.count={exception.Data.Count}");
            foreach (DictionaryEntry entry in exception.Data)
            {
                builder.AppendLine(
                    $"{prefix}.data[{entry.Key?.ToString() ?? "<null>"}]={entry.Value?.ToString() ?? "<null>"}");
            }

            builder.AppendLine($"{prefix}.toString:");
            builder.AppendLine(exception.ToString());

            if (exception.InnerException is not null)
            {
                AppendExceptionDiagnostic(builder, exception.InnerException, depth + 1);
            }
        }

        private static void OnAppDomainUnhandledException(
            object sender,
            System.UnhandledExceptionEventArgs args)
        {
            DiagnosticsFileLogger.AppendBlock(
                "GLOBAL APPDOMAIN UNHANDLED EXCEPTION",
                $"isTerminating={args.IsTerminating}{Environment.NewLine}"
                + $"exception={args.ExceptionObject}");
        }

        private static void OnUnobservedTaskException(
            object? sender,
            UnobservedTaskExceptionEventArgs args)
        {
            DiagnosticsFileLogger.AppendBlock(
                "GLOBAL UNOBSERVED TASK EXCEPTION",
                $"observed={args.Observed}{Environment.NewLine}"
                + $"exception={args.Exception}");
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
            services.AddSingleton<StageStatusFilterOptionsProvider>();
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
            services.AddSingleton<StageOrderNeedsStore>();
            services.AddSingleton<OrderWorkflowFactory>();
            services.AddSingleton<OrderEditWorkflow>();
            services.AddSingleton<StageOrderEditWorkflow>();
            services.AddSingleton<StageSupplyEditWorkflow>();
            services.AddSingleton<ContractWorkflowFactory>();
            services.AddSingleton<ContractCommentWorkflow>();
            services.AddSingleton<ContractCommerSaveWorkflow>();
            services.AddSingleton<ActivityReportStore>();
            services.AddSingleton<ActivityReportLoader>();
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




