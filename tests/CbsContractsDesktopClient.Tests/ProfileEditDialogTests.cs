using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ProfileEditDialogTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string ProfileEditDialogPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "References",
        "ProfileEditDialog.cs");

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
        Assert.Contains("HorizontalAlignment = HorizontalAlignment.Right", code);
        Assert.Contains("TextAlignment = TextAlignment.Right", code);
        Assert.Contains("Grid.SetColumn(label, 0);", code);
        Assert.Contains("Grid.SetColumn(editor, 1);", code);
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

        Assert.Contains("new AutoSuggestBox", code);
        Assert.Contains("MaxSuggestionListHeight = 180", code);
        Assert.Contains("UpdateTextOnSelect = false", code);
        Assert.Contains("ItemTemplate = BuildPositionSuggestionTemplate()", code);
        Assert.Contains("nameof(ProfileEditViewModel.PositionSuggestionLabels)", code);
        Assert.Contains("UpdatePositionOptionsAsync", code);
        Assert.Contains("CommitPositionInput", code);

        Assert.Contains("new ComboBox", code);
        Assert.Contains("DisplayMemberPath = \"Label\"", code);
        Assert.Contains("SelectedValuePath = \"Value\"", code);
        Assert.Contains("nameof(ProfileEditViewModel.DepartmentOptions)", code);
        Assert.Contains("nameof(ProfileEditViewModel.SelectedDepartmentId)", code);
        Assert.Contains("new PasswordBox", code);
        Assert.Contains("nameof(ProfileEditViewModel.IsActivated)", code);
    }
}
