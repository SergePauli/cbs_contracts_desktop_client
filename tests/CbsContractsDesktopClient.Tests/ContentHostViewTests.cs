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

    private static readonly string ContentHostViewBasePath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ContentHostViewBase.cs");

    private static readonly string ComplexHostViewBasePath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ComplexHostViewBase.cs");

    private static readonly string FilterIconFactoryPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Controls",
        "FilterIconFactory.cs");

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

    private static readonly string StageHostViewPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "StageHostView.cs");

    private static readonly string ContractHostViewPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ContractHostView.cs");

    private static readonly string RevisionHostViewPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "RevisionHostView.cs");

    private static readonly string EmployeeHostViewPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "EmployeeHostView.cs");

    private static readonly string ContragentHostViewPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "ContragentHostView.cs");

    private static readonly string TablePageStorePath = Path.Combine(
        ProjectRoot,
        "src",
        "Stores",
        "Table",
        "TablePageStore.cs");

    private static readonly string AppShellPageXamlPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "AppShellPage.xaml");

    [Fact]
    public void LegacyContentHostView_IsRemoved()
    {
        Assert.False(File.Exists(ContentHostViewXamlPath));
        Assert.False(File.Exists(ContentHostViewCodeBehindPath));
    }

    [Fact]
    public void AppShellPage_UsesContentHostRouterView()
    {
        var xaml = File.ReadAllText(AppShellPageXamlPath);

        Assert.Contains("<shell:ContentHostRouterView Grid.Row=\"1\"", xaml);
        Assert.DoesNotContain("<shell:ContentHostView Grid.Row=\"1\"", xaml);
    }

    [Fact]
    public void ContentHostRouterView_MapsRoutesToConcreteHostsOnly()
    {
        var xaml = File.ReadAllText(ContentHostRouterViewXamlPath);
        var codeBehind = File.ReadAllText(ContentHostRouterViewCodeBehindPath);

        Assert.Contains("x:Class=\"CbsContractsDesktopClient.Views.Shell.ContentHostRouterView\"", xaml);
        Assert.Contains("<ContentControl", xaml);
        Assert.Contains("x:Name=\"HostContentControl\"", xaml);
        Assert.Contains("HorizontalContentAlignment=\"Stretch\"", xaml);
        Assert.Contains("VerticalContentAlignment=\"Stretch\"", xaml);
        Assert.Contains("ContentHostRouteKind.Reference => GetReferenceHostView(route)", codeBehind);
        Assert.Contains("ContentHostRouteKind.Holiday => GetHolidayHostView(route)", codeBehind);
        Assert.Contains("ContentHostRouteKind.Profile => GetProfileHostView(route)", codeBehind);
        Assert.Contains("ContentHostRouteKind.Employee => GetEmployeeHostView(route)", codeBehind);
        Assert.Contains("ContentHostRouteKind.Contragent => GetContragentHostView(route)", codeBehind);
        Assert.Contains("ContentHostRouteKind.Revision => GetRevisionHostView(route)", codeBehind);
        Assert.Contains("ContentHostRouteKind.Stage => GetStageHostView(route)", codeBehind);
        Assert.DoesNotContain("ContentHostRouteKind.Table", codeBehind);
        Assert.DoesNotContain("GetTableHostView", codeBehind);
        Assert.DoesNotContain("new ContentHostView()", codeBehind);
    }

    [Fact]
    public void ContentHostBase_OwnsCommonDialogPrimitives()
    {
        var hostBase = File.ReadAllText(ContentHostViewBasePath);

        Assert.Contains("public abstract class ContentHostViewBase : UserControl", hostBase);
        Assert.Contains("private readonly ContentHostDialogCoordinator _dialogCoordinator;", hostBase);
        Assert.Contains("protected async Task ShowErrorDialogAsync", hostBase);
        Assert.Contains("protected async Task ShowInfoDialogAsync", hostBase);
        Assert.Contains("protected async Task<bool> ConfirmDialogAsync", hostBase);
        Assert.Contains("protected static void ShowSuccessNotification", hostBase);
        Assert.Contains("protected static long? TryGetSelectedRowId", hostBase);
    }

    [Fact]
    public void ComplexHostViewBase_OwnsCommonTableActions()
    {
        var codeBehind = File.ReadAllText(ComplexHostViewBasePath);

        Assert.Contains("public abstract class ComplexHostViewBase : ContentHostViewBase", codeBehind);
        Assert.Contains("protected TableHostView TableView { get; private set; }", codeBehind);
        Assert.Contains("private TableHostView CreateTableHostView()", codeBehind);
        Assert.Contains("private void RecreateTableHostView()", codeBehind);
        Assert.Contains("TableView = CreateTableHostView();", codeBehind);
        Assert.Contains("_tableHost.Children.Add(TableView);", codeBehind);
        Assert.Contains("CreateResetFiltersButton()", codeBehind);
        Assert.Contains("CreateSettingsButton()", codeBehind);
        Assert.Contains("ConfigureColumnsAsync()", codeBehind);
        Assert.Contains("new TableColumnLayoutDialog(Store.CurrentTablePage.Columns)", codeBehind);
        Assert.Contains("await Store.SaveColumnLayoutAsync(dialog.BuildColumns());", codeBehind);
        Assert.Contains("Store.CanConfigureColumns", codeBehind);
        Assert.Contains("await Store.ResetColumnWidthsAsync();", codeBehind);
        Assert.Contains("await Store.ClearFiltersAsync();", codeBehind);
        Assert.Contains("await Store.ClearSortsAsync();", codeBehind);
    }

    [Fact]
    public void ComplexHostViewBase_OwnsHeaderActionButtonVisualStates()
    {
        var codeBehind = File.ReadAllText(ComplexHostViewBasePath);

        Assert.Contains("ApplyEditButtonState", codeBehind);
        Assert.Contains("Microsoft.UI.Colors.RoyalBlue", codeBehind);
        Assert.Contains("ApplyCreateButtonState", codeBehind);
        Assert.Contains("Microsoft.UI.Colors.ForestGreen", codeBehind);
        Assert.Contains("ApplyDeleteButtonState", codeBehind);
        Assert.Contains("Microsoft.UI.Colors.Firebrick", codeBehind);
        Assert.Contains("ApplyDefaultActionButtonState", codeBehind);
        Assert.Contains("GetBrush(\"ShellPrimaryTextBrush\")", codeBehind);
        Assert.Contains("GetBrush(\"ShellSecondaryTextBrush\")", codeBehind);
        Assert.Contains("button.IsEnabled = isEnabled;", codeBehind);
        Assert.Contains("button.Foreground = isEnabled", codeBehind);
    }

    [Fact]
    public void ComplexAndReferenceHostsUseSharedFilterClearIcon()
    {
        var complexHost = File.ReadAllText(ComplexHostViewBasePath);
        var referenceXaml = File.ReadAllText(ReferenceHostViewXamlPath);
        var referenceCodeBehind = File.ReadAllText(ReferenceHostViewCodeBehindPath);
        var iconFactory = File.ReadAllText(FilterIconFactoryPath);

        Assert.Contains("internal static class FilterIconFactory", iconFactory);
        Assert.Contains("BuildFilterClearIcon()", iconFactory);
        Assert.Contains("Glyph = \"\\uE71C\"", iconFactory);
        Assert.Contains("Glyph = \"\\uE733\"", iconFactory);
        Assert.Contains("button.Content = FilterIconFactory.BuildFilterClearIcon();", complexHost);
        Assert.Contains("x:Name=\"ResetFiltersButton\"", referenceXaml);
        Assert.Contains("ResetFiltersButton.Content = FilterIconFactory.BuildFilterClearIcon();", referenceCodeBehind);
    }

    [Fact]
    public void ComplexHosts_UseSharedHeaderActionButtonVisualStates()
    {
        var contractHost = File.ReadAllText(ContractHostViewPath);
        var stageHost = File.ReadAllText(StageHostViewPath);
        var revisionHost = File.ReadAllText(RevisionHostViewPath);
        var employeeHost = File.ReadAllText(EmployeeHostViewPath);
        var contragentHost = File.ReadAllText(ContragentHostViewPath);

        Assert.Contains("ApplyEditButtonState(_editButton", contractHost);
        Assert.Contains("ApplyCreateButtonState(_createEmployeeButton", contractHost);
        Assert.Contains("ApplyDefaultActionButtonState(_saveFiltersButton", contractHost);
        Assert.Contains("ApplyEditButtonState(_editButton", stageHost);
        Assert.Contains("ApplyCreateButtonState(_createEmployeeButton", stageHost);
        Assert.Contains("ApplyDefaultActionButtonState(_saveFiltersButton", stageHost);
        Assert.Contains("ApplyEditButtonState(_editButton", revisionHost);
        Assert.Contains("ApplyDefaultActionButtonState(", revisionHost);
        Assert.Contains("ApplyEditButtonState(_editButton", employeeHost);
        Assert.Contains("ApplyDeleteButtonState(_deleteButton", employeeHost);
        Assert.Contains("ApplyCreateButtonState(_createButton", employeeHost);
        Assert.Contains("ApplyEditButtonState(_editButton", contragentHost);
        Assert.Contains("ApplyDeleteButtonState(_deleteButton", contragentHost);
        Assert.Contains("ApplyCreateButtonState(_createButton", contragentHost);
        Assert.Contains("ApplyDefaultActionButtonState(_fnsCompareButton", contragentHost);
        Assert.Contains("ApplyDefaultActionButtonState(_copyButton", contragentHost);
    }

    [Fact]
    public void SettingsButtons_UseNeutralActiveState()
    {
        var complexHost = File.ReadAllText(ComplexHostViewBasePath);
        var referenceCodeBehind = File.ReadAllText(ReferenceHostViewCodeBehindPath);

        Assert.Contains("private Button? _settingsButton;", complexHost);
        Assert.Contains("_settingsButton = CreateHeaderIconButton(\"\\uE713\", \"Настройки таблицы\")", complexHost);
        Assert.Contains("ApplyDefaultActionButtonState(_settingsButton, Store.HasActiveReference);", complexHost);
        Assert.Contains("UpdateSettingsButtonState();", referenceCodeBehind);
        Assert.Contains("HeaderSettingsButton.IsEnabled = _viewModel.HasActiveReference;", referenceCodeBehind);
        Assert.Contains("HeaderSettingsButton.Foreground = _viewModel.HasActiveReference", referenceCodeBehind);
    }

    [Fact]
    public void TableHostView_OwnsCbsTableViewZone()
    {
        var xaml = File.ReadAllText(TableHostViewXamlPath);
        var codeBehind = File.ReadAllText(TableHostViewCodeBehindPath);

        Assert.Contains("x:Class=\"CbsContractsDesktopClient.Views.Shell.TableHostView\"", xaml);
        Assert.Contains("<controls:CbsTableView", xaml);
        Assert.Contains("x:Name=\"TableView\"", xaml);
        Assert.Contains("public void AttachTableState", codeBehind);
        Assert.Contains("public void AttachTableRows", codeBehind);
        Assert.Contains("public void DetachTableState()", codeBehind);
        Assert.Contains("private ICbsTableRows<TableDataRow>? _rows;", codeBehind);
        Assert.Contains("private IReadOnlyList<CbsTableColumnDefinition> _columns = [];", codeBehind);
        Assert.Contains("TableView.ItemsSource = _items;", codeBehind);
        Assert.Contains("TableView.ShowStageCostFraction", codeBehind);
        Assert.Contains("public event EventHandler<CbsTableSortRequestedEventArgs>? SortRequested;", codeBehind);
        Assert.Contains("public event EventHandler<CbsTableRowSelectionChangedEventArgs>? RowSelectionChanged;", codeBehind);
    }

    [Fact]
    public void TableHostView_HandlesRowReplacementSeparatelyFromItemsRefresh()
    {
        var codeBehind = File.ReadAllText(TableHostViewCodeBehindPath);

        Assert.Contains("private ITableRowReplacementSource? _rowReplacementSource;", codeBehind);
        Assert.Contains("_rowReplacementSource.RowReplaced += OnRowReplaced;", codeBehind);
        Assert.Contains("_rowReplacementSource.RowReplaced -= OnRowReplaced;", codeBehind);
        Assert.Contains("private void OnRowReplaced", codeBehind);
        Assert.Contains("TableView.RefreshVisibleRow(e.Index, row);", codeBehind);
        Assert.Contains("TableView.RefreshVisibleRowsIfViewportHasPlaceholders();", codeBehind);
    }

    [Fact]
    public void StageHostView_OwnsStageSpecificOptionsSources()
    {
        var stageHost = File.ReadAllText(StageHostViewPath);
        var tablePageStore = File.ReadAllText(TablePageStorePath);

        Assert.Contains("OptionsRegistry.Set(\"StageStatus\"", stageHost);
        Assert.Contains("OptionsRegistry.Set(\"TaskKind\"", stageHost);
        Assert.Contains("LoadStageStatusOptionsAsync", stageHost);
        Assert.Contains("LoadStageTaskKindOptionsAsync", stageHost);
        Assert.Contains("FormatTaskKindOptionLabel", stageHost);
        Assert.Contains("OptionsRegistry.Get(\"StageStatus\")", stageHost);
        Assert.DoesNotContain("NormalizeStageStatusOptions", tablePageStore);
        Assert.DoesNotContain("LoadTaskKindOptionsAsync", tablePageStore);
        Assert.DoesNotContain("FormatTaskKindOptionLabel", tablePageStore);
    }

    [Fact]
    public void StageHostView_PreparesStageEditContextFromContractId()
    {
        var stageHost = File.ReadAllText(StageHostViewPath);

        Assert.Contains("private async Task<bool> PrepareStageEditContextAsync(TableDataRow sourceRow)", stageHost);
        Assert.Contains("private long? ResolveStageContractId(TableDataRow sourceRow)", stageHost);
        Assert.Contains("TryGetLongValue(sourceRow, \"contract.id\")", stageHost);
        Assert.Contains("TryGetLongValue(sourceRow, \"contract_id\")", stageHost);
        Assert.Contains("_contractWorkflowStore.BeginStageEdit(contract, sourceRow, contragent);", stageHost);
    }

    [Fact]
    public void ReferenceHostView_AcceptsRouteInput()
    {
        var xaml = File.ReadAllText(ReferenceHostViewXamlPath);
        var codeBehind = File.ReadAllText(ReferenceHostViewCodeBehindPath);

        Assert.Contains("x:Class=\"CbsContractsDesktopClient.Views.Shell.ReferenceHostView\"", xaml);
        Assert.Contains("<shell:ContentHostViewBase", xaml);
        Assert.Contains("<shell:TableHostView", xaml);
        Assert.Contains("public sealed partial class ReferenceHostView : ContentHostViewBase", codeBehind);
        Assert.Contains("public string? Route", codeBehind);
        Assert.Contains("_tablePageDefinitionService.TryGetByRoute(route, out var definition)", codeBehind);
        Assert.DoesNotContain("_tableHostView", codeBehind);
    }

    [Fact]
    public void ComplexRoutes_HaveDedicatedHostViews()
    {
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "src", "Views", "Shell", "HolidayHostView.cs")));
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "src", "Views", "Shell", "ProfileHostView.cs")));
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "src", "Views", "Shell", "EmployeeHostView.cs")));
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "src", "Views", "Shell", "ContragentHostView.cs")));
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "src", "Views", "Shell", "RevisionHostView.cs")));
        Assert.True(File.Exists(Path.Combine(ProjectRoot, "src", "Views", "Shell", "StageHostView.cs")));
    }
}
