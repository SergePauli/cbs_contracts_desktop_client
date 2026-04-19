using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ApiServiceBaseTraceTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

    private static readonly string ApiServiceBasePath = Path.Combine(
        ProjectRoot,
        "src",
        "Services",
        "ApiServiceBase.cs");

    [Fact]
    public void ApiServiceBase_EmitsHttpRequestPayloadTrace()
    {
        var code = File.ReadAllText(ApiServiceBasePath);

        Assert.Contains("private const bool DiagnosticsEnabled = true;", code);
        Assert.Contains("HTTP POST uri=", code);
        Assert.Contains("HTTP POST JSON uri=", code);
        Assert.Contains("HTTP PUT uri=", code);
        Assert.Contains("HTTP DELETE uri=", code);
        Assert.Contains("HTTP RESPONSE uri=", code);
        Assert.Contains("SerializeForTrace", code);
    }
}
