using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContentHostViewTests
{
    private static readonly string ProjectRoot = TestProjectPaths.RepositoryRoot;

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

    private static readonly string ContentHostDialogCoordinatorPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ContentHostDialogCoordinator.cs");

    private static readonly string ContentHostViewBasePath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ContentHostViewBase.cs");

    private static readonly string ContentHostRouterViewXamlPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ContentHostRouterView.xaml");

    private static readonly string ContentHostRouterViewCodeBehindPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ContentHostRouterView.xaml.cs");

    private static readonly string ReferenceHostViewXamlPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ReferenceHostView.xaml");

    private static readonly string ReferenceHostViewCodeBehindPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ReferenceHostView.xaml.cs");

    private static readonly string TableHostViewXamlPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "TableHostView.xaml");

    private static readonly string TableHostViewCodeBehindPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "TableHostView.xaml.cs");

    private static readonly string AppShellPageXamlPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "AppShellPage.xaml");

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

        Assert.Contains("Store=\"{Binding}\"", xaml);
        Assert.Contains("RowDoubleTapped=\"ReferenceTableView_RowDoubleTapped\"", xaml);
        Assert.Contains("RowSelectionChanged=\"ReferenceTableView_RowSelectionChanged\"", xaml);
        Assert.Contains("<shell:TableHostView", xaml);
        Assert.DoesNotContain("<controls:CbsTableView", xaml);
        Assert.DoesNotContain("xmlns:controls=\"using:CbsContractsDesktopClient.Views.Controls\"", xaml);
    }

    [Fact]
    public void TableHostView_OwnsCbsTableViewZone()
    {
        var xaml = File.ReadAllText(TableHostViewXamlPath);
        var codeBehind = File.ReadAllText(TableHostViewCodeBehindPath);

        Assert.Contains("x:Class=\"CbsContractsDesktopClient.Views.Shell.TableHostView\"", xaml);
        Assert.Contains("x:Name=\"Root\"", xaml);
        Assert.Contains("Background=\"{StaticResource ShellMutedPanelBackgroundBrush}\"", xaml);
        Assert.Contains("<controls:CbsTableView", xaml);
        Assert.Contains("x:Name=\"TableView\"", xaml);
        Assert.Contains("Columns=\"{Binding Store.CurrentColumns, ElementName=Root}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Store.Items, ElementName=Root}\"", xaml);
        Assert.Contains("SelectedItem=\"{Binding Store.SelectedRow, ElementName=Root, Mode=TwoWay}\"", xaml);
        Assert.Contains("public static readonly DependencyProperty StoreProperty", codeBehind);
        Assert.Contains("public static readonly DependencyProperty ColumnsProperty", codeBehind);
        Assert.Contains("public static readonly DependencyProperty ItemsSourceProperty", codeBehind);
        Assert.Contains("public static readonly DependencyProperty RowHeightProperty", codeBehind);
        Assert.Contains("public static readonly DependencyProperty SelectedItemProperty", codeBehind);
        Assert.Contains("public static readonly DependencyProperty RetainedBufferRowsProperty", codeBehind);
        Assert.Contains("public event EventHandler<CbsTableSortRequestedEventArgs>? SortRequested;", codeBehind);
        Assert.Contains("public event EventHandler<CbsTableRowSelectionChangedEventArgs>? RowSelectionChanged;", codeBehind);
        Assert.Contains("TableView.RowSelectionChanged += (_, args) =>", codeBehind);
        Assert.Contains("SelectedItem = args.Row;", codeBehind);
        Assert.Contains("TableView.ApplyFilterInputs(filters);", codeBehind);
        Assert.Contains("TableView.ClearFilterInputs();", codeBehind);
    }

    [Fact]
    public void AppShellPage_UsesContentHostRouterView()
    {
        var xaml = File.ReadAllText(AppShellPageXamlPath);

        Assert.Contains("<shell:ContentHostRouterView Grid.Row=\"1\"", xaml);
        Assert.DoesNotContain("<shell:ContentHostView Grid.Row=\"1\"", xaml);
    }

    [Fact]
    public void ContentHostRouterView_MapsRoutesToConcreteHosts()
    {
        var xaml = File.ReadAllText(ContentHostRouterViewXamlPath);
        var codeBehind = File.ReadAllText(ContentHostRouterViewCodeBehindPath);

        Assert.Contains("x:Class=\"CbsContractsDesktopClient.Views.Shell.ContentHostRouterView\"", xaml);
        Assert.Contains("ContentControl x:Name=\"HostContentControl\"", xaml);
        Assert.Contains("ITablePageDefinitionService", codeBehind);
        Assert.Contains("ResolveRouteKind", codeBehind);
        Assert.Contains("ContentHostRouteKind.Reference => GetReferenceHostView(route)", codeBehind);
        Assert.Contains("ContentHostRouteKind.Table => GetTableHostView(route)", codeBehind);
        Assert.Contains("UpdateCurrentHostRoute(routeKind, route);", codeBehind);
        Assert.Contains("private static bool IsSimpleReferenceRoute", codeBehind);
        Assert.Contains("route.StartsWith(\"/references/\", StringComparison.OrdinalIgnoreCase)", codeBehind);
        Assert.Contains("string.Equals(route, \"/holidays\", StringComparison.OrdinalIgnoreCase)", codeBehind);
        Assert.Contains("private ReferenceHostView GetReferenceHostView(string? route)", codeBehind);
        Assert.Contains("private ContentHostView GetTableHostView(string? route)", codeBehind);
        Assert.Contains("new ContentHostView()", codeBehind);
    }

    [Fact]
    public void ReferenceHostView_AcceptsRouteInput()
    {
        var xaml = File.ReadAllText(ReferenceHostViewXamlPath);
        var codeBehind = File.ReadAllText(ReferenceHostViewCodeBehindPath);

        Assert.Contains("x:Class=\"CbsContractsDesktopClient.Views.Shell.ReferenceHostView\"", xaml);
        Assert.Contains("<shell:ContentHostViewBase", xaml);
        Assert.Contains("<shell:TableHostView", xaml);
        Assert.Contains("x:Name=\"ReferenceTableView\"", xaml);
        Assert.Contains("Store=\"{Binding}\"", xaml);
        Assert.Contains("x:Name=\"DetailHost\"", xaml);
        Assert.Contains("Click=\"CreateRowButton_Click\"", xaml);
        Assert.Contains("Click=\"EditSelectedRowButton_Click\"", xaml);
        Assert.Contains("Click=\"DeleteSelectedRowButton_Click\"", xaml);
        Assert.Contains("public sealed partial class ReferenceHostView : ContentHostViewBase", codeBehind);
        Assert.Contains("public ReferenceHostView(string route)", codeBehind);
        Assert.Contains("public string? Route", codeBehind);
        Assert.Contains("ITablePageDefinitionService", codeBehind);
        Assert.Contains("IReferenceDefinitionService", codeBehind);
        Assert.Contains("NavigateToRouteAsync(value)", codeBehind);
        Assert.Contains("_tablePageDefinitionService.TryGetByRoute(route, out var definition)", codeBehind);
        Assert.DoesNotContain("_tableHostView", codeBehind);
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
        Assert.Contains("Height=\"215\"", detailXaml);
        Assert.Contains("<ColumnDefinition Width=\"60*\" />", detailXaml);
        Assert.Contains("<ColumnDefinition Width=\"40*\" />", detailXaml);
        Assert.Contains("<ColumnDefinition Width=\"13*\" />", detailXaml);
        Assert.Contains("<ColumnDefinition Width=\"87*\" />", detailXaml);
        Assert.Contains("x:Name=\"OwnershipFullNameTextBlock\"", detailXaml);
        Assert.Contains("x:Name=\"OwnershipCodeTextBlock\"", detailXaml);
        Assert.Contains("Text=\"Форма\"", detailXaml);
        Assert.DoesNotContain("x:Name=\"ContragentNameTextBlock\"", detailXaml);
        Assert.DoesNotContain("x:Name=\"ContragentIdTextBlock\"", detailXaml);
        Assert.Contains("x:Name=\"ContactsPanel\"", detailXaml);
        Assert.Contains("x:Name=\"ContractLinksPanel\"", detailXaml);
        Assert.Contains("Text=\"Контракты\"", detailXaml);
        Assert.Contains("public string BuildClipboardText()", detailCode);
        Assert.Contains("AddClipboardLine(lines, \"Форма\"", detailCode);
        Assert.Contains("AddClipboardLine(lines, \"Полное имя\"", detailCode);
        Assert.Contains("AddClipboardLine(lines, \"Реквизиты\"", detailCode);
        Assert.Contains("AddClipboardLine(lines, \"Описание\"", detailCode);
        Assert.Contains("AddClipboardLine(lines, \"Контакты\"", detailCode);
        Assert.Contains("AddClipboardLine(lines, \"Адреса\"", detailCode);
        Assert.Contains("AddClipboardLine(lines, \"Контракты\"", detailCode);
        Assert.DoesNotContain("AddClipboardLine(lines, \"Сотрудники\"", detailCode);
        Assert.Contains("BuildContractLinksClipboardText(ContractsRow)", detailCode);
        Assert.Contains("<references:EmployeeBox x:Name=\"EmployeesBox\" />", detailXaml);
        Assert.Contains("Style=\"{StaticResource ShellDetailFooterColumnSeparatorStyle}\"", detailXaml);
        Assert.DoesNotContain("Padding=\"12,0,0,0\"", detailXaml);
        Assert.Contains("public event EventHandler<EmployeeBoxEditRequestedEventArgs>? EmployeeEditRequested;", detailCode);
        Assert.Contains("EmployeesBox.EditRequested += (_, args) => EmployeeEditRequested?.Invoke(this, args);", detailCode);
        Assert.Contains("EmployeesBox.Employees = ReadEmployees(row);", detailCode);
        Assert.Contains("public TableDataRow? ContractsRow", detailCode);
        Assert.Contains("RefreshContractLinks();", detailCode);
        Assert.Contains("ReadContractLinks(ContractsRow)", detailCode);
        Assert.Contains("ContractLinksPanel.Children.Add(new HyperlinkButton", detailCode);
        Assert.Contains("private sealed record ContractLinkItem", detailCode);
        Assert.Contains("\"contragent.contracts\"", detailCode);
        Assert.Contains("var title = ReadStringProperty(item, \"name\");", detailCode);
        Assert.DoesNotContain("ReadStringProperty(item, \"list_key\")", detailCode);
        Assert.Contains("\"requisites.organization.ownership.full_name\"", detailCode);
        Assert.Contains("\"requisites.organization.ownership.code\"", detailCode);
        Assert.Contains("\"requisites.organization.okopf\"", detailCode);
        Assert.Contains("_referenceLookupCacheService = App.Services.GetService<IReferenceLookupCacheService>();", detailCode);
        Assert.Contains("RefreshOwnershipFromReferenceAsync(row, refreshVersion)", detailCode);
        Assert.Contains("_referenceLookupCacheService.FindOwnershipAsync(", detailCode);
        Assert.Contains("GetOwnershipId(row)", detailCode);
        Assert.Contains("GetOwnershipCode(row)", detailCode);
        Assert.Contains("ownership.FullName", detailCode);
        Assert.Contains("ownership.Code", detailCode);
        Assert.Contains("код: {code}", detailCode);
        Assert.DoesNotContain("ID: {id}", detailCode);
        Assert.Contains("\"requisites.organization.full_name\"", detailCode);
        Assert.Contains("FormatPart(\"ИНН\", TryGetText(row, \"requisites.organization.inn\", \"inn\"))", detailCode);
        Assert.Contains("FormatPart(\"КПП\", TryGetText(row, \"requisites.organization.kpp\", \"kpp\"))", detailCode);
        Assert.Contains("FormatPart(\"Подразделение\", TryGetText(row, \"requisites.organization.division\", \"division\", \"contragent.division\"))", detailCode);
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
    public void ContentHostView_FilterResetButtonAndMenuUseDifferentActions()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("private async void ResetFiltersButton_Click", codeBehind);
        Assert.Contains("var filters = await _viewModel.ResetFiltersAsync();", codeBehind);
        Assert.Contains("private async void ResetFiltersMenuItem_Click", codeBehind);
        Assert.Contains("var confirmed = await ConfirmFilterClearAsync();", codeBehind);
        Assert.Contains("var filters = await _viewModel.ClearFiltersAsync();", codeBehind);
        Assert.Contains("private async Task<bool> ConfirmFilterClearAsync()", codeBehind);
        Assert.Contains("ConfirmDialogAsync(", codeBehind);
        Assert.Contains("Очистить все фильтры текущей таблицы?", codeBehind);
    }

    [Fact]
    public void ContentHostView_PreparesProfileEditStateScaffold()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("await ShowReferenceEditDialogAsync(isCreateMode: true);", codeBehind);
        Assert.Contains("ShowContragentCreateMenu(anchor);", codeBehind);
        Assert.Contains("new MenuFlyoutSubItem", codeBehind);
        Assert.Contains("Text = \"Смена юр.лица\"", codeBehind);
        Assert.DoesNotContain("IsEnabled = false", codeBehind);
        Assert.Contains("await ChangeContragentLegalEntityFromFnsAsync();", codeBehind);
        Assert.Contains("await ChangeContragentLegalEntityManuallyAsync();", codeBehind);
        Assert.Contains("Text = \"Импорт из ФНС\"", codeBehind);
        Assert.Contains("Text = \"Ручной ввод\"", codeBehind);
        Assert.Contains("await ImportContragentFromFnsAsync();", codeBehind);
        Assert.Contains("await ShowReferenceEditDialogAsync(isCreateMode: false);", codeBehind);
        Assert.Contains("if (_viewModel.CurrentReference.EditorKind == ReferenceEditorKind.Profile)", codeBehind);
        Assert.Contains("await ShowProfileEditDialogAsync(isCreateMode);", codeBehind);
        Assert.Contains("if (_viewModel.CurrentReference.EditorKind == ReferenceEditorKind.Employee)", codeBehind);
        Assert.Contains("await ShowEmployeeEditDialogAsync(isCreateMode);", codeBehind);
        Assert.Contains("if (_viewModel.CurrentReference.EditorKind == ReferenceEditorKind.Contragent)", codeBehind);
        Assert.Contains("await ShowContragentEditDialogAsync(isCreateMode);", codeBehind);
        Assert.Contains("var viewModel = new ProfileEditViewModel(state, LoadPositionOptionsAsync);", codeBehind);
        Assert.Contains("var dialog = new ProfileEditDialog(viewModel)", codeBehind);
        Assert.Contains("ProfileEditPayloadBuilder.BuildForCreate(viewModel)", codeBehind);
        Assert.Contains("ProfileEditPayloadBuilder.BuildForUpdate(viewModel)", codeBehind);
        Assert.Contains("await _modelMutationService.CreateAsync(definition.Model, payload)", codeBehind);
        Assert.Contains("await _modelMutationService.UpdateAsync(definition.Model, payload)", codeBehind);
        Assert.Contains("CreateProfileEditDialogState", codeBehind);
        Assert.Contains("ProfileEditStateFactory.Create(", codeBehind);
        Assert.Contains("private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadPositionOptionsAsync(", codeBehind);
        Assert.Contains("private async Task<TableDataRow?> LoadEmployeeEditRowAsync(", codeBehind);
        Assert.Contains("long? employeeId = null", codeBehind);
        Assert.Contains("Preset = \"edit\"", codeBehind);
        Assert.Contains("[\"id__eq\"] = id.Value", codeBehind);
        Assert.Contains("var viewModel = new EmployeeEditViewModel(state, LoadPositionOptionsAsync, LoadContragentOptionsAsync);", codeBehind);
        Assert.Contains("EmployeeEditPayloadBuilder.BuildForCreate(viewModel)", codeBehind);
        Assert.Contains("EmployeeEditPayloadBuilder.BuildForUpdate(viewModel)", codeBehind);
        Assert.Contains("var viewModel = new ContragentEditViewModel(state, LoadAddressOptionsAsync);", codeBehind);
        Assert.Contains("ContragentEditPayloadBuilder.BuildForCreate(viewModel)", codeBehind);
        Assert.Contains("ContragentEditPayloadBuilder.BuildForUpdate(viewModel)", codeBehind);
        Assert.Contains("ContragentEditPayloadBuilder.BuildForLegalEntityChange(viewModel)", codeBehind);
        Assert.Contains("await EnsureContragentAddressAsync(viewModel);", codeBehind);
        Assert.Contains("isLegalEntityChangeMode: true", codeBehind);
        Assert.Contains("private async Task ChangeContragentLegalEntityManuallyAsync()", codeBehind);
        Assert.Contains("private async Task ChangeContragentLegalEntityFromFnsAsync()", codeBehind);
        Assert.Contains("private async Task<ContragentEditDialogState?> CreateLegalEntityChangeStateAsync(", codeBehind);
        Assert.Contains("CreateNewLegalEntityRegistration(result)", codeBehind);
        Assert.Contains("CopyRegistrationForLegalEntityChange(registration)", codeBehind);
        Assert.Contains("private async Task ImportContragentFromFnsAsync()", codeBehind);
        Assert.Contains("private async Task<FnsImportCriteria?> ShowFnsImportCriteriaDialogAsync()", codeBehind);
        Assert.Contains("private async Task<FnsContragentLookupResult?> ShowFnsImportResultSelectionDialogAsync(", codeBehind);
        Assert.Contains("private async Task<ContragentEditDialogState> CreateContragentImportStateAsync(", codeBehind);
        Assert.Contains("_fnsContragentService.SearchByReqAsync(criteria.Inn)", codeBehind);
        Assert.Contains("FilterFnsImportResults(results, criteria)", codeBehind);
        Assert.Contains("MatchesFnsImportKpp(result, criteria.Kpp)", codeBehind);
        Assert.Contains("MatchesFnsImportName(result, criteria.Name)", codeBehind);
        Assert.Contains("Header = \"КПП\"", codeBehind);
        Assert.Contains("Header = \"Наименование\"", codeBehind);
        Assert.Contains("fullName.Contains(name, StringComparison.CurrentCultureIgnoreCase)", codeBehind);
        Assert.Contains("shortName.Contains(name, StringComparison.CurrentCultureIgnoreCase)", codeBehind);
        Assert.Contains("await ShowContragentEditDialogAsync(isCreateMode: true, state);", codeBehind);
        Assert.Contains("private sealed record FnsImportCriteria", codeBehind);
        Assert.Contains("private sealed record FnsImportSelectionItem", codeBehind);
        Assert.Contains("BuildFnsImportSelectionLabel(result)", codeBehind);
        Assert.Contains("NormalizeSingleLine(result.Organization.FullName)", codeBehind);
        Assert.Contains("private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadAddressOptionsAsync(", codeBehind);
        Assert.Contains("private async Task<TableDataRow?> LoadContragentEditRowAsync", codeBehind);
        Assert.Contains("Model = \"Contragent\"", codeBehind);
        Assert.Contains("Preset = \"edit\"", codeBehind);
        Assert.Contains("LoadSimpleReferenceOptionsAsync(\"Ownership\", \"card\")", codeBehind);
        Assert.Contains("LoadSimpleReferenceOptionsAsync(\"Area\", \"item\")", codeBehind);
        Assert.Contains("RefreshContragentDetailContractsAsync()", codeBehind);
        Assert.Contains("ContragentDetailView.ContractsRow = row;", codeBehind);
        Assert.Contains("LoadContragentEditRowAsync(id.Value, cancellationTokenSource.Token)", codeBehind);
        Assert.Contains("BuildOwnershipOptionLabel(item)", codeBehind);
        Assert.Contains("return $\"{item.Name} - {item.FullName}\";", codeBehind);
        Assert.Contains("_referenceLookupCacheService.GetOptionsAsync(model, preset, cancellationToken)", codeBehind);
        Assert.Contains("private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadContragentOptionsAsync(", codeBehind);
        Assert.Contains("Model = \"Position\"", codeBehind);
        Assert.Contains("Preset = \"item\"", codeBehind);
        Assert.Contains("[\"name__cnt\"] = normalizedSearchText", codeBehind);
        Assert.Contains(".OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)", codeBehind);
    }

    [Fact]
    public void ContentHostDialogCoordinator_OwnsCommonShellDialogs()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);
        var hostBase = File.ReadAllText(ContentHostViewBasePath);
        var coordinator = File.ReadAllText(ContentHostDialogCoordinatorPath);

        Assert.Contains("public sealed partial class ContentHostView : ContentHostViewBase", codeBehind);
        Assert.Contains("public abstract class ContentHostViewBase : UserControl", hostBase);
        Assert.Contains("private readonly ContentHostDialogCoordinator _dialogCoordinator;", hostBase);
        Assert.Contains("new ContentHostDialogCoordinator(() => XamlRoot)", hostBase);
        Assert.Contains("protected async Task ShowErrorDialogAsync", hostBase);
        Assert.Contains("protected async Task ShowInfoDialogAsync", hostBase);
        Assert.Contains("protected async Task<bool> ConfirmDialogAsync", hostBase);
        Assert.Contains("protected static void ShowSuccessNotification", hostBase);
        Assert.Contains("protected static long? TryGetSelectedRowId", hostBase);
        Assert.Contains("internal sealed class ContentHostDialogCoordinator", coordinator);
        Assert.Contains("public async Task ShowErrorAsync", coordinator);
        Assert.Contains("public async Task ShowInfoAsync", coordinator);
        Assert.Contains("public async Task<bool> ConfirmAsync", coordinator);
        Assert.Contains("DialogChrome.Apply(dialog);", coordinator);
    }

    [Fact]
    public void ContentHostView_OpensEditOnRowDoubleClick_ExceptInternRole()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("private async void ReferenceTableView_RowDoubleTapped(object sender, CbsTableRowDoubleTappedEventArgs e)", codeBehind);
        Assert.Contains("private void ReferenceTableView_RowSelectionChanged(object sender, CbsTableRowSelectionChangedEventArgs e)", codeBehind);
        Assert.Contains("_viewModel.SelectedRow = null;", codeBehind);
        Assert.Contains("_viewModel.SelectedRow = e.Row;", codeBehind);
        Assert.Contains("if (IsInternEditBlocked())", codeBehind);
        Assert.Contains("await ShowReferenceEditDialogAsync(isCreateMode: false);", codeBehind);
        Assert.Contains("private bool IsInternEditBlocked()", codeBehind);
        Assert.Contains("string.Equals(role, \"intern\", System.StringComparison.OrdinalIgnoreCase)", codeBehind);
    }

    [Fact]
    public void ContentHostView_OpensCommercialStageEditDialog_ForStagesTable()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("private const int CommersDepartmentId = 2;", codeBehind);
        Assert.Contains("private bool IsStagesTableActive()", codeBehind);
        Assert.Contains("if (!isCreateMode && IsStagesTableActive())", codeBehind);
        Assert.Contains("await ShowStageEditDialogAsync();", codeBehind);
        Assert.Contains("_userService.CurrentUser?.DepartmentId == CommersDepartmentId", codeBehind);
        Assert.Contains("await ShowStageCommerEditDialogAsync();", codeBehind);
        Assert.Contains("new StageCommerEditDialog(", codeBehind);
        Assert.Contains("_contractWorkflowStore.SelectedStageEditState ?? StageEditState.FromRow(sourceRow),", codeBehind);
        Assert.Contains("_contractWorkflowStore.SelectedContractEditState,", codeBehind);
        Assert.Contains("dialog.SaveRequestedAsync += async args =>", codeBehind);
        Assert.Contains("if (!dialog.WasSaved || savedRow is null)", codeBehind);
        Assert.Contains("LoadStageEditRowAsync", codeBehind);
        Assert.Contains("private string GetCurrentTableModel()", codeBehind);
        Assert.Contains("private const string ContractModel = \"Contract\";", codeBehind);
        Assert.Contains("_modelMutationService.UpdateAsync(", codeBehind);
        Assert.Contains("SaveStagePayloadAsync", codeBehind);
        Assert.Contains("ContractModel,", codeBehind);
        Assert.Contains("dialog.ShouldCloseContract()", codeBehind);
        Assert.Contains("dialog.BuildContractClosePayload()", codeBehind);
    }

    [Fact]
    public void ContentHostView_OpensFinancialStageEditDialog_ForFinanceDepartment()
    {
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("private const int FinDepartmentId = 3;", codeBehind);
        Assert.Contains("_userService.CurrentUser?.DepartmentId == FinDepartmentId", codeBehind);
        Assert.Contains("await ShowStageFinEditDialogAsync();", codeBehind);
        Assert.Contains("new StageFinEditDialog(", codeBehind);
        Assert.Contains("dialog.SaveRequestedAsync += async args =>", codeBehind);
        Assert.Contains("if (!dialog.WasSaved || savedRow is null)", codeBehind);
        Assert.Contains("dialog.HasContractExternalNumberChanges()", codeBehind);
        Assert.Contains("dialog.BuildContractExternalNumberPayload()", codeBehind);
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

    [Fact]
    public void ContentHostView_DefinesStageCopyActionButton()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("x:Name=\"CopyStageInfoButton\"", xaml);
        Assert.Contains("Click=\"CopyStageInfoButton_Click\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"Скопировать этап в буфер обмена\"", xaml);
        Assert.Contains("private void CopyStageInfoButton_Click(object sender, RoutedEventArgs e)", codeBehind);
        Assert.Contains("StageClipboardFormatter.BuildClipboardText(_viewModel.SelectedRow)", codeBehind);
        Assert.Contains("var isStagesTable = IsStagesTableActive();", codeBehind);
        Assert.Contains("var canCopyStageInfo = hasSelectedRow && isStagesTable;", codeBehind);
        Assert.Contains("CopyStageInfoButton.Visibility = isStagesTable ? Visibility.Visible : Visibility.Collapsed;", codeBehind);
        Assert.Contains("CopyStageInfoButton.IsEnabled = canCopyStageInfo;", codeBehind);
    }

    [Fact]
    public void ContentHostView_DefinesStageCommentActionButton()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("x:Name=\"CommentStageButton\"", xaml);
        Assert.Contains("Click=\"CommentStageButton_Click\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"Комментировать этап\"", xaml);
        Assert.Contains("private void CommentStageButton_Click(object sender, RoutedEventArgs e)", codeBehind);
        Assert.Contains("PlaceholderText = \"Введите комментарий + Enter\"", codeBehind);
        Assert.Contains("args.Key != VirtualKey.Enter", codeBehind);
        Assert.Contains("private async Task SaveStageCommentAsync(string? comment, Flyout flyout)", codeBehind);
        Assert.Contains("StageEditPayloadBuilderHelpers.AppendCommentAttributes(payload, normalizedComment, profileId);", codeBehind);
        Assert.Contains("await SaveStagePayloadAsync(payload);", codeBehind);
        Assert.Contains("var canCommentStage = hasSelectedRow && isStagesTable && _userService.CurrentUser?.ProfileId is not null;", codeBehind);
        Assert.Contains("CommentStageButton.Visibility = isStagesTable ? Visibility.Visible : Visibility.Collapsed;", codeBehind);
        Assert.Contains("CommentStageButton.IsEnabled = canCommentStage;", codeBehind);
    }

    [Fact]
    public void ContentHostView_DefinesStageEmployeeCreateActionButton()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("x:Name=\"CreateStageEmployeeButton\"", xaml);
        Assert.Contains("Click=\"CreateStageEmployeeButton_Click\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"Новый сотрудник\"", xaml);
        Assert.Contains("private async void CreateStageEmployeeButton_Click(object sender, RoutedEventArgs e)", codeBehind);
        Assert.Contains("_referenceDefinitionService.TryGetByRoute(\"/employees\", out var employeeDefinition)", codeBehind);
        Assert.Contains("TryGetLongValue(_viewModel.SelectedRow, \"contract.contragent.id\")", codeBehind);
        Assert.Contains("TryGetText(_viewModel.SelectedRow, \"contract.contragent.name\")", codeBehind);
        Assert.Contains("initialState: new EmployeeEditDialogState", codeBehind);
        Assert.Contains("ContragentId = contragentId", codeBehind);
        Assert.Contains("ContragentName = contragentName", codeBehind);
        Assert.Contains("EmployeeEditDialogState? initialState = null", codeBehind);
        Assert.Contains("var state = initialState ?? EmployeeEditStateFactory.Create(definition, isCreateMode, sourceRow);", codeBehind);
        Assert.Contains("CreateStageEmployeeButton.Visibility = isStagesTable ? Visibility.Visible : Visibility.Collapsed;", codeBehind);
        Assert.Contains("CreateStageEmployeeButton.IsEnabled = canCreateStageEmployee;", codeBehind);
    }

    [Fact]
    public void ContentHostView_DefinesStageCostFractionToggleButton()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("x:Name=\"ShowStageCostFractionButton\"", xaml);
        Assert.Contains("Click=\"ShowStageCostFractionButton_Click\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"Показывать дробную часть суммы\"", xaml);
        Assert.Contains("_showStageCostFraction = _localUserSettingsService.Get().ShowStageCostFraction;", codeBehind);
        Assert.Contains("ReferenceTableView.ShowStageCostFraction = _showStageCostFraction;", codeBehind);
        Assert.Contains("settings.ShowStageCostFraction = showStageCostFraction;", codeBehind);
        Assert.Contains("ShowStageCostFractionButton.Visibility = isStagesTable ? Visibility.Visible : Visibility.Collapsed;", codeBehind);
    }

    [Fact]
    public void ContentHostView_DefinesFnsCompareActionButton()
    {
        var xaml = File.ReadAllText(ContentHostViewXamlPath);
        var codeBehind = File.ReadAllText(ContentHostViewCodeBehindPath);

        Assert.Contains("x:Name=\"FnsCompareButton\"", xaml);
        Assert.Contains("Click=\"FnsCompareButton_Click\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"Сверить данные с ФНС\"", xaml);
        Assert.Contains("x:Name=\"CopyContragentDetailsButton\"", xaml);
        Assert.Contains("Click=\"CopyContragentDetailsButton_Click\"", xaml);
        Assert.Contains("ToolTipService.ToolTip=\"Скопировать данные контрагента\"", xaml);
        Assert.Contains("private async void FnsCompareButton_Click(object sender, RoutedEventArgs e)", codeBehind);
        Assert.Contains("await CompareSelectedContragentWithFnsAsync();", codeBehind);
        Assert.Contains("private void CopyContragentDetailsButton_Click(object sender, RoutedEventArgs e)", codeBehind);
        Assert.Contains("ContragentDetailView.BuildClipboardText()", codeBehind);
        Assert.Contains("Clipboard.SetContent(dataPackage);", codeBehind);
        Assert.Contains("var isContractDetailTable = IsContractDetailTableActive();", codeBehind);
        Assert.Contains("var hasWorkflowContract = _contractWorkflowStore.Contract is { IsPlaceholder: false };", codeBehind);
        Assert.Contains("var showContractCopyDetails = isContractDetailTable && !isStagesTable;", codeBehind);
        Assert.Contains("var canCopyRevisionContract = showContractCopyDetails && hasWorkflowContract;", codeBehind);
        Assert.Contains("CopyContragentDetailsButton.Visibility = isContragentReference || showContractCopyDetails ? Visibility.Visible : Visibility.Collapsed;", codeBehind);
        Assert.Contains("IsContractDetailTableActive", codeBehind);
        Assert.Contains("? \"Скопировать данные контракта\"", codeBehind);
        Assert.Contains("_fnsContragentService = App.Services.GetRequiredService<IFnsContragentService>();", codeBehind);
        Assert.Contains("_fnsContragentService.SearchByReqAsync(state.Inn.Trim(), state.Kpp)", codeBehind);
        Assert.Contains("BuildFnsCompareRows(editViewModel, remote)", codeBehind);
        Assert.Contains("ContragentEditPayloadBuilder.BuildForUpdate(editViewModel)", codeBehind);
    }
}
