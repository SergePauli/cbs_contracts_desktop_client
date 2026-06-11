using Microsoft.UI.Xaml.Controls;
using CbsContractsDesktopClient.ViewModels;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
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
                CloseButtonText = string.Empty,
                DefaultButton = ContentDialogButton.None,
                XamlRoot = this.XamlRoot
            };
            dialog.Content = BuildPlaceholderDialogContent(message, dialog.Hide);

            DialogChrome.Apply(dialog);

            await dialog.ShowAsync();
        }

        private static FrameworkElement BuildPlaceholderDialogContent(string message, Action close)
        {
            var closeButton = new Button
            {
                Content = "Закрыть",
                Width = 112,
                MinHeight = 28,
                Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var root = new Grid
            {
                MinWidth = 320,
                RowSpacing = 10
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.WrapWholeWords
            });

            Grid.SetRow(closeButton, 1);
            root.Children.Add(closeButton);
            closeButton.Click += (_, _) => close();
            return root;
        }
    }
}
