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
        Assert.Contains("await Store.ResetColumnWidthsAsync();", codeBehind);
        Assert.Contains("await Store.ClearFiltersAsync();", codeBehind);
        Assert.Contains("await Store.ClearSortsAsync();", codeBehind);
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
