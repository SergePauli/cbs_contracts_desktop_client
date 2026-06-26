using System.Net;
using System.Net.Http;
using System.Text;
using CbsContractsDesktopClient.Services.Diagnostics;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public class SystemDiagnosticsRunnerTests
{
    private static readonly Uri PrimaryApiUri = new("http://api-server:5000/");
    private static readonly Uri QueryApiUri = new("http://api-server:8080/");
    private static readonly Uri FnsApiUri = new("https://api-fns.test/api/");

    [Fact]
    public async Task RunAsync_PrimaryApi_ReturnsOkWithoutHttpRequest()
    {
        var capturedRequests = new List<HttpRequestMessage>();
        var runner = CreateRunner(request =>
        {
            capturedRequests.Add(request);
            return request.RequestUri?.AbsolutePath == "/healthz"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("ok", Encoding.UTF8, "text/plain")
                }
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        var results = await runner.RunAsync();

        var primary = Assert.Single(results, result => result.Title == SystemDiagnosticsRunner.PrimaryApiTitle);
        Assert.True(primary.IsHealthy);
        Assert.Equal("ОК", primary.StatusText);
        Assert.Equal("Авторизация выполнена", primary.Detail);
        Assert.DoesNotContain(capturedRequests, request => request.RequestUri!.ToString().StartsWith(PrimaryApiUri.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_QueryApiHealthzOk_ReturnsAvailable()
    {
        var capturedRequests = new List<HttpRequestMessage>();
        var runner = CreateRunner(request =>
        {
            capturedRequests.Add(request);
            return request.RequestUri?.AbsolutePath == "/healthz"
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("ok", Encoding.UTF8, "text/plain")
                }
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        var results = await runner.RunAsync();

        var query = Assert.Single(results, result => result.Title == SystemDiagnosticsRunner.QueryApiTitle);
        Assert.True(query.IsHealthy);
        Assert.Equal("Доступен", query.StatusText);
        Assert.Equal("healthz: ok", query.Detail);
        Assert.Contains(capturedRequests, request => request.RequestUri!.ToString() == "http://api-server:8080/healthz");
    }

    [Fact]
    public async Task RunAsync_QueryApiHealthzUnexpectedBody_ReturnsError()
    {
        var runner = CreateRunner(request => request.RequestUri?.AbsolutePath == "/healthz"
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("warming-up", Encoding.UTF8, "text/plain")
            }
            : new HttpResponseMessage(HttpStatusCode.OK));

        var results = await runner.RunAsync();

        var query = Assert.Single(results, result => result.Title == SystemDiagnosticsRunner.QueryApiTitle);
        Assert.False(query.IsHealthy);
        Assert.Equal("Ошибка", query.StatusText);
        Assert.Contains("body: warming-up", query.Detail);
    }

    [Fact]
    public async Task RunAsync_QueryApiHealthzNonSuccess_ReturnsError()
    {
        var runner = CreateRunner(request => request.RequestUri?.AbsolutePath == "/healthz"
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("down", Encoding.UTF8, "text/plain")
            }
            : new HttpResponseMessage(HttpStatusCode.OK));

        var results = await runner.RunAsync();

        var query = Assert.Single(results, result => result.Title == SystemDiagnosticsRunner.QueryApiTitle);
        Assert.False(query.IsHealthy);
        Assert.Equal("Ошибка", query.StatusText);
        Assert.Contains("HTTP 503", query.Detail);
    }

    [Fact]
    public async Task RunAsync_MissingContractsFolder_ReturnsUnavailable()
    {
        var folderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var runner = CreateRunner(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok", Encoding.UTF8, "text/plain")
        }, folderPath);

        var results = await runner.RunAsync();

        var folder = Assert.Single(results, result => result.Title == SystemDiagnosticsRunner.ContractsFolderTitle);
        Assert.False(folder.IsHealthy);
        Assert.Equal("Недоступна", folder.StatusText);
        Assert.Equal("Каталог не найден", folder.Detail);
    }

    private static SystemDiagnosticsRunner CreateRunner(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        string? contractsFolderPath = null)
    {
        return new SystemDiagnosticsRunner(
            () => new HttpClient(new StubHttpMessageHandler(responseFactory)),
            PrimaryApiUri,
            QueryApiUri,
            FnsApiUri,
            contractsFolderPath ?? Path.GetTempPath());
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }
}
