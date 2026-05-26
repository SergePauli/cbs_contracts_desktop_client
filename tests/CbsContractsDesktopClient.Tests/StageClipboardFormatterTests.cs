using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageClipboardFormatterTests
{
    [Fact]
    public void BuildClipboardText_UsesStageDatesContragentAndExternalContractNumber()
    {
        var row = CreateRow(
            ("start_at", "Mon May 04 2026"),
            ("deadline_at", "Thu May 14 2026"),
            ("contract.contragent.name", "ООО Ромашка"),
            ("contract.external_number", "EXT-77"),
            ("contract.name", "12/26/001"));

        var text = StageClipboardFormatter.BuildClipboardText(row);

        Assert.Equal("04.05.2026-14.05.2026 | ООО Ромашка | EXT-77", text);
    }

    [Fact]
    public void BuildClipboardText_FallsBackToContractNameAndBlankDates()
    {
        var row = CreateRow(
            ("start_at", null),
            ("deadline_at", null),
            ("contract.contragent.org.full_name", "АО Полное имя"),
            ("contract.external_number", null),
            ("contract.name", "12/26/001"));

        var text = StageClipboardFormatter.BuildClipboardText(row);

        Assert.Equal(" -  | АО Полное имя | 12/26/001", text);
    }

    private static ReferenceDataRow CreateRow(params (string Key, object? Value)[] values)
    {
        return new ReferenceDataRow
        {
            Values = values.ToDictionary(
                static value => value.Key,
                static value => JsonSerializer.SerializeToElement(value.Value))
        };
    }
}
