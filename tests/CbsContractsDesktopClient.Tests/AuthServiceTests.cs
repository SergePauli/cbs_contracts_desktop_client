using System.Net;
using System.Net.Http;
using System.Text;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Services;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_ReturnsSuccessfulResponse_WhenApiReturnsValidPayload()
    {
        var json = """
            {
              "tokens": {
                "access": "access-token",
                "refresh": "refresh-token"
              },
              "user": {
                "id": 42,
                "name": "tester",
                "role": "admin"
              }
            }
            """;

        var service = CreateAuthService(new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            })));

        var response = await service.LoginAsync("tester", "secret");

        Assert.True(response.Success);
        Assert.Equal("access-token", response.Token);
        Assert.NotNull(response.User);
        Assert.Equal(42, response.User.Id);
        Assert.Equal("tester", response.User.Username);
        Assert.Equal("admin", response.User.Role);
        Assert.False(string.IsNullOrWhiteSpace(response.DebugJson));
    }

    [Fact]
    public async Task LoginAsync_ReturnsHttpError_WhenApiReturnsNonSuccessStatusCode()
    {
        var service = CreateAuthService(new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))));

        var response = await service.LoginAsync("tester", "wrong-password");

        Assert.False(response.Success);
        Assert.Equal("Ошибка HTTP: Unauthorized", response.Message);
    }

    [Fact]
    public async Task LoginAsync_ReturnsDeserializationError_WhenUserIsMissing()
    {
        const string json = """{"tokens":{"access":"token"}}""";
        var service = CreateAuthService(new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            })));

        var response = await service.LoginAsync("tester", "secret");

        Assert.False(response.Success);
        Assert.Equal("Ошибка десериализации", response.Message);
        Assert.Equal(json, response.DebugJson);
    }

    [Fact]
    public async Task LoginAsync_ReturnsNetworkError_WhenHttpClientThrows()
    {
        var service = CreateAuthService(new StubHttpMessageHandler(_ =>
            throw new HttpRequestException("connection failed")));

        var response = await service.LoginAsync("tester", "secret");

        Assert.False(response.Success);
        Assert.Contains("Ошибка сети:", response.Message);
        Assert.Contains("connection failed", response.Message);
    }

    private static AuthService CreateAuthService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        return new AuthService(httpClient, new StubUserService());
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
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
