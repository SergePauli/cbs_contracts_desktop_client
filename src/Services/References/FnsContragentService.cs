using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class FnsContragentService : IFnsContragentService
    {
        private const string ActiveStatus = "Действующее";
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly IReferenceLookupCacheService _lookupCacheService;

        public FnsContragentService(HttpClient httpClient, IReferenceLookupCacheService lookupCacheService)
        {
            _httpClient = httpClient;
            _lookupCacheService = lookupCacheService;
        }

        public async Task<FnsResponse> GetByReqAsync(
            string req,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(req))
            {
                return new FnsResponse();
            }

            var keyResolution = ResolveApiKey(apiKey);
            if (string.IsNullOrWhiteSpace(keyResolution.Value))
            {
                ApiServiceBase.EmitExternalTrace(
                    "FNS HTTP GET skipped reason=missing-api-key payload=<none> keySource=<none> keyLength=0");
                DiagnosticsFileLogger.AppendFnsBlock(
                    "FNS REQUEST SKIPPED",
                    string.Join(
                        Environment.NewLine,
                        "method=GET",
                        "url=<not built>",
                        "payload=<none>",
                        "reason=missing-api-key",
                        "keySource=<none>",
                        "keyLength=0"));
                throw new InvalidOperationException("FNS API key is not configured. Set CBS_FNS_KEY or pass apiKey explicitly.");
            }

            var requestUri = $"egr?req={Uri.EscapeDataString(req.Trim())}&key={Uri.EscapeDataString(keyResolution.Value)}";
            var fullUrl = BuildFullUrl(requestUri);
            var displayUrl = BuildDisplayUrl(requestUri);

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            var requestLog = string.Join(
                Environment.NewLine,
                "method=GET",
                $"url={fullUrl}",
                $"urlRedacted={displayUrl}",
                "payload=<none>",
                $"baseAddress={_httpClient.BaseAddress}",
                $"defaultHeaders={FormatHeaders(_httpClient.DefaultRequestHeaders)}",
                $"requestHeaders={FormatHeaders(request.Headers)}",
                $"keySource={keyResolution.Source}",
                $"keyLength={keyResolution.Value.Length}",
                $"keyTail={GetKeyTail(keyResolution.Value)}");
            DiagnosticsFileLogger.AppendFnsBlock("FNS REQUEST", requestLog);
            ApiServiceBase.EmitExternalTrace(
                $"FNS HTTP GET url={displayUrl} method=GET payload=<none> defaultHeaders={FormatHeaders(_httpClient.DefaultRequestHeaders)} requestHeaders={FormatHeaders(request.Headers)} keySource={keyResolution.Source} keyLength={keyResolution.Value.Length} keyTail={GetKeyTail(keyResolution.Value)}");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseLog = string.Join(
                Environment.NewLine,
                "method=GET",
                $"url={fullUrl}",
                $"urlRedacted={displayUrl}",
                "payload=<none>",
                $"status={(int)response.StatusCode}",
                $"reason={response.ReasonPhrase}",
                $"responseHeaders={FormatHeaders(response.Headers)}",
                $"contentHeaders={FormatHeaders(response.Content.Headers)}",
                "body:",
                body);
            DiagnosticsFileLogger.AppendFnsBlock("FNS RESPONSE", responseLog);
            ApiServiceBase.EmitExternalTrace(
                $"FNS HTTP RESPONSE url={displayUrl} status={(int)response.StatusCode} reason={response.ReasonPhrase} responseHeaders={FormatHeaders(response.Headers)} contentHeaders={FormatHeaders(response.Content.Headers)} body={TruncateForTrace(body)}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"FNS HTTP {(int)response.StatusCode} ({response.StatusCode}). {body}".Trim(),
                    inner: null,
                    response.StatusCode);
            }

            return JsonSerializer.Deserialize<FnsResponse>(body, SerializerOptions) ?? new FnsResponse();
        }

        public async Task<IReadOnlyList<FnsContragentLookupResult>> SearchByReqAsync(
            string req,
            string? kpp = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            var response = await GetByReqAsync(req, apiKey, cancellationToken);
            return await ReadDataAsync(response, kpp, cancellationToken);
        }

        public async Task<IReadOnlyList<FnsContragentLookupResult>> ReadDataAsync(
            FnsResponse response,
            string? kpp = null,
            CancellationToken cancellationToken = default)
        {
            if (response.Items.Count == 0)
            {
                return [];
            }

            var regionsTask = _lookupCacheService.GetItemsAsync("Area", "item", cancellationToken);
            var ownershipsTask = _lookupCacheService.GetItemsAsync("Ownership", "card", cancellationToken);
            await Task.WhenAll(regionsTask, ownershipsTask);

            return NormalizeItems(
                response.Items,
                kpp,
                await regionsTask,
                await ownershipsTask);
        }

        private static IReadOnlyList<FnsContragentLookupResult> NormalizeItems(
            IReadOnlyList<FnsResponseItem> items,
            string? kpp,
            IReadOnlyList<ReferenceLookupItem> regions,
            IReadOnlyList<ReferenceLookupItem> ownerships)
        {
            var results = new List<FnsContragentLookupResult>();
            var legalItems = items.Where(static item => item.LegalEntity is not null).ToList();
            if (legalItems.Count == 0)
            {
                return results;
            }

            var last = legalItems[^1];
            foreach (var item in legalItems)
            {
                var legalEntity = item.LegalEntity!;
                if (!string.Equals(legalEntity.Status, ActiveStatus, StringComparison.OrdinalIgnoreCase)
                    && !ReferenceEquals(item, last))
                {
                    continue;
                }

                var baseResult = CreateResult(legalEntity, null, regions, ownerships);
                results.Add(baseResult);

                AddBranchResults(results, baseResult, legalEntity, kpp, regions, ownerships);
            }

            return results;
        }

        private static void AddBranchResults(
            List<FnsContragentLookupResult> results,
            FnsContragentLookupResult baseResult,
            FnsLegalEntity legalEntity,
            string? kpp,
            IReadOnlyList<ReferenceLookupItem> regions,
            IReadOnlyList<ReferenceLookupItem> ownerships)
        {
            if (legalEntity.Branches.Count == 0)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(kpp))
            {
                var branch = legalEntity.Branches.FirstOrDefault(branch =>
                    string.Equals(branch.Kpp, kpp.Trim(), StringComparison.OrdinalIgnoreCase));

                if (branch is not null)
                {
                    results.Add(CreateResult(legalEntity, branch, regions, ownerships));
                }

                return;
            }

            foreach (var branch in legalEntity.Branches)
            {
                results.Add(CreateResult(legalEntity, branch, regions, ownerships));
            }
        }

        private static FnsContragentLookupResult CreateResult(
            FnsLegalEntity legalEntity,
            FnsBranch? branch,
            IReadOnlyList<ReferenceLookupItem> regions,
            IReadOnlyList<ReferenceLookupItem> ownerships)
        {
            var address = NormalizeText(branch?.Address) ?? NormalizeText(legalEntity.Address?.FullAddress) ?? string.Empty;
            var region = ResolveRegion(legalEntity, regions);
            var ownership = ResolveOwnership(legalEntity.OkopfCode, ownerships);

            return new FnsContragentLookupResult
            {
                Organization = new FnsContragentOrganization
                {
                    Name = NormalizeText(branch?.Name) ?? NormalizeText(legalEntity.ShortName),
                    FullName = NormalizeText(branch?.Name) ?? NormalizeText(legalEntity.FullName),
                    Inn = NormalizeText(legalEntity.Inn),
                    Kpp = NormalizeText(branch?.Kpp) ?? NormalizeText(legalEntity.Kpp),
                    Okopf = NormalizeText(legalEntity.OkopfCode),
                    Ogrn = NormalizeText(legalEntity.Ogrn),
                    Okfc = NormalizeText(legalEntity.StatCodes?.Okfc),
                    Okogu = NormalizeText(legalEntity.StatCodes?.Okogu),
                    Okpo = NormalizeText(legalEntity.StatCodes?.Okpo),
                    Oktmo = NormalizeText(legalEntity.StatCodes?.Oktmo),
                    OwnershipId = ToLong(ownership?.Id),
                    OwnershipOkopf = NormalizeText(legalEntity.OkopfCode)
                },
                RealAddress = new FnsContragentAddress
                {
                    Kind = "real",
                    Value = address,
                    AreaId = region?.Id
                },
                RegisteredAddress = new FnsContragentAddress
                {
                    Kind = "registred",
                    Value = address,
                    AreaId = region?.Id
                },
                Region = region,
                Contacts = ExtractContacts(legalEntity.Contacts),
                Description = string.IsNullOrWhiteSpace(legalEntity.Status)
                    ? null
                    : $"статус: {NormalizeText(legalEntity.Status)}"
            };
        }

        private static IReadOnlyList<FnsContactItem> ExtractContacts(object? contacts)
        {
            if (contacts is not JsonElement element || element.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var results = new List<FnsContactItem>();
            AddFirstContact(results, element, "Сайт", "SiteUrl");
            AddFirstContact(results, element, "e-mail", "Email");
            AddFirstContact(results, element, "Телефон", "Phone");

            return results;
        }

        private static void AddFirstContact(List<FnsContactItem> results, JsonElement contacts, string propertyName, string contactType)
        {
            if (!contacts.TryGetProperty(propertyName, out var valueElement))
            {
                return;
            }

            var value = GetFirstString(valueElement);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            results.Add(new FnsContactItem
            {
                Value = value,
                Type = contactType
            });
        }

        private static string? GetFirstString(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => NormalizeText(element.GetString()),
                JsonValueKind.Array => element.EnumerateArray()
                    .Select(GetFirstString)
                    .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
                _ => null
            };
        }

        private static FnsLookupItem? ResolveRegion(FnsLegalEntity legalEntity, IReadOnlyList<ReferenceLookupItem> regions)
        {
            var regionCode = NormalizeText(legalEntity.Address?.RegionCode);
            if (string.IsNullOrWhiteSpace(regionCode))
            {
                return null;
            }

            var postalCode = ToLong(legalEntity.Address?.PostalCode);
            if (postalCode is not null)
            {
                var region = regions.FirstOrDefault(item =>
                {
                    var id = ToLong(item.Id);
                    return id is not null && postalCode.Value - id.Value < 100 && postalCode.Value > id.Value;
                });

                if (region is not null)
                {
                    return new FnsLookupItem
                    {
                        Id = ToLong(region.Id),
                        Name = region.DisplayName
                    };
                }
            }

            return new FnsLookupItem
            {
                Id = ToLong(regionCode),
                Name = string.Empty
            };
        }

        private static ReferenceLookupItem? ResolveOwnership(string? okopfCode, IReadOnlyList<ReferenceLookupItem> ownerships)
        {
            var normalizedOkopf = NormalizeText(okopfCode);
            if (string.IsNullOrWhiteSpace(normalizedOkopf))
            {
                return null;
            }

            return ownerships.FirstOrDefault(item =>
                string.Equals(NormalizeText(item.Code), normalizedOkopf, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeText(item.Row.GetValue("okopf")?.ToString()), normalizedOkopf, StringComparison.OrdinalIgnoreCase));
        }

        private static long? ToLong(object? value)
        {
            return value switch
            {
                null => null,
                long number => number,
                int number => number,
                string text when long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
                JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt64(out var number) => number,
                JsonElement { ValueKind: JsonValueKind.String } element when long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
                _ => null
            };
        }

        private static string? NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private string BuildDisplayUrl(string requestUri)
        {
            return RedactFnsKey(BuildFullUrl(requestUri));
        }

        private string BuildFullUrl(string requestUri)
        {
            return _httpClient.BaseAddress is null
                ? requestUri
                : new Uri(_httpClient.BaseAddress, requestUri).ToString();
        }

        private static string RedactFnsKey(string url)
        {
            const string marker = "key=";
            var keyIndex = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return url;
            }

            var valueStart = keyIndex + marker.Length;
            var valueEnd = url.IndexOf('&', valueStart);
            return valueEnd < 0
                ? $"{url[..valueStart]}<hidden>"
                : $"{url[..valueStart]}<hidden>{url[valueEnd..]}";
        }

        private static string FormatHeaders(System.Net.Http.Headers.HttpHeaders headers)
        {
            var values = headers
                .Select(static header => $"{header.Key}={string.Join("|", header.Value)}")
                .ToList();

            return values.Count == 0
                ? "<none>"
                : string.Join("; ", values);
        }

        private static string TruncateForTrace(string body)
        {
            const int maxLength = 20000;
            if (string.IsNullOrEmpty(body) || body.Length <= maxLength)
            {
                return body;
            }

            return $"{body[..maxLength]}... <truncated>";
        }

        private static string GetKeyTail(string value)
        {
            return value.Length <= 4
                ? "<short>"
                : value[^4..];
        }

        private static FnsApiKeyResolution ResolveApiKey(string? apiKey)
        {
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                return new FnsApiKeyResolution(apiKey.Trim(), "argument");
            }

            return ReadApiKey("CBS_FNS_KEY")
                ?? ReadApiKey("FNS_KEY")
                ?? ReadApiKey("REACT_APP_FNS_KEY")
                ?? new FnsApiKeyResolution(null, "<none>");
        }

        private static FnsApiKeyResolution? ReadApiKey(string name)
        {
            var processValue = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(processValue))
            {
                return new FnsApiKeyResolution(processValue.Trim(), $"{name}:process");
            }

            var userValue = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
            return string.IsNullOrWhiteSpace(userValue)
                ? null
                : new FnsApiKeyResolution(userValue.Trim(), $"{name}:user");
        }

        private sealed record FnsApiKeyResolution(string? Value, string Source);
    }
}
