using System.Net.Http;
using System.Net.Http.Headers;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public class LocalApiIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task AuthService_CanLoginAgainstLocalPrimaryApi()
    {
        var config = LocalIntegrationConfig.TryRead();
        if (config is null)
        {
            return;
        }

        using var httpClient = CreateJsonClient(config.PrimaryApiUri);
        var userService = new StubUserService();
        var service = new AuthService(httpClient, userService);

        var response = await service.LoginAsync(config.Username, config.Password);

        Assert.True(response.Success, response.Message);
        Assert.NotNull(response.User);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.True(response.User!.Id >= 0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DataQueryService_CanReadStatusItemsAgainstLocalDataApi()
    {
        var config = LocalIntegrationConfig.TryRead();
        if (config is null)
        {
            return;
        }

        var authUserService = new StubUserService();
        using var authHttpClient = CreateJsonClient(config.PrimaryApiUri);
        var authService = new AuthService(authHttpClient, authUserService);
        var login = await authService.LoginAsync(config.Username, config.Password);

        Assert.True(login.Success, login.Message);
        Assert.NotNull(login.User);
        Assert.False(string.IsNullOrWhiteSpace(login.User!.Token));

        var queryUserService = new StubUserService();
        queryUserService.SetCurrentUser(login.User);

        using var dataHttpClient = CreateJsonClient(config.DataApiUri);
        var dataQueryService = new DataQueryService(dataHttpClient, queryUserService);

        var items = await dataQueryService.GetDataAsync<StatusItem>(
            new DataQueryRequest
            {
                Model = "Status",
                Preset = "item",
                Sorts = ["id asc"],
                Limit = 20,
                Offset = 0
            });

        Assert.NotEmpty(items);
        Assert.All(items, item =>
        {
            Assert.True(item.Id >= 0);
            Assert.False(string.IsNullOrWhiteSpace(item.Name));
        });
    }

    private static HttpClient CreateJsonClient(Uri baseUri)
    {
        var client = new HttpClient
        {
            BaseAddress = baseUri
        };
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private sealed class LocalIntegrationConfig
    {
        public required Uri PrimaryApiUri { get; init; }

        public required Uri DataApiUri { get; init; }

        public required string Username { get; init; }

        public required string Password { get; init; }

        public static LocalIntegrationConfig? TryRead()
        {
            var username = Environment.GetEnvironmentVariable("CBS_TEST_USERNAME") ?? "admin";
            var password = Environment.GetEnvironmentVariable("CBS_TEST_PASSWORD") ?? "1235";

            var primaryApiUrl = Environment.GetEnvironmentVariable("CBS_TEST_PRIMARY_API_URL") ?? "http://serge-lenovo:5000/";
            var dataApiUrl = Environment.GetEnvironmentVariable("CBS_TEST_DATA_API_URL") ?? "http://serge-lenovo:8080/";

            return new LocalIntegrationConfig
            {
                PrimaryApiUri = new Uri(primaryApiUrl, UriKind.Absolute),
                DataApiUri = new Uri(dataApiUrl, UriKind.Absolute),
                Username = username,
                Password = password
            };
        }
    }

    private sealed class StubUserService : IUserService
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
