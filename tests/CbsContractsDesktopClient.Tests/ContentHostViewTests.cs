using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContentHostViewTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string ContentHostViewXamlPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ContentHostView.xaml");

    private static readonly string ContentHostViewCodeBehindPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ContentHostView.xaml.cs");

    [Fact]
    public void ContentHostView_SettingsButton_ContainsTableSettingsTooltipAndMenuFlyout()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);

        Assert.Contains("x:Name=\"HeaderSettingsButton\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"Настройки таблицы\"", xaml);
        Assert.Contains("<Button.Flyout>", xaml);
        Assert.Contains("<MenuFlyout>", xaml);
    }

    [Fact]
    public void ContentHostView_BindsMultiSelectOptionsSourcesIntoReferenceTable()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);

        Assert.Contains("MultiSelectOptionsSources=\"{Binding CurrentFilterOptionsSources}\"", xaml);
        Assert.Contains("RowDoubleTapped=\"ReferenceTableView_RowDoubleTapped\"", xaml);
    }

    [Fact]
    public void ContentHostView_DefinesEmployeeDetailFooterBelowTable()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);
        var detailXamlPath = Path.Combine(ProjectRoot, "src", "Views", "References", "EmployeeDetailView.xaml");
        var detailXaml = File.ReadAllText(detailXamlPath);

        Assert.Contains("xmlns:references=\"using:CbsContractsDesktopClient.Views.References\"", xaml);
        Assert.Contains("<references:EmployeeDetailView", xaml);
        Assert.Contains("Grid.Row=\"1\"", xaml);
        Assert.Contains("Row=\"{Binding SelectedRow}\"", xaml);
        Assert.Contains("Visibility=\"{Binding ShowEmployeeDetailView, Converter={StaticResource BoolVisibilityConverter}}\"", xaml);
        Assert.Contains("Height=\"130\"", detailXaml);
        Assert.Contains("Background=\"{StaticResource ShellTableHeaderBackgroundBrush}\"", detailXaml);
        Assert.Contains("<ColumnDefinition Width=\"15*\" />", detailXaml);
        Assert.Contains("<ColumnDefinition Width=\"85*\" />", detailXaml);
        Assert.Contains("HorizontalAlignment=\"Right\"", detailXaml);
        Assert.Contains("HorizontalAlignment=\"Left\"", detailXaml);
        Assert.Contains("x:Name=\"EmployeeDismissedStatusTextBlock\"", detailXaml);
        Assert.Contains("x:Name=\"ContactsPanel\"", detailXaml);
        Assert.Contains("Text=\"Должность\"", detailXaml);
        Assert.Contains("Text=\"Контакты\"", detailXaml);
    }

    [Fact]
    public void ContentHostView_DefinesContragentDetailFooterBelowTable()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);
        var detailXamlPath = Path.Combine(ProjectRoot, "src", "Views", "References", "ContragentDetailView.xaml");
        var detailCodePath = Path.Combine(ProjectRoot, "src", "Views", "References", "ContragentDetailView.xaml.cs");
        var employeeBoxXamlPath = Path.Combine(ProjectRoot, "src", "Views", "References", "EmployeeBox.xaml");
        var employeeBoxCodePath = Path.Combine(ProjectRoot, "src", "Views", "References", "EmployeeBox.xaml.cs");
        var detailXaml = File.ReadAllText(detailXamlPath);
        var detailCode = File.ReadAllText(detailCodePath);
        var employeeBoxXaml = File.ReadAllText(employeeBoxXamlPath);
        var employeeBoxCode = File.ReadAllText(employeeBoxCodePath);

        Assert.Contains("<references:ContragentDetailView", xaml);
        Assert.Contains("Grid.Row=\"1\"", xaml);
        Assert.Contains("Row=\"{Binding SelectedRow}\"", xaml);
        Assert.Contains("EmployeeEditRequested=\"ContragentDetailView_EmployeeEditRequested\"", xaml);
        Assert.Contains("Visibility=\"{Binding ShowContragentDetailView, Converter={StaticResource BoolVisibilityConverter}}\"", xaml);
        Assert.Contains("Height=\"170\"", detailXaml);
        Assert.Contains("<ColumnDefinition Width=\"60*\" />", detailXaml);
        Assert.Contains("<ColumnDefinition Width=\"40*\" />", detailXaml);
        Assert.Contains("<ColumnDefinition Width=\"13*\" />", detailXaml);
        Assert.Contains("<ColumnDefinition Width=\"87*\" />", detailXaml);
        Assert.Contains("x:Name=\"ContragentNameTextBlock\"", detailXaml);
        Assert.Contains("x:Name=\"ContactsPanel\"", detailXaml);
        Assert.Contains("<references:EmployeeBox x:Name=\"EmployeesBox\" />", detailXaml);
        Assert.Contains("Padding=\"12,0,0,0\"", detailXaml);
        Assert.Contains("public event EventHandler<EmployeeBoxEditRequestedEventArgs>? EmployeeEditRequested;", detailCode);
        Assert.Contains("EmployeesBox.EditRequested += (_, args) => EmployeeEditRequested?.Invoke(this, args);", detailCode);
        Assert.Contains("EmployeesBox.Employees = ReadEmployees(row);", detailCode);
        Assert.Contains("\"requisites.organization.full_name\"", detailCode);
        Assert.Contains("FormatPart(\"ИНН\", GetText(row, \"requisites.organization.inn\", \"inn\"))", detailCode);
        Assert.Contains("FormatPart(\"КПП\", GetText(row, \"requisites.organization.kpp\", \"kpp\"))", detailCode);
        Assert.Contains("FormatPart(\"Подразделение\", GetText(row, \"requisites.organization.division\", \"division\", \"contragent.division\"))", detailCode);
        Assert.DoesNotContain("FormatPart(\"ОГРН\"", detailCode);
        Assert.DoesNotContain("FormatPart(\"ОКПО\"", detailCode);
        Assert.Contains("\"contacts.contact_attributes.value\"", detailCode);
        Assert.Contains("\"contacts.contact_attributes.name\"", detailCode);
        Assert.Contains("\"contacts.contact.name\"", detailCode);
        Assert.Contains("ReadNestedContactValue(item, \"contact_attributes\")", detailCode);
        Assert.Contains("ReadNestedContactValue(item, \"contact\")", detailCode);
        Assert.Contains("\"real_addr.address.value\"", detailCode);
        Assert.Contains("x:Class=\"CbsContractsDesktopClient.Views.References.EmployeeBox\"", employeeBoxXaml);
        Assert.Contains("x:Name=\"EmployeesListView\"", employeeBoxXaml);
        Assert.Contains("SelectionMode=\"Single\"", employeeBoxXaml);
        Assert.Contains("Text=\"Сотрудники\"", employeeBoxXaml);
        Assert.Contains("Text=\"НЕТ ЗАПИСЕЙ\"", employeeBoxXaml);
        Assert.Contains("public event EventHandler<EmployeeBoxEditRequestedEventArgs>? EditRequested;", employeeBoxCode);
        Assert.Contains("Padding = new Thickness(2)", employeeBoxCode);
        Assert.Contains("Padding = new Thickness(2, 4, 2, 4)", employeeBoxCode);
        Assert.Contains("ShellAccentPanelBackgroundAltBrush", employeeBoxCode);
        Assert.Contains("DisableContainerHover(listViewItem);", employeeBoxCode);
        Assert.Contains("ListViewItemBackgroundPointerOver", employeeBoxCode);
        Assert.Contains("ListViewItemBackgroundPointerOverSelected", employeeBoxCode);
        Assert.Contains("ToolTipService.SetToolTip(listViewItem, employee.Description);", employeeBoxCode);
        Assert.DoesNotContain("ToolTipService.SetToolTip(name, employee.Description);", employeeBoxCode);
        Assert.Contains("Clipboard.SetContent(dataPackage);", employeeBoxCode);
        Assert.Contains("ContactTypeClassifier.TryClassify(contact, out var match)", employeeBoxCode);
        Assert.DoesNotContain("employee.Contacts.Take(4)", employeeBoxCode);
        Assert.Contains("nameof(CanEdit)", employeeBoxCode);
        Assert.Contains("string.Equals(role, \"intern\", StringComparison.OrdinalIgnoreCase)", employeeBoxCode);
    }

    [Fact]
    public void ContentHostView_SettingsMenu_DefinesThreeResetCommands()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);

        Assert.Contains("Text=\"Сбросить ширину\"", xaml);
        Assert.Contains("Click=\"ResetColumnWidthsMenuItem_Click\"", xaml);

        Assert.Contains("Text=\"Сбросить фильтры\"", xaml);
        Assert.Contains("Click=\"ResetFiltersMenuItem_Click\"", xaml);

        Assert.Contains("Text=\"Сбросить сортировку\"", xaml);
        Assert.Contains("Click=\"ResetSortingMenuItem_Click\"", xaml);
    }

    [Fact]
    public void ContentHostView_CodeBehind_ImplementsThreeSettingsMenuHandlers()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("private async void ResetColumnWidthsMenuItem_Click", codeBehind);
        Assert.Contains("private async void ResetFiltersMenuItem_Click", codeBehind);
        Assert.Contains("private async void ResetSortingMenuItem_Click", codeBehind);
    }

    [Fact]
    public void ContentHostView_PreparesProfileEditStateScaffold()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("await ShowReferenceEditDialogAsync(isCreateMode: true);", codeBehind);
        Assert.Contains("await ShowReferenceEditDialogAsync(isCreateMode: false);", codeBehind);
        Assert.Contains("if (_viewModel.CurrentReference.EditorKind == ReferenceEditorKind.Profile)", codeBehind);
        Assert.Contains("await ShowProfileEditDialogAsync(isCreateMode);", codeBehind);
        Assert.Contains("if (_viewModel.CurrentReference.EditorKind == ReferenceEditorKind.Employee)", codeBehind);
        Assert.Contains("await ShowEmployeeEditDialogAsync(isCreateMode);", codeBehind);
        Assert.Contains("var viewModel = new ProfileEditViewModel(state, LoadPositionOptionsAsync);", codeBehind);
        Assert.Contains("var dialog = new ProfileEditDialog(viewModel)", codeBehind);
        Assert.Contains("ProfileEditPayloadBuilder.BuildForCreate(viewModel)", codeBehind);
        Assert.Contains("ProfileEditPayloadBuilder.BuildForUpdate(viewModel)", codeBehind);
        Assert.Contains("await _referenceCrudService.CreateAsync(definition, payload)", codeBehind);
        Assert.Contains("await _referenceCrudService.UpdateAsync(definition, payload)", codeBehind);
        Assert.Contains("CreateProfileEditDialogState", codeBehind);
        Assert.Contains("ProfileEditStateFactory.Create(", codeBehind);
        Assert.Contains("private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadPositionOptionsAsync(", codeBehind);
        Assert.Contains("private async Task<ReferenceDataRow?> LoadEmployeeEditRowAsync(", codeBehind);
        Assert.Contains("long? employeeId = null", codeBehind);
        Assert.Contains("Preset = \"edit\"", codeBehind);
        Assert.Contains("[\"id__eq\"] = id.Value", codeBehind);
        Assert.Contains("var viewModel = new EmployeeEditViewModel(state, LoadPositionOptionsAsync, LoadContragentOptionsAsync);", codeBehind);
        Assert.Contains("EmployeeEditPayloadBuilder.BuildForCreate(viewModel)", codeBehind);
        Assert.Contains("EmployeeEditPayloadBuilder.BuildForUpdate(viewModel)", codeBehind);
        Assert.Contains("private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadContragentOptionsAsync(", codeBehind);
        Assert.Contains("Model = \"Position\"", codeBehind);
        Assert.Contains("Preset = \"item\"", codeBehind);
        Assert.Contains("[\"name__cnt\"] = normalizedSearchText", codeBehind);
        Assert.Contains(".OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)", codeBehind);
    }

    [Fact]
    public void ContentHostView_OpensEditOnRowDoubleClick_ExceptInternRole()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("private async void ReferenceTableView_RowDoubleTapped(object sender, CbsTableRowDoubleTappedEventArgs e)", codeBehind);
        Assert.Contains("_viewModel.SelectedRow = e.Row;", codeBehind);
        Assert.Contains("if (IsInternEditBlocked())", codeBehind);
        Assert.Contains("await ShowReferenceEditDialogAsync(isCreateMode: false);", codeBehind);
        Assert.Contains("private bool IsInternEditBlocked()", codeBehind);
        Assert.Contains("string.Equals(role, \"intern\", System.StringComparison.OrdinalIgnoreCase)", codeBehind);
    }

    [Fact]
    public void ContentHostView_OpensEmployeeEditDialog_FromContragentEmployeeBox()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("private async void ContragentDetailView_EmployeeEditRequested", codeBehind);
        Assert.Contains("e.Employee.Id is not long employeeId", codeBehind);
        Assert.Contains("_referenceDefinitionService.TryGetByRoute(\"/employees\", out var employeeDefinition)", codeBehind);
        Assert.Contains("await ShowEmployeeEditDialogAsync(isCreateMode: false, employeeId, employeeDefinition);", codeBehind);
        Assert.Contains("var definition = employeeDefinition ?? _viewModel.CurrentReference;", codeBehind);
        Assert.Contains("sourceRow = await LoadEmployeeEditRowAsync(employeeId);", codeBehind);
        Assert.Contains("EmployeeEditStateFactory.Create(definition, isCreateMode, sourceRow)", codeBehind);
    }

    [Fact]
    public void ContentHostView_DefinesHolidayRecalcActionButton()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("x:Name=\"HolidayRecalcButton\"", xaml);
        Assert.Contains("Click=\"HolidayRecalcButton_Click\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"Пересчитать сроки этапов\"", xaml);
        Assert.Contains("private async void HolidayRecalcButton_Click(object sender, RoutedEventArgs e)", codeBehind);
        Assert.Contains("await RecalculateHolidayStagesAsync();", codeBehind);
        Assert.Contains("private async Task RecalculateHolidayStagesAsync()", codeBehind);
        Assert.Contains("LoadAffectedStagesAsync", codeBehind);
        Assert.Contains("LoadHolidayCalendarAsync", codeBehind);
        Assert.Contains("BuildStagePatch", codeBehind);
    }
}
