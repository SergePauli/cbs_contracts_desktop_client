using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContragentEditStateFactoryTests
{
    [Fact]
    public void Create_NormalizesSingleLineTextFields()
    {
        var row = CreateRow(
            ("id", 10),
            ("requisites", new
            {
                organization = new
                {
                    id = 20,
                    inn = "  1\r\n2  ",
                    name = "ООО\r\nРомашка",
                    full_name = "Общество\r\nс   ограниченной\tответственностью"
                }
            }));

        var state = ContragentEditStateFactory.Create(Definition(), isCreateMode: false, row);

        Assert.Equal("1 2", state.Inn);
        Assert.Equal("ООО Ромашка", state.Name);
        Assert.Equal("Общество с ограниченной ответственностью", state.FullName);
    }

    [Fact]
    public void OrganizationHistory_FormatsDatesInLocalCulture()
    {
        var source = "2026-05-02T10:15:00Z";
        var item = new ContragentOrganizationHistoryItem
        {
            CreatedAt = source
        };
        var expected = DateTimeOffset
            .Parse(source, CultureInfo.InvariantCulture)
            .ToLocalTime()
            .ToString("g", CultureInfo.CurrentCulture);

        Assert.Contains($"внесено: {expected}", item.PeriodText);
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

    private static ReferenceDefinition Definition()
    {
        return new ReferenceDefinition
        {
            Route = "/contragents",
            Model = "Contragent",
            Title = "Контрагенты",
            Preset = "card",
            EditorKind = ReferenceEditorKind.Contragent
        };
    }
}
