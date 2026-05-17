using CbsContractsDesktopClient.ViewModels.Workflow;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageCommerEditPayloadBuilderTests
{
    [Fact]
    public void BuildContractClosePayload_SerializesContractStatusAndClosedDate()
    {
        var payload = StageCommerEditPayloadBuilder.BuildContractClosePayload(
            42L,
            new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(42L, payload["id"]);
        Assert.Equal(5L, payload["status_id"]);
        Assert.Equal("Thu May 14 2026", payload["closed_at"]);
    }
}
