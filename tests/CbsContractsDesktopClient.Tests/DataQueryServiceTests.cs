using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Services;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public class DataQueryServiceTests
{
    [Fact]
    public async Task GetDataAsync_PostsRequestToIndexEndpoint_AndReturnsTypedItems()
    {
        HttpRequestMessage? capturedRequest = null;
        const string json = """
            [
              { "id": 10, "name": "Анна" },
              { "id": 11, "name": "Сергей" }
            ]
            """;

        var service = CreateService(
            new StubUserService(),
            new StubHttpMessageHandler(request =>
            {
                capturedRequest = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }));

        var items = await service.GetDataAsync<TestRecord>(new DataQueryRequest
        {
            Model = "Employee",
            Preset = "card",
            Limit = 50,
            Offset = 0,
            Sorts = ["name asc"],
            Filters = new { used__eq = true }
        });

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("http://localhost/api/index", capturedRequest.RequestUri!.ToString());
        Assert.Collection(
            items,
            first =>
            {
                Assert.Equal(10, first.Id);
                Assert.Equal("Анна", first.Name);
            },
            second =>
            {
                Assert.Equal(11, second.Id);
                Assert.Equal("Сергей", second.Name);
            });
    }

    [Fact]
    public async Task GetCountAsync_NormalizesPrimitiveNumericResponse()
    {
        var service = CreateService(
            new StubUserService(),
            new StubHttpMessageHandler(_ =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("42", Encoding.UTF8, "application/json")
                })));

        var count = await service.GetCountAsync(new DataQueryRequest
        {
            Model = "Employee"
        });

        Assert.Equal(42, count);
    }

    [Fact]
    public async Task GetCountAsync_NormalizesObjectCountResponse_AndSendsBearerToken()
    {
        HttpRequestMessage? capturedRequest = null;
        DataQueryRequest? capturedPayload = null;
        var userService = new StubUserService
        {
            CurrentUser = new User
            {
                Token = "desktop-token"
            }
        };

        var service = CreateService(
            userService,
            new StubHttpMessageHandler(request =>
            {
                capturedRequest = request;
                capturedPayload = request.Content is null
                    ? null
                    : request.Content.ReadFromJsonAsync<DataQueryRequest>().GetAwaiter().GetResult();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "count": "17" }""", Encoding.UTF8, "application/json")
                });
            }));

        var count = await service.GetCountAsync(new DataQueryRequest
        {
            Model = "Contract",
            Preset = "edit",
            Filters = new { used__eq = true },
            Sorts = ["name asc"],
            Limit = 50,
            Offset = 0
        });

        Assert.Equal(17, count);
        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedPayload);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("desktop-token", capturedRequest.Headers.Authorization?.Parameter);
        Assert.Equal("http://localhost/api/count", capturedRequest.RequestUri!.ToString());
        Assert.Equal("Contract", capturedPayload!.Model);
        Assert.Equal("edit", capturedPayload.Preset);
        Assert.NotNull(capturedPayload.Filters);
        Assert.Null(capturedPayload.Sorts);
        Assert.Null(capturedPayload.Limit);
        Assert.Null(capturedPayload.Offset);
    }

    private static DataQueryService CreateService(IUserService userService, HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        return new DataQueryService(httpClient, userService);
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

    private sealed class TestRecord
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
