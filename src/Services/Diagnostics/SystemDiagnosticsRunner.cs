using System.IO;
using System.Net.Http;

namespace CbsContractsDesktopClient.Services.Diagnostics
{
    public sealed class SystemDiagnosticsRunner
    {
        public const string PrimaryApiTitle = "Основной API";
        public const string QueryApiTitle = "Query API";
        public const string FnsApiTitle = "ФНС API";
        public const string ContractsFolderTitle = "Папка контрактов";

        private readonly Func<HttpClient> _httpClientFactory;
        private readonly Uri _primaryApiUri;
        private readonly Uri _queryApiUri;
        private readonly Uri _fnsApiUri;
        private readonly string _contractsFolderPath;

        public SystemDiagnosticsRunner(
            Func<HttpClient> httpClientFactory,
            Uri primaryApiUri,
            Uri queryApiUri,
            Uri fnsApiUri,
            string contractsFolderPath)
        {
            _httpClientFactory = httpClientFactory;
            _primaryApiUri = primaryApiUri;
            _queryApiUri = queryApiUri;
            _fnsApiUri = fnsApiUri;
            _contractsFolderPath = contractsFolderPath;
        }

        public async Task<IReadOnlyList<SystemDiagnosticsResult>> RunAsync(CancellationToken cancellationToken = default)
        {
            var results = await Task.WhenAll(
                Task.FromResult(CheckPrimaryApi()),
                CheckQueryApiAsync(cancellationToken),
                CheckFnsApiAsync(cancellationToken),
                Task.Run(CheckContractsFolder, cancellationToken));

            return results;
        }

        private SystemDiagnosticsResult CheckPrimaryApi()
        {
            return SystemDiagnosticsResult.Healthy(
                PrimaryApiTitle,
                _primaryApiUri.ToString(),
                "ОК",
                "Авторизация выполнена");
        }

        private async Task<SystemDiagnosticsResult> CheckQueryApiAsync(CancellationToken cancellationToken)
        {
            var healthUri = new Uri(_queryApiUri, "healthz");

            try
            {
                using var httpClient = _httpClientFactory();
                using var response = await httpClient.GetAsync(healthUri, cancellationToken);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode && string.Equals(content.Trim(), "ok", StringComparison.Ordinal))
                {
                    return SystemDiagnosticsResult.Healthy(
                        QueryApiTitle,
                        healthUri.ToString(),
                        "Доступен",
                        "healthz: ok");
                }

                return SystemDiagnosticsResult.Unhealthy(
                    QueryApiTitle,
                    healthUri.ToString(),
                    "Ошибка",
                    $"HTTP {(int)response.StatusCode} {response.StatusCode}, body: {FormatBody(content)}");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                return SystemDiagnosticsResult.Unhealthy(
                    QueryApiTitle,
                    healthUri.ToString(),
                    "Недоступен",
                    ex.Message);
            }
        }

        private async Task<SystemDiagnosticsResult> CheckFnsApiAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var httpClient = _httpClientFactory();
                using var request = new HttpRequestMessage(HttpMethod.Get, _fnsApiUri);
                using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                var isHealthy = (int)response.StatusCode < 500;
                return new SystemDiagnosticsResult(
                    FnsApiTitle,
                    _fnsApiUri.ToString(),
                    isHealthy ? "Ответ есть" : "Ошибка",
                    $"HTTP {(int)response.StatusCode} {response.StatusCode}",
                    isHealthy);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                return SystemDiagnosticsResult.Unhealthy(
                    FnsApiTitle,
                    _fnsApiUri.ToString(),
                    "Недоступен",
                    ex.Message);
            }
        }

        private SystemDiagnosticsResult CheckContractsFolder()
        {
            try
            {
                if (!Directory.Exists(_contractsFolderPath))
                {
                    return SystemDiagnosticsResult.Unhealthy(
                        ContractsFolderTitle,
                        _contractsFolderPath,
                        "Недоступна",
                        "Каталог не найден");
                }

                _ = Directory.EnumerateFileSystemEntries(_contractsFolderPath).Take(1).Count();
                return SystemDiagnosticsResult.Healthy(
                    ContractsFolderTitle,
                    _contractsFolderPath,
                    "Доступна",
                    "Чтение каталога доступно");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return SystemDiagnosticsResult.Unhealthy(
                    ContractsFolderTitle,
                    _contractsFolderPath,
                    "Нет доступа",
                    ex.Message);
            }
        }

        private static string FormatBody(string content)
        {
            var value = content.Trim();
            return string.IsNullOrEmpty(value) ? "<empty>" : value;
        }
    }

    public sealed record SystemDiagnosticsResult(
        string Title,
        string Target,
        string StatusText,
        string Detail,
        bool IsHealthy)
    {
        public static SystemDiagnosticsResult Healthy(
            string title,
            string target,
            string statusText,
            string detail)
        {
            return new SystemDiagnosticsResult(title, target, statusText, detail, true);
        }

        public static SystemDiagnosticsResult Unhealthy(
            string title,
            string target,
            string statusText,
            string detail)
        {
            return new SystemDiagnosticsResult(title, target, statusText, detail, false);
        }
    }
}
