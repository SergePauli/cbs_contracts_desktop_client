using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Shared.Data;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class JsonDataReaderTests
{
    [Fact]
    public void TryGetArrayCount_ReturnsArrayLengthFromReferenceRow()
    {
        var row = new ReferenceDataRow
        {
            Values =
            {
                ["items"] = JsonSerializer.SerializeToElement(new[] { 1, 2, 3 })
            }
        };

        Assert.Equal(3, JsonDataReader.TryGetArrayCount(row, "items"));
    }

    [Fact]
    public void EnumerateObjectArray_SkipsNonObjectItems()
    {
        var row = new ReferenceDataRow
        {
            Values =
            {
                ["items"] = JsonSerializer.SerializeToElement(new object?[]
                {
                    new { id = 1 },
                    "ignored",
                    new { id = 2 }
                })
            }
        };

        var ids = JsonDataReader
            .EnumerateObjectArray(row, "items")
            .Select(static item => JsonDataReader.TryGetLong(item, "id"))
            .ToList();

        Assert.Equal([1L, 2L], ids);
    }

    [Fact]
    public void TryGetSingleLineString_NormalizesJsonText()
    {
        var item = JsonSerializer.SerializeToElement(new
        {
            name = "  first\r\nsecond   third  "
        });

        Assert.Equal("first second third", JsonDataReader.TryGetSingleLineString(item, "name"));
    }

    [Fact]
    public void TryGetText_ReturnsFirstNonEmptyRowValue()
    {
        var row = new ReferenceDataRow
        {
            Values =
            {
                ["empty"] = JsonSerializer.SerializeToElement(" "),
                ["name"] = JsonSerializer.SerializeToElement("Contract")
            }
        };

        Assert.Equal("Contract", JsonDataReader.TryGetText(row, "empty", "name"));
    }

    [Fact]
    public void GetDisplayText_ReadsDisplayRowBeforeFallbackRow()
    {
        var sourceRow = new ReferenceDataRow
        {
            Values =
            {
                ["name"] = JsonSerializer.SerializeToElement("source")
            }
        };
        var displayRow = new ReferenceDataRow
        {
            Values =
            {
                ["name"] = JsonSerializer.SerializeToElement("display")
            }
        };

        Assert.Equal("display", JsonDataReader.GetDisplayText(displayRow, sourceRow, "name"));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void TryGetBool_ReadsNumericText(string value, bool expected)
    {
        Assert.Equal(expected, JsonDataReader.TryGetBool(value));
    }
}
