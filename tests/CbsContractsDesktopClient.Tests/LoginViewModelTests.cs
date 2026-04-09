using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.ViewModels;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public class LoginViewModelTests
{
    [Fact]
    public void Constructor_LoadsSavedCredentials_WhenTheyExist()
    {
        var authService = new FakeAuthService();
        var userService = new FakeUserService();
        var credentialService = new FakeCredentialManagerService
        {
            StoredCredentials = new SavedCredentials("saved-user", "saved-password")
        };

        var viewModel = new LoginViewModel(authService, userService, credentialService);

        Assert.Equal("saved-user", viewModel.Username);
        Assert.Equal("saved-password", viewModel.Password);
        Assert.True(viewModel.RememberMe);
    }

    [Fact]
    public async Task LoginCommand_DoesNotCallAuth_WhenUsernameOrPasswordIsMissing()
    {
        var authService = new FakeAuthService();
        var userService = new FakeUserService();
        var credentialService = new FakeCredentialManagerService();
        var viewModel = new LoginViewModel(authService, userService, credentialService)
        {
            Username = "",
            Password = ""
        };

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.Equal(0, authService.LoginCallCount);
        Assert.False(viewModel.IsLoading);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
    }

    [Fact]
    public async Task LoginCommand_SetsUserAndSavesCredentials_OnSuccessfulLogin()
    {
        var authService = new FakeAuthService
        {
            Response = new LoginResponse
            {
                Success = true,
                DebugJson = "{\"ok\":true}",
                User = new User
                {
                    Id = 7,
                    Username = "tester",
                    FullName = "Test User",
                    Role = "admin"
                }
            }
        };
        var userService = new FakeUserService();
        var credentialService = new FakeCredentialManagerService();
        var viewModel = new LoginViewModel(authService, userService, credentialService)
        {
            Username = "tester",
            Password = "secret",
            RememberMe = true
        };
        var loginSucceededRaised = false;
        viewModel.LoginSucceeded += () => loginSucceededRaised = true;

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsLoading);
        Assert.True(loginSucceededRaised);
        Assert.NotNull(userService.CurrentUser);
        Assert.Equal("tester", userService.CurrentUser!.Username);
        Assert.Equal("{\"ok\":true}", viewModel.DebugLoginResponse);
        Assert.Equal(1, credentialService.SaveCallCount);
        Assert.Equal(("tester", "secret"), credentialService.LastSavedCredentials);
        Assert.Equal(0, credentialService.DeleteCallCount);
        Assert.Equal(1, authService.LoginCallCount);
    }

    [Fact]
    public async Task LoginCommand_SetsErrorMessage_WhenAuthServiceReturnsFailure()
    {
        var authService = new FakeAuthService
        {
            Response = new LoginResponse
            {
                Success = false,
                Message = "Неверный логин или пароль"
            }
        };
        var userService = new FakeUserService();
        var credentialService = new FakeCredentialManagerService();
        var viewModel = new LoginViewModel(authService, userService, credentialService)
        {
            Username = "tester",
            Password = "wrong"
        };
        var loginSucceededRaised = false;
        viewModel.LoginSucceeded += () => loginSucceededRaised = true;

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsLoading);
        Assert.Equal("Неверный логин или пароль", viewModel.ErrorMessage);
        Assert.Null(userService.CurrentUser);
        Assert.False(loginSucceededRaised);
        Assert.Equal(0, credentialService.SaveCallCount);
        Assert.Equal(0, credentialService.DeleteCallCount);
    }

    [Fact]
    public async Task LoginCommand_CompletesSuccessfully_WhenCredentialStorageThrows()
    {
        var authService = new FakeAuthService
        {
            Response = new LoginResponse
            {
                Success = true,
                DebugJson = "{\"ok\":true}",
                User = new User
                {
                    Username = "tester"
                }
            }
        };
        var userService = new FakeUserService();
        var credentialService = new FakeCredentialManagerService
        {
            SaveException = new InvalidOperationException("storage unavailable")
        };
        var viewModel = new LoginViewModel(authService, userService, credentialService)
        {
            Username = "tester",
            Password = "secret",
            RememberMe = true
        };
        var loginSucceededRaised = false;
        viewModel.LoginSucceeded += () => loginSucceededRaised = true;

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsLoading);
        Assert.True(loginSucceededRaised);
        Assert.NotNull(userService.CurrentUser);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
        Assert.Equal(1, credentialService.SaveCallCount);
    }

    [Fact]
    public async Task LoginCommand_SetsErrorMessage_WhenAuthServiceThrows()
    {
        var authService = new FakeAuthService
        {
            LoginException = new InvalidOperationException("boom")
        };
        var userService = new FakeUserService();
        var credentialService = new FakeCredentialManagerService();
        var viewModel = new LoginViewModel(authService, userService, credentialService)
        {
            Username = "tester",
            Password = "secret"
        };

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsLoading);
        Assert.Contains("boom", viewModel.ErrorMessage);
        Assert.Null(userService.CurrentUser);
    }

    private sealed class FakeAuthService : IAuthService
    {
        public int LoginCallCount { get; private set; }
        public LoginResponse Response { get; set; } = new();
        public Exception? LoginException { get; set; }

        public Task<LoginResponse> LoginAsync(string username, string password)
        {
            LoginCallCount++;

            if (LoginException != null)
            {
                throw LoginException;
            }

            return Task.FromResult(Response);
        }

        public void Logout()
        {
        }
    }

    private sealed class FakeCredentialManagerService : ICredentialManagerService
    {
        public SavedCredentials? StoredCredentials { get; set; }
        public int SaveCallCount { get; private set; }
        public int DeleteCallCount { get; private set; }
        public (string Username, string Password)? LastSavedCredentials { get; private set; }
        public Exception? SaveException { get; set; }
        public Exception? DeleteException { get; set; }

        public SavedCredentials? TryGetCredentials()
        {
            return StoredCredentials;
        }

        public void SaveCredentials(string username, string password)
        {
            SaveCallCount++;
            LastSavedCredentials = (username, password);

            if (SaveException != null)
            {
                throw SaveException;
            }
        }

        public void DeleteCredentials()
        {
            DeleteCallCount++;

            if (DeleteException != null)
            {
                throw DeleteException;
            }
        }
    }

    private sealed class FakeUserService : IUserService
    {
        public User? CurrentUser { get; set; }
        public bool IsAuthenticated => CurrentUser != null;

        public void SetCurrentUser(User user)
        {
            CurrentUser = user;
        }

        public void ClearCurrentUser()
        {
            CurrentUser = null;
        }

        public bool HasRole(string role)
        {
            return CurrentUser?.Role?.Equals(role, StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
