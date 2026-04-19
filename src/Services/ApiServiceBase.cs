using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CbsContractsDesktopClient.Services
{
    public abstract class ApiServiceBase
    {
        private static readonly bool DiagnosticsEnabled = true;
        public static event Action<string>? TraceEmitted;
        private static readonly TimeSpan DiagnosticRequestTimeout = TimeSpan.FromSeconds(15);

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;
        private readonly IUserService _userService;

        protected ApiServiceBase(HttpClient httpClient, IUserService userService)
        {
            _httpClient = httpClient;
            _userService = userService;
        }

        protected JsonSerializerOptions SerializerOptions => JsonOptions;

        protected async Task<TResponse> PutAsync<TRequest, TResponse>(string requestUri, TRequest request, CancellationToken cancellationToken = default)
        {
            EmitTrace(FormatRequestTrace("HTTP PUT", requestUri, request));
            using var message = CreateJsonRequest(HttpMethod.Put, requestUri, request);
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            EmitTrace($"HTTP RESPONSE uri={requestUri} body={TruncateForTrace(body)}");
            var result = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
            if (result is null)
            {
                throw new InvalidOperationException($"Ответ '{requestUri}' не удалось десериализовать в {typeof(TResponse).Name}.");
            }

            return result;
        }

        protected async Task<TResponse> DeleteAsync<TResponse>(string requestUri, CancellationToken cancellationToken = default)
        {
            EmitTrace($"HTTP DELETE uri={requestUri}");
            using var message = CreateRequest(HttpMethod.Delete, requestUri);
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            EmitTrace($"HTTP RESPONSE uri={requestUri} body={TruncateForTrace(body)}");
            var result = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
            if (result is null)
            {
                throw new InvalidOperationException($"Ответ '{requestUri}' не удалось десериализовать в {typeof(TResponse).Name}.");
            }

            return result;
        }

        protected async Task<TResponse> PostAsync<TRequest, TResponse>(string requestUri, TRequest request, CancellationToken cancellationToken = default)
        {
            using var message = CreatePostRequest(requestUri, request);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DiagnosticRequestTimeout);
            EmitTrace(FormatRequestTrace("HTTP POST", requestUri, request));
            EmitTrace($"STEP API 01 before-send uri={requestUri} timeout={DiagnosticRequestTimeout.TotalSeconds:0}s");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(
                    message,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                EmitTrace($"STEP API 01 timeout uri={requestUri} timeout={DiagnosticRequestTimeout.TotalSeconds:0}s");
                throw new TimeoutException(
                    $"HTTP request '{requestUri}' timed out after {DiagnosticRequestTimeout.TotalSeconds:0} seconds.",
                    ex);
            }
            catch (Exception ex)
            {
                EmitTrace($"STEP API 01 error uri={requestUri} type={ex.GetType().Name} message={ex.Message}");
                throw;
            }

            using var _ = response;
            EmitTrace($"STEP API 02 after-headers uri={requestUri} status={(int)response.StatusCode}");
            EmitTrace($"STEP API 03 before-ensure-success uri={requestUri}");
            await EnsureSuccessAsync(response, cancellationToken);
            EmitTrace($"STEP API 04 after-ensure-success uri={requestUri}");

            EmitTrace($"STEP API 05 before-read-json uri={requestUri}");
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            EmitTrace($"HTTP RESPONSE uri={requestUri} body={TruncateForTrace(body)}");
            var result = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
            EmitTrace($"STEP API 06 after-read-json uri={requestUri} isNull={(result is null ? "true" : "false")}");
            if (result is null)
            {
                throw new InvalidOperationException($"Ответ '{requestUri}' не удалось десериализовать в {typeof(TResponse).Name}.");
            }

            EmitTrace($"STEP API 07 return uri={requestUri}");
            return result;
        }

        protected async Task<JsonElement> PostForJsonAsync<TRequest>(string requestUri, TRequest request, CancellationToken cancellationToken = default)
        {
            EmitTrace(FormatRequestTrace("HTTP POST JSON", requestUri, request));
            using var message = CreatePostRequest(requestUri, request);
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            EmitTrace($"HTTP RESPONSE uri={requestUri} body={TruncateForTrace(body)}");
            var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }

        private HttpRequestMessage CreatePostRequest<TRequest>(string requestUri, TRequest request)
        {
            return CreateJsonRequest(HttpMethod.Post, requestUri, request);
        }

        private HttpRequestMessage CreateJsonRequest<TRequest>(HttpMethod method, string requestUri, TRequest request)
        {
            var message = CreateRequest(method, requestUri);
            message.Content = JsonContent.Create(request, options: JsonOptions);
            return message;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string requestUri)
        {
            var message = new HttpRequestMessage(method, requestUri)
            {
            };

            var token = _userService.CurrentUser?.Token;
            if (!string.IsNullOrWhiteSpace(token))
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return message;
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} ({response.StatusCode}). {body}".Trim(),
                inner: null,
                response.StatusCode);
        }

        protected static void EmitTrace(string message)
        {
            if (!DiagnosticsEnabled)
            {
                return;
            }

            TraceEmitted?.Invoke(message);
        }

        private static string SerializeForTrace<TRequest>(TRequest request)
        {
            try
            {
                return JsonSerializer.Serialize(request, JsonOptions);
            }
            catch (Exception ex)
            {
                return $"<serialization failed: {ex.GetType().Name}: {ex.Message}>";
            }
        }

        private static string FormatRequestTrace<TRequest>(string verb, string requestUri, TRequest request)
        {
            return ShouldSuppressRequestPayload(requestUri)
                ? $"{verb} uri={requestUri}"
                : $"{verb} uri={requestUri} payload={SerializeForTrace(request)}";
        }

        private static bool ShouldSuppressRequestPayload(string requestUri)
        {
            return string.Equals(requestUri, "api/index", StringComparison.OrdinalIgnoreCase)
                || string.Equals(requestUri, "api/count", StringComparison.OrdinalIgnoreCase);
        }

        private static string TruncateForTrace(string body)
        {
            const int maxLength = 4000;
            if (string.IsNullOrEmpty(body) || body.Length <= maxLength)
            {
                return body;
            }

            return $"{body[..maxLength]}... <truncated>";
        }
    }
}
