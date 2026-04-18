using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ReferenceCrudServiceTests
{
    [Fact]
    public void BuildRequest_CreatePayload_UsesRailsStyleEnvelope()
    {
        var request = BuildRequest(
            CreateDefinition(),
            new Dictionary<string, object?>
            {
                ["name"] = "Новая запись"
            });

        Assert.Equal("card", request["data_set"]);

        var payload = Assert.IsType<Dictionary<string, object?>>(request["Status"]);
        Assert.Equal("Новая запись", payload["name"]);
    }

    [Fact]
    public async Task CreateAsync_PostsToAddEndpoint_AndParsesResponse()
    {
        HttpRequestMessage? capturedRequest = null;

        var service = CreateService(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "id": 12, "name": "Новая запись" }""", Encoding.UTF8, "application/json")
            });
        }));

        var result = await service.CreateAsync(
            CreateDefinition(),
            new Dictionary<string, object?>
            {
                ["name"] = "Новая запись"
            });

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("http://localhost/model/add/Status", capturedRequest.RequestUri!.ToString());
        Assert.Equal(12L, result.GetValue("id"));
        Assert.Equal("Новая запись", result.GetValue("name"));
    }

    [Fact]
    public void BuildRequest_UpdatePayload_UsesRailsStyleEnvelope()
    {
        var request = BuildRequest(
            CreateDefinition(),
            new Dictionary<string, object?>
            {
                ["id"] = 15L,
                ["name"] = "Новое имя",
                ["used"] = false
            });

        Assert.Equal("card", request["data_set"]);

        var payload = Assert.IsType<Dictionary<string, object?>>(request["Status"]);
        Assert.Equal(15L, payload["id"]);
        Assert.Equal("Новое имя", payload["name"]);
        Assert.Equal(false, payload["used"]);
    }

    [Fact]
    public async Task UpdateAsync_PutsRailsStyleEnvelope_ToRecordEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;

        var service = CreateService(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "id": 15, "name": "Новое имя", "used": false }""", Encoding.UTF8, "application/json")
            });
        }));

        var result = await service.UpdateAsync(
            CreateDefinition(),
            new Dictionary<string, object?>
            {
                ["id"] = 15L,
                ["name"] = "Новое имя",
                ["used"] = false
            });

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Put, capturedRequest!.Method);
        Assert.Equal("http://localhost/model/Status/15", capturedRequest.RequestUri!.ToString());
        Assert.False((bool?)result.GetValue("used") ?? true);
        Assert.Equal("Новое имя", result.GetValue("name"));
    }

    [Fact]
    public async Task DeleteAsync_DeletesRecordEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;

        var service = CreateService(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "id": 21, "name": "Удаленная запись" }""", Encoding.UTF8, "application/json")
            });
        }));

        var result = await service.DeleteAsync(CreateDefinition(), 21);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Delete, capturedRequest!.Method);
        Assert.Equal("http://localhost/model/Status/21", capturedRequest.RequestUri!.ToString());
        Assert.Equal(21L, result.GetValue("id"));
    }

    private static Dictionary<string, object?> BuildRequest(
        ReferenceDefinition definition,
        IReadOnlyDictionary<string, object?> payload)
    {
        var method = typeof(ReferenceCrudService).GetMethod(
            "BuildRequest",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method!.Invoke(null, [definition, payload]);
        return Assert.IsType<Dictionary<string, object?>>(result);
    }

    private static ReferenceCrudService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        return new ReferenceCrudService(httpClient, new StubUserService());
    }

    private static ReferenceDefinition CreateDefinition()
    {
        return new ReferenceDefinition
        {
            Route = "/references/Status",
            Model = "Status",
            Title = "Статусы",
            Preset = "card"
        };
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

        public bool IsAuthenticated => CurrentUser is not null;

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
