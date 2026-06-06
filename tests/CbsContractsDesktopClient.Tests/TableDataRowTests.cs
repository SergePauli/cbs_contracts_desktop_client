using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class TableDataRowTests
{
    [Fact]
    public void GetValue_ReturnsNestedObjectProperty_ByDottedPath()
    {
        var row = new TableDataRow
        {
            Values =
            {
                ["user"] = JsonSerializer.SerializeToElement(new
                {
                    name = "admin",
                    role = "manager"
                })
            }
        };

        Assert.Equal("admin", row.GetValue("user.name"));
        Assert.Equal("manager", row.GetValue("user.role"));
    }

    [Fact]
    public void GetValue_ReturnsNestedValues_FromArrayPath()
    {
        var row = new TableDataRow
        {
            Values =
            {
                ["user"] = JsonSerializer.SerializeToElement(new
                {
                    person = new
                    {
                        person_contacts = new[]
                        {
                            new
                            {
                                contact = new
                                {
                                    value = "first@example.com"
                                }
                            },
                            new
                            {
                                contact = new
                                {
                                    value = "second@example.com"
                                }
                            }
                        }
                    }
                })
            }
        };

        Assert.Equal(
            "first@example.com, second@example.com",
            row.GetValue("user.person.person_contacts.contact.value"));
    }

    [Fact]
    public void GetValue_PrefersDirectTopLevelValue_WhenExactKeyExists()
    {
        var row = new TableDataRow
        {
            Values =
            {
                ["user.name"] = JsonSerializer.SerializeToElement("flattened"),
                ["user"] = JsonSerializer.SerializeToElement(new
                {
                    name = "nested"
                })
            }
        };

        Assert.Equal("flattened", row.GetValue("user.name"));
    }

    [Fact]
    public void RefreshResolvedValues_RebuildsCachedValuesAfterValuesMutation()
    {
        var row = new TableDataRow
        {
            Values =
            {
                ["name"] = JsonSerializer.SerializeToElement("old"),
                ["user"] = JsonSerializer.SerializeToElement(new
                {
                    name = "old nested"
                })
            }
        };

        Assert.Equal("old", row.GetValue("name"));
        Assert.Equal("old nested", row.GetValue("user.name"));

        row.Values["name"] = JsonSerializer.SerializeToElement("new");
        row.Values["user"] = JsonSerializer.SerializeToElement(new
        {
            name = "new nested"
        });
        row.RefreshResolvedValues();

        Assert.Equal("new", row.GetValue("name"));
        Assert.Equal("new nested", row.GetValue("user.name"));
    }
}
