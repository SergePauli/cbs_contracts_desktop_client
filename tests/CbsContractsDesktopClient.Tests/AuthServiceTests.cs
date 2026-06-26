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
        Assert.Equal("refresh-token", response.User.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(response.DebugJson));
    }

    [Fact]
    public async Task LoginAsync_MapsProfileDisplayFields_WhenApiReturnsUserDetails()
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
                "email": "tester@example.com",
                "role": "admin",
                "person": {
                  "full_name": "Иванов Иван Иванович"
                },
                "department": {
                  "id": 7,
                  "name": "Отдел продаж"
                }
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
        Assert.Equal("tester", response.User.Username);
        Assert.Equal("Иванов Иван Иванович", response.User.FullName);
        Assert.Equal("tester@example.com", response.User.Email);
        Assert.Equal(7, response.User.DepartmentId);
        Assert.Equal("Отдел продаж", response.User.DepartmentName);
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

    [Fact]
    public async Task RefreshAccessTokenAsync_SendsRefreshBearerToken_AndUpdatesCurrentUserTokens()
    {
        HttpRequestMessage? capturedRequest = null;
        var userService = new StubUserService
        {
            CurrentUser = new User
            {
                Token = "expired-access-token",
                RefreshToken = "refresh-token"
            }
        };
        var service = CreateAuthService(
            new StubHttpMessageHandler(request =>
            {
                capturedRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"tokens":{"access":"new-access-token","refresh":"new-refresh-token"},"user":{"id":42,"name":"tester"}}""",
                        Encoding.UTF8,
                        "application/json")
                });
            }),
            userService);

        var token = await service.RefreshAccessTokenAsync("expired-access-token");

        Assert.Equal("new-access-token", token);
        Assert.Equal("new-access-token", userService.CurrentUser!.Token);
        Assert.Equal("new-refresh-token", userService.CurrentUser.RefreshToken);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal("http://localhost/auth/refresh", capturedRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("refresh-token", capturedRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_ClearsCurrentUser_WhenRefreshReturnsUnauthorized()
    {
        var userService = new StubUserService
        {
            CurrentUser = new User
            {
                Token = "expired-access-token",
                RefreshToken = "refresh-token"
            }
        };
        var service = CreateAuthService(
            new StubHttpMessageHandler(_ =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))),
            userService);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.RefreshAccessTokenAsync("expired-access-token"));

        Assert.Null(userService.CurrentUser);
    }

    private static AuthService CreateAuthService(HttpMessageHandler handler, IUserService? userService = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        return new AuthService(httpClient, userService ?? new StubUserService());
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
