using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ProfileEditDialogTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

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
        Assert.Contains("new Grid", code);
        Assert.Contains("ColumnSpacing = 12", code);
        Assert.Contains("RowSpacing = 10", code);
        Assert.Contains("new GridLength(180)", code);
        Assert.Contains("new GridLength(1, GridUnitType.Star)", code);
        Assert.Contains("HorizontalAlignment = HorizontalAlignment.Right", code);
        Assert.Contains("TextAlignment = TextAlignment.Right", code);
        Assert.Contains("Grid.SetColumn(label, 0);", code);
        Assert.Contains("Grid.SetColumn(editor, 1);", code);
        Assert.Contains("BuildDepartmentEditor()", code);
        Assert.Contains("BuildPositionEditor()", code);
        Assert.Contains("BuildPasswordEditor()", code);
        Assert.Contains("BuildActivatedEditor()", code);
        Assert.Contains("if (!ViewModel.State.IsCreateMode)", code);
        Assert.Contains("new AutoSuggestBox", code);
        Assert.Contains("MaxSuggestionListHeight = 180", code);
        Assert.Contains("UpdateTextOnSelect = false", code);
        Assert.Contains("ItemTemplate = BuildPositionSuggestionTemplate()", code);
        Assert.Contains("nameof(ProfileEditViewModel.PositionSuggestionLabels)", code);
        Assert.Contains("AutoSuggestionBoxTextChangeReason.UserInput", code);
        Assert.Contains("autoSuggestBox.SuggestionChosen += (_, args) =>", code);
        Assert.Contains("autoSuggestBox.QuerySubmitted += (_, args) =>", code);
        Assert.Contains("autoSuggestBox.LostFocus += (_, _) =>", code);
        Assert.Contains("ViewModel.FindPositionOption", code);
        Assert.Contains("XamlReader.Load(", code);
        Assert.Contains("Text=\"{Binding}\"", code);
        Assert.Contains("Padding=\"8,4,8,4\"", code);
        Assert.Contains("MinHeight=\"28\"", code);
        Assert.Contains("UpdatePositionOptionsAsync", code);
        Assert.Contains("CommitPositionInput", code);
        Assert.Contains("new ComboBox", code);
        Assert.Contains("new PasswordBox", code);
        Assert.Contains("ToggleButton.IsCheckedProperty", code);
        Assert.Contains("\"Пароль\"", code);
        Assert.Contains("\"Активирован\"", code);
        Assert.Contains("\"Логин\"", code);
        Assert.Contains("\"ФИО\"", code);
        Assert.Contains("\"Должность\"", code);
        Assert.Contains("\"Отдел\"", code);
        Assert.DoesNotContain("\"Последний вход\"", code);
        Assert.Contains("IsTabStop = false", code);
        Assert.Contains("MinWidth = 280", code);
        Assert.Contains("DisplayMemberPath = \"Label\"", code);
        Assert.Contains("SelectedValuePath = \"Value\"", code);
        Assert.Contains("nameof(ProfileEditViewModel.DepartmentOptions)", code);
        Assert.Contains("nameof(ProfileEditViewModel.SelectedDepartmentId)", code);
        Assert.Contains("nameof(ProfileEditViewModel.IsActivated)", code);
    }
}
