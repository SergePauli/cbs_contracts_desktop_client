using System.Net;
using System.Text;
using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class FnsContragentServiceTests
{
    [Fact]
    public async Task SearchByReqAsync_LoadsFnsData_AndNormalizesContragent()
    {
        HttpRequestMessage? capturedRequest = null;
        var service = CreateService(
            new StubHttpMessageHandler(request =>
            {
                capturedRequest = request;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(FnsJson(), Encoding.UTF8, "application/json")
                });
            }),
            new StubReferenceLookupCacheService(
                [LookupItem("Area", "item", 101000L, "Москва", null)],
                [LookupItem("Ownership", "card", 7L, "ООО", "12300")]));

        var results = await service.SearchByReqAsync("7707083893", "770701001", "secret-key");

        Assert.NotNull(capturedRequest);
        Assert.Equal("http://localhost/egr?req=7707083893&key=secret-key", capturedRequest!.RequestUri!.ToString());

        var result = Assert.Single(results);
        Assert.Equal("7707083893", result.Organization.Inn);
        Assert.Equal("770701001", result.Organization.Kpp);
        Assert.Equal("ООО Ромашка", result.Organization.Name);
        Assert.Equal(7L, result.Organization.OwnershipId);
        Assert.Equal(101000L, result.Region?.Id);
        Assert.Equal("Москва", result.RealAddress.Value);
        Assert.Equal("статус: Действующее", result.Description);
        Assert.Contains(result.Contacts, contact => contact is { Type: "Email", Value: "info@example.com" });
        Assert.Contains(result.Contacts, contact => contact is { Type: "Phone", Value: "+7 495 000-00-00" });
        Assert.Contains(result.Contacts, contact => contact is { Type: "SiteUrl", Value: "https://example.com" });
    }

    [Fact]
    public async Task ReadDataAsync_WithKpp_AddsMatchingBranchResult()
    {
        var service = CreateService(
            new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))),
            new StubReferenceLookupCacheService(
                [LookupItem("Area", "item", 101000L, "Москва", null)],
                []));

        var response = JsonSerializer.Deserialize<FnsResponse>(FnsJson())!;

        var results = await service.ReadDataAsync(response, "770702002");

        Assert.Equal(2, results.Count);
        Assert.Equal("770701001", results[0].Organization.Kpp);
        Assert.Equal("770702002", results[1].Organization.Kpp);
        Assert.Equal("Филиал Ромашка", results[1].Organization.Name);
        Assert.Equal("Адрес филиала", results[1].RealAddress.Value);
    }

    [Fact]
    public void ToContragentPayload_BuildsRailsStyleNestedAttributes()
    {
        var result = new FnsContragentLookupResult
        {
            ObjUuid = "obj-1",
            RequisitesListKey = "req-1",
            Region = new FnsLookupItem
            {
                Id = 77,
                Name = "Москва"
            },
            Organization = new FnsContragentOrganization
            {
                Name = "ООО Ромашка",
                Inn = "7707083893",
                OwnershipId = 7
            },
            RealAddress = new FnsContragentAddress
            {
                Kind = "real",
                ListKey = "addr-1",
                Value = "Москва",
                AreaId = 77
            },
            RegisteredAddress = new FnsContragentAddress
            {
                Kind = "registred",
                ListKey = "addr-2",
                Value = "Москва",
                AreaId = 77
            },
            Contacts =
            [
                new FnsContactItem
                {
                    ListKey = "contact-1",
                    Type = "Email",
                    Value = "info@example.com"
                }
            ]
        };

        var payload = result.ToContragentPayload();

        Assert.Equal("obj-1", payload["obj_uuid"]);
        Assert.Equal(77L, payload["region_id"]);

        var requisites = Assert.IsAssignableFrom<IEnumerable<object?>>(payload["contragent_organizations_attributes"]);
        var requisitesItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(requisites));
        var organization = Assert.IsType<Dictionary<string, object?>>(requisitesItem["organization_attributes"]);
        Assert.Equal("req-1", requisitesItem["list_key"]);
        Assert.Equal("ООО Ромашка", organization["name"]);
        Assert.Equal(7L, organization["ownership_id"]);

        var contacts = Assert.IsAssignableFrom<IEnumerable<object?>>(payload["contragent_contacts_attributes"]);
        var contactItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(contacts));
        var contact = Assert.IsType<Dictionary<string, object?>>(contactItem["contact_attributes"]);
        Assert.Equal("Email", contact["type"]);
    }

    private static FnsContragentService CreateService(HttpMessageHandler handler, IReferenceLookupCacheService lookupCacheService)
    {
        return new FnsContragentService(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost/")
            },
            lookupCacheService);
    }

    private static ReferenceLookupItem LookupItem(string model, string preset, long id, string name, string? okopf)
    {
        var row = new ReferenceDataRow
        {
            Values =
            {
                ["id"] = JsonSerializer.SerializeToElement(id),
                ["name"] = JsonSerializer.SerializeToElement(name)
            }
        };

        if (!string.IsNullOrWhiteSpace(okopf))
        {
            row.Values["okopf"] = JsonSerializer.SerializeToElement(okopf);
        }

        return new ReferenceLookupItem
        {
            Model = model,
            Preset = preset,
            Id = id,
            Name = name,
            FullName = name,
            Code = okopf ?? string.Empty,
            Row = row
        };
    }

    private static string FnsJson()
    {
        return """
        {
          "items": [
            {
              "ЮЛ": {
                "ИНН": "7707083893",
                "КПП": "770701001",
                "ОГРН": "1027700132195",
                "КодОКОПФ": "12300",
                "Статус": "Действующее",
                "НаимСокрЮЛ": "ООО\nРомашка",
                "НаимПолнЮЛ": "Общество с ограниченной ответственностью Ромашка",
                "Адрес": {
                  "КодРегион": "77",
                  "Индекс": "101001",
                  "АдресПолн": "Москва"
                },
                "КодыСтат": {
                  "ОКПО": "12345678",
                  "ОКТМО": "45382000",
                  "ОКФС": "16",
                  "ОКОГУ": "4210014"
                },
                "Контакты": {
                  "Сайт": [ "https://example.com" ],
                  "e-mail": [ "info@example.com", "support@example.com" ],
                  "Телефон": "+7 495 000-00-00"
                },
                "Филиалы": [
                  {
                    "КПП": "770702002",
                    "Наименование": "Филиал Ромашка",
                    "Адрес": "Адрес филиала"
                  }
                ]
              }
            }
          ]
        }
        """;
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

    private sealed class StubReferenceLookupCacheService : IReferenceLookupCacheService
    {
        private readonly IReadOnlyList<ReferenceLookupItem> _regions;
        private readonly IReadOnlyList<ReferenceLookupItem> _ownerships;

        public StubReferenceLookupCacheService(
            IReadOnlyList<ReferenceLookupItem> regions,
            IReadOnlyList<ReferenceLookupItem> ownerships)
        {
            _regions = regions;
            _ownerships = ownerships;
        }

        public Task<IReadOnlyList<ReferenceLookupItem>> GetItemsAsync(
            string model,
            string? preset = null,
            CancellationToken cancellationToken = default)
        {
            if (string.Equals(model, "Area", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(_regions);
            }

            if (string.Equals(model, "Ownership", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(_ownerships);
            }

            return Task.FromResult<IReadOnlyList<ReferenceLookupItem>>([]);
        }

        public Task<IReadOnlyList<CbsTableFilterOptionDefinition>> GetOptionsAsync(
            string model,
            string? preset = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>([]);
        }

        public Task<ReferenceLookupItem?> FindByIdAsync(
            string model,
            object? id,
            string? preset = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ReferenceLookupItem?>(null);
        }

        public Task<ReferenceLookupItem?> FindOwnershipAsync(
            object? id,
            string? code,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ReferenceLookupItem?>(null);
        }

        public void Invalidate(string model, string? preset = null)
        {
        }
    }
}
