using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ReferenceDataRowTests
{
    [Fact]
    public void GetValue_ReturnsNestedObjectProperty_ByDottedPath()
    {
        var row = new ReferenceDataRow
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
        var row = new ReferenceDataRow
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
        var row = new ReferenceDataRow
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
}
