using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class EmployeeEditStateFactoryTests
{
    [Fact]
    public void Create_ForEdit_FlattensFreshEmployeeRowWithContacts()
    {
        var definition = CreateDefinition();
        var row = new ReferenceDataRow
        {
            Values =
            {
                ["id"] = JsonValue(15),
                ["list_key"] = JsonValue("employee-list-key"),
                ["person"] = JsonValue(new
                {
                    id = 20,
                    full_name = "Иванов Иван Иванович",
                    contacts = new[]
                    {
                        new
                        {
                            id = 31,
                            list_key = "contact-list-key",
                            contact = new
                            {
                                value = "ivan@example.com",
                                type = "Email"
                            }
                        }
                    }
                }),
                ["position"] = JsonValue(new
                {
                    id = 7,
                    name = "Инженер"
                }),
                ["contragent"] = JsonValue(new
                {
                    id = 9,
                    full_name = "Общество с ограниченной ответственностью Ромашка",
                    name = "ООО Ромашка"
                }),
                ["used"] = JsonValue(false),
                ["priority"] = JsonValue(3),
                ["description"] = JsonValue("Описание")
            }
        };

        var state = EmployeeEditStateFactory.Create(definition, isCreateMode: false, row);

        Assert.Equal(definition, state.Definition);
        Assert.False(state.IsCreateMode);
        Assert.Equal(15L, state.Id);
        Assert.Equal("employee-list-key", state.ListKey);
        Assert.Equal(20L, state.PersonId);
        Assert.Equal("Иванов Иван Иванович", state.PersonName);
        Assert.Equal(7L, state.PositionId);
        Assert.Equal("Инженер", state.PositionName);
        Assert.Equal(9L, state.ContragentId);
        Assert.Equal("Общество с ограниченной ответственностью Ромашка", state.ContragentName);
        Assert.False(state.IsUsed);
        Assert.Equal(3, state.Priority);
        Assert.Equal("Описание", state.Description);

        var contact = Assert.Single(state.Contacts);
        Assert.Equal(31L, contact.Id);
        Assert.Equal("contact-list-key", contact.ListKey);
        Assert.Equal("ivan@example.com", contact.Value);
        Assert.Equal("Email", contact.Type);
        Assert.Equal("ivan@example.com", state.ContactsText);
    }

    [Fact]
    public void Create_ForCreate_UsesEmployeeDefaults()
    {
        var definition = CreateDefinition();

        var state = EmployeeEditStateFactory.Create(definition, isCreateMode: true, sourceRow: null);

        Assert.True(state.IsCreateMode);
        Assert.True(state.IsUsed);
        Assert.Empty(state.Contacts);
        Assert.Equal(string.Empty, state.ContactsText);
    }

    private static ReferenceDefinition CreateDefinition()
    {
        return new ReferenceDefinition
        {
            Route = "/employees",
            Model = "Employee",
            Title = "Сотрудники",
            Preset = "card",
            EditorKind = ReferenceEditorKind.Employee
        };
    }

    private static JsonElement JsonValue<TValue>(TValue value)
    {
        return JsonSerializer.SerializeToElement(value);
    }
}
