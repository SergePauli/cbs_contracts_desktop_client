using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContragentEditDialogTests
{
    private static readonly string DialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "References",
        "ContragentEditDialog.cs");

    [Fact]
    public void ContragentEditDialog_UsesWebLikeTabsAndFields()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("public sealed class ContragentEditDialog : ContentDialog", code);
        Assert.Contains("using CbsContractsDesktopClient.Views.Controls;", code);
        Assert.Contains("Title = ViewModel.DialogTitle;", code);
        Assert.Contains("Resources[\"ContentDialogMinWidth\"] = 650d;", code);
        Assert.Contains("Resources[\"ContentDialogMinHeight\"] = 600d;", code);
        Assert.Contains("Resources[\"ContentDialogMaxWidth\"] = 920d;", code);
        Assert.Contains("Content = BuildContent();", code);
        Assert.Contains("DialogChrome.Apply(this);", code);
        Assert.Contains("Loaded += OnLoaded;", code);
        Assert.Contains("SuppressKeyboardAcceleratorTooltips(this);", code);
        Assert.Contains("element.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementModeEnum.Hidden;", code);
        Assert.Contains("Header = \"Основное\"", code);
        Assert.Contains("Header = \"Коды и счета\"", code);
        Assert.Contains("Header = \"Регистрации\"", code);
        Assert.Contains("BuildRegistrationsView()", code);
        Assert.Contains("ViewModel.OrganizationHistory", code);
        Assert.Contains("RenderRegistrationTimeline()", code);
        Assert.Contains("BuildRegistrationHistoryItem(organizations[index], index, organizations.Count)", code);
        Assert.Contains("new Rectangle", code);
        Assert.Contains("new Ellipse", code);
        Assert.Contains("RegistrationUsedCheckBox_Checked", code);
        Assert.Contains("ViewModel.ActivateRegistration(registration)", code);
        Assert.Contains("ViewModel.VisibleOrganizationHistory", code);
        Assert.Contains("DeleteRegistrationButton_Click", code);
        Assert.Contains("ViewModel.MarkRegistrationForDestroy(registration)", code);
        Assert.Contains("ToolTipService.SetToolTip(deleteButton, \"Удалить регистрацию\");", code);
        Assert.Contains("AddFormRow(grid, \"ИНН\"", code);
        Assert.Contains("AddFormRow(grid, \"КПП\"", code);
        Assert.Contains("AddFormRow(grid, \"КодПодр.\"", code);
        Assert.Contains("AddFormRow(grid, \"Форма\"", code);
        Assert.Contains("AddFormRow(grid, \"Наименование\"", code);
        Assert.Contains("AddFormRow(grid, \"Регион\"", code);
        Assert.Contains("AddFormRow(grid, \"Адрес фактический\"", code);
        Assert.Contains("AddFormRow(grid, \"Контакты\"", code);
        Assert.Contains("AddFormRow(grid, \"ОГРН\"", code);
        Assert.Contains("AddFormRow(grid, \"Наименование банка\"", code);
        Assert.DoesNotContain("Смена юр.лица", code);
        Assert.DoesNotContain("BuildNewRequisitesEditor", code);
        Assert.Contains("DialogContactsEditor.ContactsTextProperty", code);
        Assert.Contains("nameof(ContragentEditViewModel.SelectedOwnershipId)", code);
        Assert.Contains("nameof(ContragentEditViewModel.SelectedRegionId)", code);
    }
}
