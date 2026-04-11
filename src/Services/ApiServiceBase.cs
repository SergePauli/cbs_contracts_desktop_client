using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CbsContractsDesktopClient.Services
{
    public abstract class ApiServiceBase
    {
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

        protected async Task<TResponse> PostAsync<TRequest, TResponse>(string requestUri, TRequest request, CancellationToken cancellationToken = default)
        {
            using var message = CreatePostRequest(requestUri, request);
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
            if (result is null)
            {
                throw new InvalidOperationException($"Ответ '{requestUri}' не удалось десериализовать в {typeof(TResponse).Name}.");
            }

            return result;
        }

        protected async Task<JsonElement> PostForJsonAsync<TRequest>(string requestUri, TRequest request, CancellationToken cancellationToken = default)
        {
            using var message = CreatePostRequest(requestUri, request);
            using var response = await _httpClient.SendAsync(message, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return document.RootElement.Clone();
        }

        private HttpRequestMessage CreatePostRequest<TRequest>(string requestUri, TRequest request)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(request, options: JsonOptions)
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
    }
}
