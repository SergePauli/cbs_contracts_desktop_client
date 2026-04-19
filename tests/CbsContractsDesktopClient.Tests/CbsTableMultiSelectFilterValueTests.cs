using System.Collections.Generic;
using System.Linq;
using CbsContractsDesktopClient.Models.Table;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class CbsTableMultiSelectFilterValueTests
{
    [Fact]
    public void Create_NormalizesObjectsWithIdAndNameProperties()
    {
        var value = CbsTableMultiSelectFilterValue.Create(
            new List<TestOption>
            {
                new() { Id = 2, Name = "\u041e\u0442\u0434\u0435\u043b \u043f\u0440\u043e\u0434\u0430\u0436" },
                new() { Id = 5, Name = "\u042e\u0440\u0438\u0434\u0438\u0447\u0435\u0441\u043a\u0438\u0439 \u043e\u0442\u0434\u0435\u043b" }
            },
            new List<int> { 5 });

        Assert.Equal(2, value.Options.Count);
        Assert.Equal(2, value.Options[0].Value);
        Assert.Equal("\u041e\u0442\u0434\u0435\u043b \u043f\u0440\u043e\u0434\u0430\u0436", value.Options[0].Label);
        Assert.Equal(new object?[] { 5 }, value.SelectedValues);
        Assert.Equal(
            ["\u042e\u0440\u0438\u0434\u0438\u0447\u0435\u0441\u043a\u0438\u0439 \u043e\u0442\u0434\u0435\u043b"],
            value.SelectedOptions.Select(static option => option.Label));
    }

    [Fact]
    public void Create_NormalizesDictionaryOptionsCaseInsensitively()
    {
        var value = CbsTableMultiSelectFilterValue.Create(
            new object?[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 10,
                    ["name"] = "\u0410\u0434\u043c\u0438\u043d\u0438\u0441\u0442\u0440\u0430\u0446\u0438\u044f"
                },
                new Dictionary<string, object?>
                {
                    ["ID"] = 11,
                    ["NAME"] = "\u0424\u0438\u043d\u0430\u043d\u0441\u044b"
                }
            },
            new object?[] { 10, 11 });

        Assert.Equal(
            ["\u0410\u0434\u043c\u0438\u043d\u0438\u0441\u0442\u0440\u0430\u0446\u0438\u044f", "\u0424\u0438\u043d\u0430\u043d\u0441\u044b"],
            value.Options.Select(static option => option.Label));
        Assert.Equal(
            ["\u0410\u0434\u043c\u0438\u043d\u0438\u0441\u0442\u0440\u0430\u0446\u0438\u044f", "\u0424\u0438\u043d\u0430\u043d\u0441\u044b"],
            value.SelectedOptions.Select(static option => option.Label));
    }

    [Fact]
    public void Create_PreservesExistingFilterOptionDefinitions()
    {
        var value = CbsTableMultiSelectFilterValue.Create(
            new object?[]
            {
                new CbsTableFilterOptionDefinition
                {
                    Value = 1,
                    Label = "IT"
                }
            },
            Array.Empty<object?>());

        Assert.Single(value.Options);
        Assert.Equal(1, value.Options[0].Value);
        Assert.Equal("IT", value.Options[0].Label);
    }

    private sealed class TestOption
    {
        public int Id { get; init; }

        public required string Name { get; init; }
    }
}
