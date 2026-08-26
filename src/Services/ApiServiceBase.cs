using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
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
        private readonly IAccessTokenRefreshService? _accessTokenRefreshService;

        protected ApiServiceBase(
            HttpClient httpClient,
            IUserService userService,
            IAccessTokenRefreshService? accessTokenRefreshService = null)
        {
            _httpClient = httpClient;
            _userService = userService;
            _accessTokenRefreshService = accessTokenRefreshService;
        }

        protected JsonSerializerOptions SerializerOptions => JsonOptions;

        protected async Task<TResponse> PutAsync<TRequest, TResponse>(string requestUri, TRequest request, CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DiagnosticRequestTimeout);
            var requestPayload = SerializeForTrace(request);
            EmitTrace(FormatRequestTrace("HTTP PUT", requestUri, request));
            using var response = await SendAsyncWithAccessTokenRefreshAsync(
                () => CreateJsonRequest(HttpMethod.Put, requestUri, request),
                requestUri,
                requestPayload,
                HttpCompletionOption.ResponseContentRead,
                timeoutCts.Token,
                cancellationToken);
            await EnsureSuccessAsync(response, requestUri, requestPayload, timeoutCts.Token, cancellationToken);

            var body = await ReadResponseBodyAsync(
                response,
                requestUri,
                requestPayload,
                timeoutCts.Token,
                cancellationToken,
                "STEP API PUT");
            EmitResponseTrace(requestUri, body);
            var result = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
            if (result is null)
            {
                throw new InvalidOperationException($"Ответ '{requestUri}' не удалось десериализовать в {typeof(TResponse).Name}.");
            }

            return result;
        }

        protected async Task<TResponse> DeleteAsync<TResponse>(string requestUri, CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DiagnosticRequestTimeout);
            const string requestPayload = "<empty>";
            EmitTrace($"HTTP DELETE uri={requestUri}");
            using var response = await SendAsyncWithAccessTokenRefreshAsync(
                () => CreateRequest(HttpMethod.Delete, requestUri),
                requestUri,
                requestPayload,
                HttpCompletionOption.ResponseContentRead,
                timeoutCts.Token,
                cancellationToken);
            await EnsureSuccessAsync(response, requestUri, requestPayload, timeoutCts.Token, cancellationToken);

            var body = await ReadResponseBodyAsync(
                response,
                requestUri,
                requestPayload,
                timeoutCts.Token,
                cancellationToken,
                "STEP API DELETE");
            EmitResponseTrace(requestUri, body);
            var result = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
            if (result is null)
            {
                throw new InvalidOperationException($"Ответ '{requestUri}' не удалось десериализовать в {typeof(TResponse).Name}.");
            }

            return result;
        }

        protected async Task<TResponse> PostAsync<TRequest, TResponse>(string requestUri, TRequest request, CancellationToken cancellationToken = default)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DiagnosticRequestTimeout);
            var requestPayload = SerializeForTrace(request);
            EmitTrace(FormatRequestTrace("HTTP POST", requestUri, request));
            EmitTrace($"STEP API 01 before-send uri={requestUri} timeout={DiagnosticRequestTimeout.TotalSeconds:0}s");

            HttpResponseMessage response;
            try
            {
                response = await SendAsyncWithAccessTokenRefreshAsync(
                    () => CreatePostRequest(requestUri, request),
                    requestUri,
                    requestPayload,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
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
            await EnsureSuccessAsync(response, requestUri, requestPayload, timeoutCts.Token, cancellationToken);
            EmitTrace($"STEP API 04 after-ensure-success uri={requestUri}");

            EmitTrace($"STEP API 05 before-read-json uri={requestUri}");
            var body = await ReadResponseBodyAsync(
                response,
                requestUri,
                requestPayload,
                timeoutCts.Token,
                cancellationToken,
                "STEP API 05");
            EmitResponseTrace(requestUri, body);
            LogSuccessfulResponseBody(requestUri, request, body);
            TResponse? result;
            try
            {
                result = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
            }
            catch (Exception ex)
            {
                LogApiBodyFailure(
                    "API RESPONSE DESERIALIZATION FAILED",
                    requestUri,
                    requestPayload,
                    response,
                    body,
                    ex);
                EmitTrace($"STEP API 06 deserialize-error uri={requestUri} type={ex.GetType().Name} message={ex.Message}");
                throw;
            }

            EmitTrace($"STEP API 06 after-read-json uri={requestUri} isNull={(result is null ? "true" : "false")}");
            if (result is null)
            {
                var exception = new InvalidOperationException(
                    $"Ответ '{requestUri}' не удалось десериализовать в {typeof(TResponse).Name}.");
                LogApiBodyFailure(
                    "API RESPONSE DESERIALIZATION FAILED",
                    requestUri,
                    requestPayload,
                    response,
                    body,
                    exception);
                throw new InvalidOperationException($"Ответ '{requestUri}' не удалось десериализовать в {typeof(TResponse).Name}.");
            }

            EmitTrace($"STEP API 07 return uri={requestUri}");
            return result;
        }

        protected async Task<JsonElement> PostForJsonAsync<TRequest>(string requestUri, TRequest request, CancellationToken cancellationToken = default)
        {
            EmitTrace(FormatRequestTrace("HTTP POST JSON", requestUri, request));
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(DiagnosticRequestTimeout);
            var requestPayload = SerializeForTrace(request);
            using var response = await SendAsyncWithAccessTokenRefreshAsync(
                () => CreatePostRequest(requestUri, request),
                requestUri,
                requestPayload,
                HttpCompletionOption.ResponseContentRead,
                timeoutCts.Token,
                cancellationToken);
            await EnsureSuccessAsync(response, requestUri, requestPayload, timeoutCts.Token, cancellationToken);

            var body = await ReadResponseBodyAsync(
                response,
                requestUri,
                requestPayload,
                timeoutCts.Token,
                cancellationToken,
                "STEP API JSON");
            EmitResponseTrace(requestUri, body);
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (Exception ex)
            {
                LogApiBodyFailure(
                    "API JSON RESPONSE PARSE FAILED",
                    requestUri,
                    requestPayload,
                    response,
                    body,
                    ex);
                throw;
            }

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

        private async Task<HttpResponseMessage> SendAsyncWithAccessTokenRefreshAsync(
            Func<HttpRequestMessage> createMessage,
            string requestUri,
            string requestPayload,
            HttpCompletionOption completionOption,
            CancellationToken timeoutToken,
            CancellationToken cancellationToken)
        {
            var attemptedAccessToken = _userService.CurrentUser?.Token ?? string.Empty;
            using var message = createMessage();
            EmitApiSend(message.Method, requestUri, requestPayload);
            HttpResponseMessage response;
            try
            {
                response = await SendAsyncWithWatchdog(
                    message,
                    requestUri,
                    requestPayload,
                    completionOption,
                    timeoutToken,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                EmitApiCanceled(message.Method, requestUri, requestPayload);
                throw;
            }

            if (!ShouldRefreshAccessToken(response, requestUri, attemptedAccessToken))
            {
                return response;
            }

            response.Dispose();
            EmitTrace($"HTTP 401 uri={requestUri} action=refresh-access-token");
            await _accessTokenRefreshService!.RefreshAccessTokenAsync(attemptedAccessToken, cancellationToken);

            using var retryMessage = createMessage();
            EmitApiSend(retryMessage.Method, requestUri, requestPayload);
            try
            {
                return await SendAsyncWithWatchdog(
                    retryMessage,
                    requestUri,
                    requestPayload,
                    completionOption,
                    timeoutToken,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                EmitApiCanceled(retryMessage.Method, requestUri, requestPayload);
                throw;
            }
        }

        private bool ShouldRefreshAccessToken(HttpResponseMessage response, string requestUri, string attemptedAccessToken)
        {
            return response.StatusCode == HttpStatusCode.Unauthorized
                && _accessTokenRefreshService is not null
                && !string.IsNullOrWhiteSpace(attemptedAccessToken)
                && !IsAuthEndpoint(requestUri);
        }

        private static bool IsAuthEndpoint(string requestUri)
        {
            return requestUri.StartsWith("auth/login", StringComparison.OrdinalIgnoreCase)
                || requestUri.StartsWith("auth/refresh", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<HttpResponseMessage> SendAsyncWithWatchdog(
            HttpRequestMessage message,
            string requestUri,
            string requestPayload,
            HttpCompletionOption completionOption,
            CancellationToken timeoutToken,
            CancellationToken cancellationToken)
        {
            var sendTask = _httpClient.SendAsync(message, completionOption, timeoutToken);
            var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, timeoutToken);

            var completedTask = await Task.WhenAny(sendTask, timeoutTask);
            if (ReferenceEquals(completedTask, sendTask))
            {
                return await sendTask;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            EmitTrace($"STEP API 01 timeout-send-watchdog uri={requestUri} timeout={DiagnosticRequestTimeout.TotalSeconds:0}s");
            var timeoutException = new TimeoutException(
                $"HTTP request '{requestUri}' did not receive response headers after {DiagnosticRequestTimeout.TotalSeconds:0} seconds.");
            DiagnosticsFileLogger.AppendBlock(
                "API SEND WATCHDOG TIMEOUT",
                $"uri={requestUri}{Environment.NewLine}"
                + $"method={message.Method.Method}{Environment.NewLine}"
                + $"completion={completionOption}{Environment.NewLine}"
                + $"request={requestPayload}{Environment.NewLine}"
                + $"error={timeoutException.GetType().Name}: {timeoutException.Message}");
            ObserveAbandonedSendTask(sendTask);
            throw timeoutException;
        }

        private static async Task EnsureSuccessAsync(
            HttpResponseMessage response,
            string requestUri,
            string requestPayload,
            CancellationToken timeoutToken,
            CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await ReadResponseBodyAsync(
                response,
                requestUri,
                requestPayload,
                timeoutToken,
                cancellationToken,
                "STEP API ERROR");
            var exception = new HttpRequestException(
                $"HTTP {(int)response.StatusCode} ({response.StatusCode}). {body}".Trim(),
                inner: null,
                response.StatusCode);
            EmitTrace($"HTTP ERROR uri={requestUri} status={(int)response.StatusCode} ({response.StatusCode})");
            LogApiBodyFailure(
                "API HTTP ERROR",
                requestUri,
                requestPayload,
                response,
                body,
                exception);
            throw exception;
        }

        private static async Task<string> ReadResponseBodyAsync(
            HttpResponseMessage response,
            string requestUri,
            string requestPayload,
            CancellationToken timeoutToken,
            CancellationToken cancellationToken,
            string traceStep)
        {
            var readTask = response.Content.ReadAsStringAsync(timeoutToken);
            var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, timeoutToken);

            try
            {
                var completedTask = await Task.WhenAny(readTask, timeoutTask);
                if (!ReferenceEquals(completedTask, readTask))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    EmitTrace($"{traceStep} timeout-read-json-watchdog uri={requestUri} timeout={DiagnosticRequestTimeout.TotalSeconds:0}s");
                    var timeoutException = new TimeoutException(
                        $"HTTP response body '{requestUri}' timed out after {DiagnosticRequestTimeout.TotalSeconds:0} seconds.");
                    LogApiBodyFailure(
                        "API RESPONSE BODY READ WATCHDOG TIMEOUT",
                        requestUri,
                        requestPayload,
                        response,
                        "<response body read task did not complete before timeout>",
                        timeoutException);
                    ObserveAbandonedReadTask(readTask);
                    throw timeoutException;
                }

                return await readTask;
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                EmitTrace($"{traceStep} timeout-read-json uri={requestUri} timeout={DiagnosticRequestTimeout.TotalSeconds:0}s");
                LogApiBodyFailure(
                    "API RESPONSE BODY READ TIMEOUT",
                    requestUri,
                    requestPayload,
                    response,
                    "<response body was not read before timeout>",
                    ex);
                throw new TimeoutException(
                    $"HTTP response body '{requestUri}' timed out after {DiagnosticRequestTimeout.TotalSeconds:0} seconds.",
                    ex);
            }
            catch (Exception ex)
            {
                EmitTrace($"{traceStep} read-json-error uri={requestUri} type={ex.GetType().Name} message={ex.Message}");
                LogApiBodyFailure(
                    "API RESPONSE BODY READ FAILED",
                    requestUri,
                    requestPayload,
                    response,
                    "<response body read failed>",
                    ex);
                throw;
            }
        }

        private static void ObserveAbandonedReadTask(Task<string> readTask)
        {
            _ = readTask.ContinueWith(
                static task =>
                {
                    _ = task.Exception;
                },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        private static void ObserveAbandonedSendTask(Task<HttpResponseMessage> sendTask)
        {
            _ = sendTask.ContinueWith(
                static task =>
                {
                    task.Result.Dispose();
                },
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
            _ = sendTask.ContinueWith(
                static task =>
                {
                    _ = task.Exception;
                },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        private static void LogApiBodyFailure(
            string title,
            string requestUri,
            string requestPayload,
            HttpResponseMessage response,
            string responseBody,
            Exception exception)
        {
            DiagnosticsFileLogger.AppendBlock(
                title,
                $"uri={requestUri}{Environment.NewLine}"
                + $"status={(int)response.StatusCode} ({response.StatusCode}){Environment.NewLine}"
                + $"request={requestPayload}{Environment.NewLine}"
                + $"responseHeaders={FormatHeaders(response)}{Environment.NewLine}"
                + $"responseBody={TruncateForTrace(responseBody)}{Environment.NewLine}"
                + $"error={exception.GetType().Name}: {exception.Message}");
        }

        private static string FormatHeaders(HttpResponseMessage response)
        {
            var headers = response.Headers
                .Concat(response.Content.Headers)
                .Select(static header => $"{header.Key}: {string.Join(", ", header.Value)}");
            return string.Join("; ", headers);
        }

        protected static void EmitTrace(string message)
        {
            if (!DiagnosticsEnabled)
            {
                return;
            }

            TraceEmitted?.Invoke(message);
        }

        protected virtual void LogSuccessfulResponseBody<TRequest>(
            string requestUri,
            TRequest request,
            string responseBody)
        {
        }

        public static void EmitExternalTrace(string message)
        {
            EmitTrace(message);
        }

        private static void EmitResponseTrace(string requestUri, string body)
        {
            if (ShouldSuppressResponsePayload(requestUri))
            {
                return;
            }

            EmitTrace($"HTTP RESPONSE uri={requestUri} body={TruncateForTrace(body)}");
        }

        private static void EmitApiSend(HttpMethod method, string requestUri, string requestPayload)
        {
            EmitTrace($"API SEND method={method.Method} uri={requestUri}{FormatQueryIdentity(requestUri, requestPayload)}");
        }

        private static void EmitApiCanceled(HttpMethod method, string requestUri, string requestPayload)
        {
            EmitTrace($"API CANCELED method={method.Method} uri={requestUri}{FormatQueryIdentity(requestUri, requestPayload)}");
        }

        private static string FormatQueryIdentity(string requestUri, string requestPayload)
        {
            if (!string.Equals(requestUri, "api/index", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(requestUri, "api/count", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            try
            {
                using var payload = JsonDocument.Parse(requestPayload);
                var root = payload.RootElement;
                var model = root.GetProperty("model").GetString()
                    ?? throw new InvalidOperationException("Data query payload model must not be null.");
                var preset = root.TryGetProperty("preset", out var presetElement)
                    && presetElement.ValueKind == JsonValueKind.String
                    ? presetElement.GetString()
                    : null;
                var id = root.TryGetProperty("filters", out var filters)
                    && filters.ValueKind == JsonValueKind.Object
                    && filters.TryGetProperty("id__eq", out var idElement)
                    ? FormatTraceValue(idElement)
                    : null;

                return $" model={model} preset={preset ?? "<null>"} id={id ?? "<null>"}";
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private static string FormatTraceValue(JsonElement value)
        {
            return value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : value.GetRawText();
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

        private static bool ShouldSuppressResponsePayload(string requestUri)
        {
            return string.Equals(requestUri, "api/index", StringComparison.OrdinalIgnoreCase)
                || string.Equals(requestUri, "api/count", StringComparison.OrdinalIgnoreCase)
                || string.Equals(requestUri, "model/Holiday", StringComparison.OrdinalIgnoreCase)
                || IsTrackedModelRecordUri(requestUri);
        }

        private static bool IsTrackedModelRecordUri(string requestUri)
        {
            return requestUri.StartsWith("model/Stage/", StringComparison.OrdinalIgnoreCase)
                || requestUri.StartsWith("model/Contract/", StringComparison.OrdinalIgnoreCase)
                || requestUri.StartsWith("model/Revision/", StringComparison.OrdinalIgnoreCase);
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
