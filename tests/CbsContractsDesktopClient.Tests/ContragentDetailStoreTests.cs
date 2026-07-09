using System.Text.Json;
using CbsContractsDesktopClient.Stores.Contragents;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContragentDetailStoreTests
{
    [Fact]
    public void SetContragent_UsesFullEmployeeNameBeforeShortName()
    {
        var store = new ContragentDetailStore();
        var row = CreateRow(
            ("name", "ООО Контрагент"),
            ("employees", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 10L,
                    ["name"] = "Иванов И.И.",
                    ["full_name"] = "Иванов Иван Иванович"
                }
            }));

        store.SetContragent(row);

        var employee = Assert.Single(store.Employees);
        Assert.Equal("Иванов Иван Иванович", employee.FullName);
        Assert.Equal("ООО Контрагент", store.Name);
    }

    [Fact]
    public void SetContragent_ReadsFullEmployeeNameFromNestedEmployeePerson()
    {
        var store = new ContragentDetailStore();
        var row = CreateRow(
            ("employees", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["name"] = "Петров П.П.",
                    ["employee"] = new Dictionary<string, object?>
                    {
                        ["person"] = new Dictionary<string, object?>
                        {
                            ["full_name"] = "Петров Петр Петрович"
                        }
                    }
                }
            }));

        store.SetContragent(row);

        var employee = Assert.Single(store.Employees);
        Assert.Equal("Петров Петр Петрович", employee.FullName);
    }

    [Fact]
    public void SetContragent_NormalizesContragentAndEmployeeContacts()
    {
        var store = new ContragentDetailStore();
        var row = CreateRow(
            ("contacts", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["contact"] = new Dictionary<string, object?>
                    {
                        ["value"] = "+7 900 000-00-00"
                    }
                },
                "+7 900 000-00-00"
            }),
            ("employees", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["full_name"] = "Сидоров Сидор Сидорович",
                    ["person"] = new Dictionary<string, object?>
                    {
                        ["contacts"] = new object[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["contact_attributes"] = new Dictionary<string, object?>
                                {
                                    ["value"] = "sidor@example.com"
                                }
                            }
                        }
                    }
                }
            }));

        store.SetContragent(row);

        Assert.Equal(["+7 900 000-00-00"], store.Contacts);
        var employee = Assert.Single(store.Employees);
        Assert.Equal(["sidor@example.com"], employee.Contacts);
    }

    private static TableDataRow CreateRow(params (string Key, object? Value)[] values)
    {
        var row = new TableDataRow();
        foreach (var (key, value) in values)
        {
            row.Values[key] = JsonSerializer.SerializeToElement(value);
        }

        row.RefreshResolvedValues();
        return row;
    }
}
