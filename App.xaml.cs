using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.ViewModels;

namespace CbsContractsDesktopClient
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public App()
        {
            this.InitializeComponent();
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<CredentialManagerService>();
            services.AddSingleton<UserService>();
            services.AddHttpClient<AuthService>(client =>
            {
                client.BaseAddress = new Uri("http://serge-lenovo:5000");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            });
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainWindow>();

            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var window = Services.GetRequiredService<MainWindow>();
            window.Activate();
        }
    }
}
