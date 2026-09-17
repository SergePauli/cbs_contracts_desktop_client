using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class EmployeeEditDialogTests
{
    private static readonly string EmployeeEditDialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "References",
        "EmployeeEditDialog.cs");

    private static readonly string DialogLookupEditorsPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "References",
        "DialogLookupEditors.cs");

    private static readonly string DialogContactsEditorPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "References",
        "DialogContactsEditor.cs");

    [Fact]
    public void EmployeeEditDialog_UsesStaticXamlWithPositionAndContragentSuggestions()
    {
        var code = File.ReadAllText(EmployeeEditDialogPath);
        var xaml = File.ReadAllText(Path.ChangeExtension(EmployeeEditDialogPath, ".xaml"));

        var lookupEditorCode = File.ReadAllText(DialogLookupEditorsPath);

        Assert.Contains("public sealed partial class EmployeeEditDialog : AppEditDialog", code);
        Assert.Contains("InitializeComponent();", code);
        Assert.Contains("public override bool Validate()", code);
        Assert.Contains("<AutoSuggestBox", xaml);
        Assert.Contains("ItemsSource=\"{Binding PositionSuggestionLabels}\"", xaml);
        Assert.Contains("UpdatePositionOptionsAsync", code);
        Assert.Contains("CommitPositionInput", code);
        Assert.Contains("ViewModel.SelectPositionOption(ViewModel.FindPositionOption", code);

        Assert.Contains("public static AutoSuggestBox BuildAutoSuggestBox", lookupEditorCode);
        Assert.Contains("new AutoSuggestBox", lookupEditorCode);
        Assert.Contains("MaxSuggestionListHeight = maxSuggestionListHeight", lookupEditorCode);
        Assert.Contains("UpdateTextOnSelect = false", lookupEditorCode);
        Assert.Contains("BuildSuggestionTemplate()", lookupEditorCode);
        Assert.Contains("SuggestionChosen", lookupEditorCode);
        Assert.Contains("QuerySubmitted", lookupEditorCode);
        Assert.Contains("LostFocus", lookupEditorCode);

        Assert.Contains("ItemsSource=\"{Binding ContragentSuggestionLabels}\"", xaml);
        Assert.Contains("UpdateContragentOptionsAsync", code);
        Assert.Contains("CommitContragentInput", code);
        Assert.Contains("ViewModel.SelectContragentOption(ViewModel.FindContragentOption", code);
        Assert.DoesNotContain("PlaceholderText = \"Фильтр контрагентов\"", code);
        Assert.DoesNotContain("new ComboBox", code);
        Assert.DoesNotContain("nameof(EmployeeEditViewModel.ContragentOptions)", code);
    }

    [Fact]
    public void DialogContactsEditor_ExposesReusableContactChipWithOptionalRemoveButton()
    {
        var code = File.ReadAllText(EmployeeEditDialogPath);
        var xaml = File.ReadAllText(Path.ChangeExtension(EmployeeEditDialogPath, ".xaml"));
        var contactsEditorCode = File.ReadAllText(DialogContactsEditorPath);

        Assert.Contains("<local:DialogContactsEditor", xaml);
        Assert.Contains("ContactsText=\"{Binding ContactsText, Mode=TwoWay}\"", xaml);
        Assert.Contains("public static UIElement BuildContactElement", contactsEditorCode);
        Assert.Contains("bool showRemoveButton", contactsEditorCode);
        Assert.Contains("if (showRemoveButton)", contactsEditorCode);
        Assert.Contains("public static IReadOnlyList<string> ParseContactValues", contactsEditorCode);
        Assert.Contains("ContactTypeClassifier.TryClassify", contactsEditorCode);
    }
}
