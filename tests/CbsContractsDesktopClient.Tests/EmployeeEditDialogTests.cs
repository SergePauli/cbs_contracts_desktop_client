using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class EmployeeEditDialogTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string EmployeeEditDialogPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "References",
        "EmployeeEditDialog.cs");

    private static readonly string DialogLookupEditorsPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "References",
        "DialogLookupEditors.cs");

    private static readonly string DialogContactsEditorPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "References",
        "DialogContactsEditor.cs");

    [Fact]
    public void EmployeeEditDialog_UsesProfileLikePositionEditorAndFilteredContragentComboBox()
    {
        var code = File.ReadAllText(EmployeeEditDialogPath);

        var lookupEditorCode = File.ReadAllText(DialogLookupEditorsPath);

        Assert.Contains("DialogLookupEditors.BuildAutoSuggestBox", code);
        Assert.Contains("nameof(EmployeeEditViewModel.PositionSuggestionLabels)", code);
        Assert.Contains("UpdatePositionOptionsAsync", code);
        Assert.Contains("CommitPositionInput", code);
        Assert.Contains("TrySelectPositionSuggestion", code);

        Assert.Contains("public static AutoSuggestBox BuildAutoSuggestBox", lookupEditorCode);
        Assert.Contains("new AutoSuggestBox", lookupEditorCode);
        Assert.Contains("MaxSuggestionListHeight = maxSuggestionListHeight", lookupEditorCode);
        Assert.Contains("UpdateTextOnSelect = false", lookupEditorCode);
        Assert.Contains("BuildSuggestionTemplate()", lookupEditorCode);
        Assert.Contains("SuggestionChosen", lookupEditorCode);
        Assert.Contains("QuerySubmitted", lookupEditorCode);
        Assert.Contains("LostFocus", lookupEditorCode);

        Assert.Contains("nameof(EmployeeEditViewModel.ContragentSuggestionLabels)", code);
        Assert.Contains("UpdateContragentOptionsAsync", code);
        Assert.Contains("CommitContragentInput", code);
        Assert.Contains("TrySelectContragentSuggestion", code);
        Assert.DoesNotContain("PlaceholderText = \"Фильтр контрагентов\"", code);
        Assert.DoesNotContain("new ComboBox", code);
        Assert.DoesNotContain("nameof(EmployeeEditViewModel.ContragentOptions)", code);
    }

    [Fact]
    public void DialogContactsEditor_ExposesReusableContactChipWithOptionalRemoveButton()
    {
        var code = File.ReadAllText(EmployeeEditDialogPath);
        var contactsEditorCode = File.ReadAllText(DialogContactsEditorPath);

        Assert.Contains("BuildContactsEditor()", code);
        Assert.Contains("new DialogContactsEditor()", code);
        Assert.Contains("public static UIElement BuildContactElement", contactsEditorCode);
        Assert.Contains("bool showRemoveButton", contactsEditorCode);
        Assert.Contains("if (showRemoveButton)", contactsEditorCode);
        Assert.Contains("public static IReadOnlyList<string> ParseContactValues", contactsEditorCode);
        Assert.Contains("ContactTypeClassifier.TryClassify", contactsEditorCode);
    }
}
