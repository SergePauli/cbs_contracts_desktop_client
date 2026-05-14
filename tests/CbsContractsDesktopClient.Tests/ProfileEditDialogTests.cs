using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ProfileEditDialogTests
{
    private static readonly string ProfileEditDialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "References",
        "ProfileEditDialog.cs");

    private static readonly string AppDialogLayoutPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Shared",
        "Dialogs",
        "AppDialogLayout.cs");

    [Fact]
    public void ProfileEditDialog_DefinesProfileEditors()
    {
        var code = File.ReadAllText(ProfileEditDialogPath);

        Assert.Contains("public sealed class ProfileEditDialog : ContentDialog", code);
        Assert.Contains("BuildFieldsGrid()", code);
        Assert.Contains("BuildValidationInfoBar()", code);
        Assert.Contains("new InfoBar", code);
        Assert.Contains("InfoBarSeverity.Error", code);
        Assert.Contains("nameof(ProfileEditViewModel.ErrorInfoMessage)", code);
        Assert.Contains("nameof(ProfileEditViewModel.IsErrorInfoVisible)", code);

        Assert.Contains("ColumnSpacing = 12", code);
        Assert.Contains("RowSpacing = 10", code);
        Assert.Contains("new GridLength(180)", code);
        Assert.Contains("new GridLength(1, GridUnitType.Star)", code);
        Assert.Contains("AddFormRow(grid, \"Логин\"", code);
        Assert.Contains("AddFormRow(grid, \"ФИО\"", code);
        Assert.Contains("AddFormRow(grid, \"Роль\"", code);
        Assert.Contains("AddFormRow(grid, \"Должность\"", code);
        Assert.Contains("AddFormRow(grid, \"Отдел\"", code);
        Assert.Contains("AddFormRow(grid, \"Пароль\"", code);
        Assert.Contains("AddFormRow(grid, \"Активирован\"", code);
        Assert.Contains("if (!ViewModel.State.IsCreateMode)", code);
        Assert.Contains("\"Логин\"", code);
        Assert.Contains("\"ФИО\"", code);
        Assert.Contains("\"Роль\"", code);
        Assert.Contains("\"Должность\"", code);
        Assert.Contains("\"Отдел\"", code);
        Assert.Contains("\"Пароль\"", code);
        Assert.Contains("\"Активирован\"", code);
        Assert.DoesNotContain("\"Последний вход\"", code);

        Assert.Contains("BuildRoleEditor()", code);
        Assert.Contains("new Flyout", code);
        Assert.Contains("BuildRoleCheckBox(\"user\"", code);
        Assert.Contains("BuildRoleCheckBox(\"admin\"", code);
        Assert.Contains("BuildRoleCheckBox(\"excel\"", code);
        Assert.Contains("BuildRoleCheckBox(\"intern\"", code);
        Assert.Contains("nameof(ProfileEditViewModel.RoleSummaryText)", code);
        Assert.Contains("nameof(ProfileEditViewModel.IsRoleUserSelected)", code);
        Assert.Contains("nameof(ProfileEditViewModel.IsRoleAdminSelected)", code);
        Assert.Contains("nameof(ProfileEditViewModel.IsRoleExcelSelected)", code);
        Assert.Contains("nameof(ProfileEditViewModel.IsRoleInternSelected)", code);

        Assert.Contains("DialogLookupEditors.BuildAutoSuggestBox", code);
        Assert.Contains("nameof(ProfileEditViewModel.PositionSuggestionLabels)", code);
        Assert.Contains("UpdatePositionOptionsAsync", code);
        Assert.Contains("CommitPositionInput", code);
        Assert.Contains("TrySelectPositionSuggestion", code);

        Assert.Contains("new ComboBox", code);
        Assert.Contains("DisplayMemberPath = \"Label\"", code);
        Assert.Contains("SelectedValuePath = \"Value\"", code);
        Assert.Contains("nameof(ProfileEditViewModel.DepartmentOptions)", code);
        Assert.Contains("nameof(ProfileEditViewModel.SelectedDepartmentId)", code);
        Assert.Contains("new PasswordBox", code);
        Assert.Contains("nameof(ProfileEditViewModel.IsActivated)", code);

        var layoutCode = File.ReadAllText(AppDialogLayoutPath);
        Assert.Contains("public static void AddFormRow", layoutCode);
        Assert.Contains("public static FrameworkElement BuildFormLabel", layoutCode);
        Assert.Contains("HorizontalAlignment = HorizontalAlignment.Right", layoutCode);
        Assert.Contains("TextAlignment = TextAlignment.Right", layoutCode);
        Assert.Contains("Grid.SetColumn(label, 0);", layoutCode);
        Assert.Contains("Grid.SetColumn(editor, 1);", layoutCode);
    }
}
