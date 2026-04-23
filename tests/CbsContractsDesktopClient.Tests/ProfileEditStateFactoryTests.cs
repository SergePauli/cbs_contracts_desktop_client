using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ProfileEditStateFactoryTests
{
    [Fact]
    public void Create_FlattensProfileRowIntoMinimalDialogState()
    {
        var definition = new ReferenceDefinition
        {
            Route = "/users",
            Model = "Profile",
            Title = "РџРѕР»СЊР·РѕРІР°С‚РµР»Рё",
            EditorKind = ReferenceEditorKind.Profile
        };

        var row = new ReferenceDataRow
        {
            Values =
            {
                ["id"] = JsonValue(15),
                ["user.id"] = JsonValue(29),
                ["user.person.id"] = JsonValue(45),
                ["user.name"] = JsonValue("asmith"),
                ["user.person.person_contacts.contact.value"] = JsonValue("asmith@example.com"),
                ["user.person.person_name.naming.fio"] = JsonValue("РРІР°РЅРѕРІ РРІР°РЅ"),
                ["user.role"] = JsonValue("manager"),
                ["position.id"] = JsonValue(8),
                ["position.name"] = JsonValue("РЎС‚Р°СЂС€РёР№ РјРµРЅРµРґР¶РµСЂ"),
                ["department.id"] = JsonValue(4),
                ["department.name"] = JsonValue("РћС‚РґРµР» РїСЂРѕРґР°Р¶"),
                ["user.activated"] = JsonValue(true),
                ["user.last_login"] = JsonValue("2026-04-20T14:30:00")
            }
        };

        var state = ProfileEditStateFactory.Create(
            definition,
            isCreateMode: false,
            row,
            [
                new CbsTableFilterOptionDefinition
                {
                    Value = 4L,
                    Label = "РћС‚РґРµР» РїСЂРѕРґР°Р¶"
                }
            ]);

        Assert.Equal(definition, state.Definition);
        Assert.False(state.IsCreateMode);
        Assert.Equal(15L, state.Id);
        Assert.Equal(29L, state.UserId);
        Assert.Equal(45L, state.PersonId);
        Assert.Equal("asmith", state.Login);
        Assert.Equal("asmith@example.com", state.Email);
        Assert.Equal("РРІР°РЅРѕРІ РРІР°РЅ", state.PersonName);
        Assert.Equal("manager", state.Role);
        Assert.Equal(8L, state.PositionId);
        Assert.Equal("РЎС‚Р°СЂС€РёР№ РјРµРЅРµРґР¶РµСЂ", state.PositionName);
        Assert.Equal(4L, state.DepartmentId);
        Assert.Equal("РћС‚РґРµР» РїСЂРѕРґР°Р¶", state.DepartmentName);
        Assert.Equal(string.Empty, state.Password);
        Assert.True(state.IsActive);
        Assert.Equal("2026-04-20T14:30:00", state.LastLoginText);
        Assert.Single(state.DepartmentOptions);
    }

    [Fact]
    public void Create_UsesDisplayFieldFallbacksForEmailAndPersonName()
    {
        var definition = new ReferenceDefinition
        {
            Route = "/users",
            Model = "Profile",
            Title = "РџРѕР»СЊР·РѕРІР°С‚РµР»Рё",
            EditorKind = ReferenceEditorKind.Profile
        };

        var row = new ReferenceDataRow
        {
            Values =
            {
                ["id"] = JsonValue(4),
                ["user.email.name"] = JsonValue("display@example.com"),
                ["user.person.full_name"] = JsonValue("РџРµС‚СЂРѕРІ РџРµС‚СЂ")
            }
        };

        var state = ProfileEditStateFactory.Create(definition, isCreateMode: false, row);

        Assert.Equal("display@example.com", state.Email);
        Assert.Equal("РџРµС‚СЂРѕРІ РџРµС‚СЂ", state.PersonName);
    }

    [Fact]
    public void Create_MapsPasswordWhenItIsPresent()
    {
        var definition = new ReferenceDefinition
        {
            Route = "/users",
            Model = "Profile",
            Title = "РџРѕР»СЊР·РѕРІР°С‚РµР»Рё",
            EditorKind = ReferenceEditorKind.Profile
        };

        var row = new ReferenceDataRow
        {
            Values =
            {
                ["user.password"] = JsonValue("secret")
            }
        };

        var state = ProfileEditStateFactory.Create(definition, isCreateMode: false, row);

        Assert.Equal("secret", state.Password);
    }

    private static System.Text.Json.JsonElement JsonValue<TValue>(TValue value)
    {
        return System.Text.Json.JsonSerializer.SerializeToElement(value);
    }
}
