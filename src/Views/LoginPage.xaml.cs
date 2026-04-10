using Microsoft.UI.Xaml.Controls;
using CbsContractsDesktopClient.ViewModels;
using System.Threading.Tasks;

namespace CbsContractsDesktopClient.Views
{
    public sealed partial class LoginPage : Page
    {
        public LoginViewModel ViewModel { get; }

        public event Action? LoginSucceeded;

        public LoginPage(LoginViewModel viewModel)
        {
            this.InitializeComponent();
            ViewModel = viewModel;
            ViewModel.LoginSucceeded += OnLoginSucceeded;
            Unloaded += OnUnloaded;
            DataContext = ViewModel;

            PasswordInput.Password = ViewModel.Password;
        }

        private void OnLoginSucceeded()
        {
            LoginSucceeded?.Invoke();
        }

        private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ViewModel.LoginSucceeded -= OnLoginSucceeded;
            Unloaded -= OnUnloaded;
        }

        private void PasswordBox_PasswordChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.Password = passwordBox.Password;
            }
        }

        private async void RegistrationLink_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ShowPlaceholderDialogAsync("Регистрация", "Форма регистрации пока не реализована.");
        }

        private async void ForgotPasswordLink_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await ShowPlaceholderDialogAsync("Восстановление пароля", "Форма восстановления пароля пока не реализована.");
        }

        private async Task ShowPlaceholderDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "Закрыть",
                XamlRoot = this.XamlRoot
            };

            await dialog.ShowAsync();
        }
    }
}
