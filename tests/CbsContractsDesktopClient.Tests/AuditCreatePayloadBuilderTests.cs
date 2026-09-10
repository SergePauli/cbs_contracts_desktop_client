using CbsContractsDesktopClient.Models.Shell;
using CbsContractsDesktopClient.Stores.Table;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class AuditCreatePayloadBuilderTests
{
    [Fact]
    public void Build_SerializesCausalAuditWithAuthorAndChangedValue()
    {
        var entry = new PendingAuditEntry(
            "Contract",
            6217,
            "signed_at",
            "updated",
            "Изменение даты подписания запустило автоматическое изменение статуса и сроков этапов",
            "-",
            "09.09.2026");

        var payload = AuditCreatePayloadBuilder.Build(entry, userId: 1, personId: 2);

        Assert.Equal("Contract", payload["auditable_type"]);
        Assert.Equal(6217L, payload["auditable_id"]);
        Assert.Equal("signed_at", payload["auditable_field"]);
        Assert.Equal("updated", payload["action"]);
        Assert.Equal(entry.Detail, payload["detail"]);
        Assert.Equal("-", payload["before"]);
        Assert.Equal("09.09.2026", payload["after"]);
        Assert.Equal(1, payload["user_id"]);
        Assert.Equal(2, payload["person_id"]);
    }
}
