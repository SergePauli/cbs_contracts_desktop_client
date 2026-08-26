using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ModelMutationServiceTests
{
    [Fact]
    public void BuildRequest_CreatePayload_UsesRailsStyleEnvelope()
    {
        var request = BuildRequest(
            "Status",
            new Dictionary<string, object?>
            {
                ["name"] = "Новая запись"
            });

        Assert.Equal("item", request["data_set"]);

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
            "Status",
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
            "Status",
            new Dictionary<string, object?>
            {
                ["id"] = 15L,
                ["name"] = "Новое имя",
                ["used"] = false
            });

        Assert.Equal("item", request["data_set"]);

        var payload = Assert.IsType<Dictionary<string, object?>>(request["Status"]);
        Assert.Equal(15L, payload["id"]);
        Assert.Equal("Новое имя", payload["name"]);
        Assert.Equal(false, payload["used"]);
    }

    [Fact]
    public void BuildRequest_KeepsCommentsAttributesInsideModelPayload()
    {
        var comments = new[]
        {
            new Dictionary<string, object?>
            {
                ["content"] = "comment",
                ["profile_id"] = 7
            }
        };

        var request = BuildRequest(
            "Status",
            new Dictionary<string, object?>
            {
                ["id"] = 15L,
                ["name"] = "РќРѕРІРѕРµ РёРјСЏ",
                ["comments_attributes"] = comments
            });

        Assert.False(request.ContainsKey("comments"));
        var payload = Assert.IsType<Dictionary<string, object?>>(request["Status"]);
        Assert.Same(comments, payload["comments_attributes"]);
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
            "Status",
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
    public async Task UpdateAsync_FinStageAndContractPayloads_MatchExpectedJsonContracts()
    {
        var capturedRequests = new List<(HttpMethod Method, string Uri, string? Body)>();

        var service = CreateService(new StubHttpMessageHandler(async request =>
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync();
            capturedRequests.Add((request.Method, request.RequestUri!.ToString(), body));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "id": 1 }""", Encoding.UTF8, "application/json")
            };
        }));
        var stage = StageEditState.FromRow(CreateRow(
            ("id", 6388L),
            ("list_key", "1fc89df1-8229-4c1d-8046-c4fd1311f4a4"),
            ("name", "05/26/002"),
            ("priority", 1),
            ("used", true),
            ("cost", 124454m),
            ("status.id", 2L),
            ("status.name", "В работе"),
            ("task_kind.id", 1L),
            ("task_kind.name", "Контрольная проверка"),
            ("task_kind.code", "01"),
            ("payment_at", "Fri Jun 19 2026"),
            ("prepayment_at", "Wed Jun 17 2026"),
            ("invoice_at", "Wed Jun 17 2026"),
            ("funded_at", "Fri Jun 19 2026"),
            ("is_funded", false),
            ("start_at", "Fri Jun 19 2026"),
            ("deadline_at", "Wed Jul 01 2026"),
            ("payment_deadline_at", "Sun Jul 05 2026"),
            ("payment_deadline_kind", "w_days"),
            ("payment_duration", 12),
            ("duration", 12)));
        var contract = ContractEditState.FromRow(CreateRow(
            ("id", 6367L),
            ("name", "05/26/002"),
            ("status_id", 5L),
            ("status.name", "Подписан"),
            ("task_kind.name", "Контрольная проверка"),
            ("external_number", "аварп"),
            ("contract_responsibles", Array.Empty<object>())));

        stage.PaymentAt = new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);
        stage.InvoiceAt = new DateTimeOffset(2026, 6, 18, 0, 0, 0, TimeSpan.Zero);
        stage.FundedAt = new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);
        stage.IsFunded = true;
        stage.PaymentDeadlineAt = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero);

        await service.UpdateAsync(
            "Stage",
            StageFinEditPayloadBuilder.BuildForUpdate(stage, string.Empty, null));
        await service.UpdateAsync(
            "Contract",
            contract!.BuildExternalNumberPayload("аварп4"));

        Assert.Collection(
            capturedRequests,
            stageRequest =>
            {
                Assert.Equal(HttpMethod.Put, stageRequest.Method);
                Assert.Equal("http://localhost/model/Stage/6388", stageRequest.Uri);
                Assert.Equal(
                    """{"data_set":"item","Stage":{"id":6388,"list_key":"1fc89df1-8229-4c1d-8046-c4fd1311f4a4","payment_at":"Sat Jun 20 2026","invoice_at":"Thu Jun 18 2026","funded_at":"Sat Jun 20 2026","is_funded":true,"payment_deadline_at":"Mon Jul 06 2026"}}""",
                    stageRequest.Body);
            },
            contractRequest =>
            {
                Assert.Equal(HttpMethod.Put, contractRequest.Method);
                Assert.Equal("http://localhost/model/Contract/6367", contractRequest.Uri);
                Assert.Equal(
                    """{"data_set":"item","Contract":{"id":6367,"external_number":"\u0430\u0432\u0430\u0440\u043F4"}}""",
                    contractRequest.Body);
            });
    }

    [Fact]
    public async Task UpdateAsync_StagePayloadRejectsReadModelComments()
    {
        var service = CreateService(new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP should not be called.")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(
                "Stage",
                new Dictionary<string, object?>
                {
                    ["id"] = 15L,
                    ["comments"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["id"] = 35194L,
                            ["content"] = "comment"
                        }
                    }
                }));

        Assert.Contains("comments", exception.Message);
        Assert.Contains("comments_attributes", exception.Message);
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

        var result = await service.DeleteAsync("Status", 21);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Delete, capturedRequest!.Method);
        Assert.Equal("http://localhost/model/Status/21", capturedRequest.RequestUri!.ToString());
        Assert.Equal(21L, result.GetValue("id"));
    }

    private static Dictionary<string, object?> BuildRequest(
        string model,
        IReadOnlyDictionary<string, object?> payload)
    {
        var method = typeof(ModelMutationService).GetMethod(
            "BuildRequest",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method!.Invoke(null, [model, payload]);
        return Assert.IsType<Dictionary<string, object?>>(result);
    }

    private static ModelMutationService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        return new ModelMutationService(httpClient, new StubUserService());
    }

    private static TableDataRow CreateRow(params (string Key, object? Value)[] values)
    {
        return new TableDataRow
        {
            Values = values.ToDictionary(
                static value => value.Key,
                static value => JsonSerializer.SerializeToElement(value.Value))
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

