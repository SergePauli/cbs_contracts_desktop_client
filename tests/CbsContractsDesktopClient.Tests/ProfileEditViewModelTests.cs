using System.Threading;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.ViewModels.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ProfileEditViewModelTests
{
    [Fact]
    public void DialogTitle_ReflectsCreateOrEditMode()
    {
        var createVm = CreateViewModel(isCreateMode: true);
        var editVm = CreateViewModel(isCreateMode: false);

        Assert.Equal("Создание профиля пользователя", createVm.DialogTitle);
        Assert.Equal("Редактирование профиля пользователя", editVm.DialogTitle);
    }

    [Fact]
    public void ErrorInfoState_CanBeShownAndCleared()
    {
        var viewModel = CreateViewModel();

        viewModel.ShowErrorInfo("Ошибка валидации");

        Assert.Equal("Ошибка валидации", viewModel.ErrorInfoMessage);
        Assert.True(viewModel.IsErrorInfoVisible);

        viewModel.ClearErrorInfo();

        Assert.Equal(string.Empty, viewModel.ErrorInfoMessage);
        Assert.False(viewModel.IsErrorInfoVisible);
    }

    [Fact]
    public void RoleApiValue_UsesOrderedCsvFromStaticOptions()
    {
        var viewModel = CreateViewModel();

        viewModel.IsRoleExcelSelected = true;
        viewModel.IsRoleUserSelected = true;
        viewModel.IsRoleAdminSelected = true;

        Assert.Equal("user,admin,excel", viewModel.RoleApiValue);
    }

    [Fact]
    public void RoleSelection_InternExcludesAdminAndExcel()
    {
        var viewModel = CreateViewModel();
        viewModel.IsRoleAdminSelected = true;
        viewModel.IsRoleExcelSelected = true;

        viewModel.IsRoleInternSelected = true;

        Assert.True(viewModel.IsRoleInternSelected);
        Assert.False(viewModel.IsRoleAdminSelected);
        Assert.False(viewModel.IsRoleExcelSelected);
        Assert.Equal("intern", viewModel.RoleApiValue);
    }

    [Fact]
    public void RoleSelection_AdminOrExcelClearsIntern()
    {
        var viewModel = CreateViewModel();
        viewModel.IsRoleInternSelected = true;

        viewModel.IsRoleAdminSelected = true;

        Assert.True(viewModel.IsRoleAdminSelected);
        Assert.False(viewModel.IsRoleInternSelected);
        Assert.Equal("admin", viewModel.RoleApiValue);
    }

    [Fact]
    public void CommitPositionInput_CapitalizesManualValue_WhenOptionsAreMissing()
    {
        var viewModel = CreateViewModel();

        viewModel.CommitPositionInput("manager");

        Assert.Equal("Manager", viewModel.PositionName);
        Assert.Equal("Manager", viewModel.PositionInput);
        Assert.Null(viewModel.SelectedPositionOption);
    }

    [Fact]
    public async Task UpdatePositionOptionsAsync_DoesNotOpenAutocomplete_ForPersistedStateValue()
    {
        var loaderCalls = 0;
        var viewModel = CreateViewModel(
            loader: (_, _) =>
            {
                loaderCalls++;
                return Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>(
                [
                    new CbsTableFilterOptionDefinition
                    {
                        Value = 7L,
                        Label = "Senior Manager"
                    }
                ]);
            },
            positionName: "Senior Manager");

        await viewModel.UpdatePositionOptionsAsync("Senior Manager");

        Assert.Equal(0, loaderCalls);
        Assert.Empty(viewModel.PositionOptions);
        Assert.Equal("Senior Manager", viewModel.PositionName);
    }

    [Fact]
    public async Task CommitPositionInput_RequiresExistingOption_WhenLookupReturnsMatches()
    {
        var viewModel = CreateViewModel(static (_, _) =>
            Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>(
            [
                new CbsTableFilterOptionDefinition
                {
                    Value = 7L,
                    Label = "Senior Manager"
                }
            ]));

        await viewModel.UpdatePositionOptionsAsync("man");
        viewModel.CommitPositionInput("manager");

        Assert.Equal(string.Empty, viewModel.PositionName);
        Assert.Equal("manager", viewModel.PositionInput);
        Assert.Null(viewModel.SelectedPositionOption);
    }

    [Fact]
    public async Task CommitPositionInput_AcceptsExactLookupOption()
    {
        var viewModel = CreateViewModel(static (_, _) =>
            Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>(
            [
                new CbsTableFilterOptionDefinition
                {
                    Value = 7L,
                    Label = "Senior Manager"
                }
            ]));

        await viewModel.UpdatePositionOptionsAsync("sen");
        viewModel.CommitPositionInput("senior manager");

        Assert.Equal("Senior Manager", viewModel.PositionName);
        Assert.Equal("Senior Manager", viewModel.PositionInput);
        Assert.NotNull(viewModel.SelectedPositionOption);
        Assert.Equal(7L, viewModel.PositionId);
    }

    [Fact]
    public async Task PositionSuggestionLabels_ReturnsLoadedLabels()
    {
        var viewModel = CreateViewModel(static (_, _) =>
            Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>(
            [
                new CbsTableFilterOptionDefinition
                {
                    Value = 7L,
                    Label = "Senior Manager"
                },
                new CbsTableFilterOptionDefinition
                {
                    Value = 8L,
                    Label = "Tech Director"
                }
            ]));

        await viewModel.UpdatePositionOptionsAsync("r");

        Assert.Equal(["Senior Manager", "Tech Director"], viewModel.PositionSuggestionLabels);
        Assert.Equal(8L, viewModel.FindPositionOption("Tech Director")?.Value);
        Assert.Equal(2, viewModel.PositionOptions.Count);
    }

    [Fact]
    public void CanSubmit_BecomesTrue_ForValidChangedLogin()
    {
        var viewModel = CreateViewModel(
            positionName: "Manager",
            role: "user",
            personName: "Ivanov Ivan",
            departmentId: 1);
        viewModel.Login = "new_login";
        viewModel.Email = "user@example.com";

        Assert.True(viewModel.CanSubmit);
    }

    [Fact]
    public void CanSubmit_IsTrue_ForInvalidEmail_WhenThereAreChanges()
    {
        var viewModel = CreateViewModel(
            positionName: "Manager",
            role: "user",
            personName: "Ivanov Ivan",
            departmentId: 1);
        viewModel.Login = "new_login";
        viewModel.Email = "invalid-email";

        Assert.True(viewModel.CanSubmit);
    }

    [Fact]
    public void CanSubmit_IsTrue_WhenRoleIsEmpty_ButThereAreChanges()
    {
        var viewModel = CreateViewModel(
            positionName: "Manager",
            personName: "Ivanov Ivan",
            departmentId: 1);
        viewModel.Login = "new_login";

        Assert.True(viewModel.CanSubmit);
    }

    [Fact]
    public void CanSubmit_IsTrue_ForCreate_WhenPasswordIsEmpty()
    {
        var viewModel = CreateViewModel(
            isCreateMode: true,
            positionName: "Manager",
            role: "user",
            personName: "Ivanov Ivan",
            departmentId: 1);

        Assert.True(viewModel.CanSubmit);
    }

    private static ProfileEditViewModel CreateViewModel(
        Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>>? loader = null,
        string positionName = "",
        string role = "",
        string personName = "",
        long? departmentId = null,
        bool isCreateMode = false)
    {
        return new ProfileEditViewModel(
            new ProfileEditDialogState
            {
                Definition = new ReferenceDefinition
                {
                    Route = "/users",
                    Model = "Profile",
                    Title = "Users",
                    EditorKind = ReferenceEditorKind.Profile
                },
                IsCreateMode = isCreateMode,
                Login = "user",
                Email = "user@example.com",
                PositionName = positionName,
                Role = role,
                PersonName = personName,
                DepartmentId = departmentId
            },
            loader);
    }
}
