using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ProfileEditPayloadBuilderTests
{
    [Fact]
    public void BuildForUpdate_MapsChangedLoginEmailAndPersonIntoUserAttributes()
    {
        var viewModel = CreateViewModel(new ProfileEditDialogState
        {
            Definition = CreateDefinition(),
            IsCreateMode = false,
            Id = 15,
            UserId = 41,
            PersonId = 73,
            Login = "asmith",
            Email = "asmith@example.com",
            PersonName = "Иванов Иван",
            Role = "user",
            PositionName = "Менеджер",
            DepartmentId = 4,
            IsActive = true
        });
        viewModel.Login = "asmith2";
        viewModel.Email = "asmith2@example.com";
        viewModel.PersonName = "Петров Петр";

        var payload = ProfileEditPayloadBuilder.BuildForUpdate(viewModel);

        Assert.Equal(15L, payload["id"]);
        Assert.False(payload.ContainsKey("list_key"));

        var userAttributes = Assert.IsType<Dictionary<string, object?>>(payload["user_attributes"]);
        Assert.Equal(41L, userAttributes["id"]);
        Assert.Equal("asmith2", userAttributes["name"]);

        var personAttributes = Assert.IsType<Dictionary<string, object?>>(userAttributes["person_attributes"]);
        Assert.Equal(73L, personAttributes["id"]);
        Assert.True(personAttributes.ContainsKey("person_names_attributes"));
        Assert.True(personAttributes.ContainsKey("person_contacts_attributes"));
    }

    [Fact]
    public void BuildForUpdate_AddsListKey_WhenAuditIsEnabled()
    {
        var viewModel = CreateViewModel(new ProfileEditDialogState
        {
            Definition = CreateDefinition(isAuditEnabled: true),
            IsCreateMode = false,
            Id = 15,
            UserId = 41,
            PersonId = 73,
            Login = "asmith",
            Email = "asmith@example.com",
            PersonName = "Иванов Иван",
            Role = "user",
            PositionName = "Менеджер",
            DepartmentId = 4,
            IsActive = true
        });
        viewModel.Login = "asmith2";

        var payload = ProfileEditPayloadBuilder.BuildForUpdate(viewModel);

        Assert.True(payload.ContainsKey("list_key"));
        Assert.False(string.IsNullOrWhiteSpace(payload["list_key"]?.ToString()));
    }

    [Fact]
    public void BuildForUpdate_ThrowsWhenUserIdMissingButUserFieldsChanged()
    {
        var viewModel = CreateViewModel(new ProfileEditDialogState
        {
            Definition = CreateDefinition(),
            IsCreateMode = false,
            Id = 15,
            Login = "asmith",
            Email = "asmith@example.com",
            PersonName = "Иванов Иван",
            Role = "user",
            PositionName = "Менеджер",
            DepartmentId = 4,
            IsActive = true
        });
        viewModel.Login = "asmith2";

        var exception = Assert.Throws<InvalidOperationException>(() => ProfileEditPayloadBuilder.BuildForUpdate(viewModel));

        Assert.Contains("User id", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildForUpdate_ReturnsOnlyId_WhenNoChanges()
    {
        var viewModel = CreateViewModel(new ProfileEditDialogState
        {
            Definition = CreateDefinition(),
            IsCreateMode = false,
            Id = 15,
            UserId = 41,
            PersonId = 73,
            Login = "asmith",
            Email = "asmith@example.com",
            PersonName = "Иванов Иван",
            Role = "user",
            PositionName = "Менеджер",
            DepartmentId = 4,
            IsActive = true
        });

        var payload = ProfileEditPayloadBuilder.BuildForUpdate(viewModel);

        Assert.Single(payload);
        Assert.Equal(15L, payload["id"]);
    }

    [Fact]
    public void BuildForCreate_BuildsNestedPayloadWithRequiredFields_AndListKey()
    {
        var viewModel = CreateViewModel(new ProfileEditDialogState
        {
            Definition = CreateDefinition(),
            IsCreateMode = true,
            Login = "new_user",
            Email = "new_user@example.com",
            PersonName = "Иванов Иван",
            Role = "user",
            PositionName = "Менеджер",
            DepartmentId = 4,
            IsActive = true
        });
        viewModel.Password = "secret";

        var payload = ProfileEditPayloadBuilder.BuildForCreate(viewModel);

        Assert.False(payload.ContainsKey("id"));
        Assert.Equal(4L, payload["department_id"]);
        Assert.True(payload.ContainsKey("position_attributes") || payload.ContainsKey("position_id"));
        Assert.True(payload.ContainsKey("list_key"));
        Assert.False(string.IsNullOrWhiteSpace(payload["list_key"]?.ToString()));

        var user = Assert.IsType<Dictionary<string, object?>>(payload["user_attributes"]);
        Assert.Equal("new_user", user["name"]);
        Assert.Equal("secret", user["password"]);
        Assert.Equal("user", user["role"]);
        Assert.True(user.ContainsKey("person_attributes"));
    }

    private static ProfileEditViewModel CreateViewModel(ProfileEditDialogState state)
    {
        return new ProfileEditViewModel(state);
    }

    private static ReferenceDefinition CreateDefinition(bool isAuditEnabled = false)
    {
        return new ReferenceDefinition
        {
            Route = "/users",
            Model = "Profile",
            Title = "Пользователи",
            EditorKind = ReferenceEditorKind.Profile,
            IsAuditEnabled = isAuditEnabled
        };
    }
}
