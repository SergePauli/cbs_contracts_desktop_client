using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageFinEditPayloadBuilderTests
{
    [Fact]
    public void BuildForUpdate_SerializesOnlyChangedFinancialFieldsAndComment()
    {
        var sourceRow = CreateRow(
            ("id", 15L),
            ("list_key", "stage-key"),
            ("status_id", 2L),
            ("payment_at", null),
            ("prepayment_at", null),
            ("invoice_at", "Tue May 12 2026"),
            ("funded_at", null),
            ("is_funded", false),
            ("start_at", null),
            ("deadline_at", null),
            ("payment_deadline_at", null));

        var payload = StageFinEditPayloadBuilder.BuildForUpdate(
            sourceRow,
            new StageFinEditPayloadInput(
                Id: 15L,
                ListKey: "stage-key",
                PaymentAt: new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero),
                PrepaymentAt: null,
                InvoiceAt: new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero),
                FundedAt: new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero),
                IsFunded: true,
                StartAt: null,
                DeadlineAt: null,
                PaymentDeadlineAt: null,
                Comment: " paid ",
                ProfileId: 7));

        Assert.Equal(15L, payload["id"]);
        Assert.Equal("stage-key", payload["list_key"]);
        Assert.False(payload.ContainsKey("status_id"));
        Assert.Equal("Thu May 14 2026", payload["payment_at"]);
        Assert.False(payload.ContainsKey("invoice_at"));
        Assert.Equal("Fri May 15 2026", payload["funded_at"]);
        Assert.Equal(true, payload["is_funded"]);
        var comments = Assert.IsType<Dictionary<string, object?>[]>(payload["comments_attributes"]);
        Assert.Equal("paid", comments[0]["content"]);
        Assert.Equal(7, comments[0]["profile_id"]);
    }

    private static TableDataRow CreateRow(params (string Key, object? Value)[] values)
    {
        return new TableDataRow
        {
            Values = values.ToDictionary(
                static value => value.Key,
                static value => JsonSerializer.SerializeToElement(value.Value))
        };
    }
}
