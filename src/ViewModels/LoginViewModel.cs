using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CbsContractsDesktopClient.Services;
using System;
using System.Threading.Tasks;

namespace CbsContractsDesktopClient.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly AuthService _authService;
        private readonly CredentialManagerService _credentialManagerService;
        private readonly UserService _userService;

        [ObservableProperty]
        private string username = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        private string debugLoginResponse = string.Empty;

        [ObservableProperty]
        private bool rememberMe = false;

        public LoginViewModel(AuthService authService, UserService userService, CredentialManagerService credentialManagerService)
        {
            _authService = authService;
            _userService = userService;
            _credentialManagerService = credentialManagerService;
            LoginCommand = new AsyncRelayCommand(LoginAsync);

            LoadSavedCredentials();
        }

        public IAsyncRelayCommand LoginCommand { get; }

        public event Action? LoginSucceeded;

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Введите имя пользователя и пароль";
                return;
            }

            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                var response = await _authService.LoginAsync(Username, Password);

                if (!response.Success)
                {
                    ErrorMessage = response.Message;
                    return;
                }

                if (response.User != null)
                {
                    DebugLoginResponse = response.DebugJson;

                    try
                    {
                        if (RememberMe)
                        {
                            _credentialManagerService.SaveCredentials(Username, Password);
                        }
                        else
                        {
                            _credentialManagerService.DeleteCredentials();
                        }
                    }
                    catch
                    {
                        // Не блокируем вход, если системное хранилище временно недоступно.
                    }

                    _userService.SetCurrentUser(response.User);
                    LoginSucceeded?.Invoke();
                }
                else
                {
                    ErrorMessage = "Ответ сервера не содержит данные пользователя.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Ошибка авторизации: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadSavedCredentials()
        {
            var savedCredentials = _credentialManagerService.TryGetCredentials();
            if (savedCredentials == null)
            {
                return;
            }

            Username = savedCredentials.Username;
            Password = savedCredentials.Password;
            RememberMe = true;
        }
    }
}
