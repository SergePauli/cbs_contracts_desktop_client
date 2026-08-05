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

    private static readonly string NavigationSidebarViewXamlPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "NavigationSidebarView.xaml");

    private static readonly string NavigationSidebarViewCodeBehindPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "Shell",
        "NavigationSidebarView.xaml.cs");

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
    public void ComplexHostViewBase_SplitsHeaderActionsAroundTitle()
    {
        var codeBehind = File.ReadAllText(ComplexHostViewBasePath);

        Assert.Contains("private readonly StackPanel _primaryHeaderActionsPanel;", codeBehind);
        Assert.Contains("private readonly StackPanel _secondaryHeaderActionsPanel;", codeBehind);
        Assert.Contains("private readonly StackPanel _headerActionsHost;", codeBehind);
        Assert.Contains("private readonly Border _headerActionSeparator;", codeBehind);
        Assert.Contains("protected virtual int PrimaryHeaderActionCount => 0;", codeBehind);
        Assert.Contains("_headerActionsHost.Children.Add(_primaryHeaderActionsPanel);", codeBehind);
        Assert.Contains("_headerActionsHost.Children.Add(_headerActionSeparator);", codeBehind);
        Assert.Contains("_headerActionsHost.Children.Add(_secondaryHeaderActionsPanel);", codeBehind);
        Assert.Contains("Grid.SetColumn(_headerActionsHost, 0);", codeBehind);
        Assert.Contains("Grid.SetColumn(_headerTitleTextBlock, 2);", codeBehind);
        Assert.Contains("_headerTitleTextBlock.HorizontalAlignment = HorizontalAlignment.Right;", codeBehind);
        Assert.Contains("Background = GetBrush(\"ShellTableGridLineBrush\")", codeBehind);
        Assert.Contains("foreach (var action in actions.Take(primaryActionCount))", codeBehind);
        Assert.Contains("foreach (var action in actions.Skip(primaryActionCount))", codeBehind);
        Assert.Contains("_secondaryHeaderActionsPanel.Children.Add(CreateResetFiltersButton());", codeBehind);
        Assert.Contains("_secondaryHeaderActionsPanel.Children.Add(CreateSettingsButton());", codeBehind);
        Assert.Contains("_headerActionSeparator.Visibility =", codeBehind);
    }

    [Fact]
    public void ReferenceHostView_SplitsHeaderActionsAroundDarkerSeparator()
    {
        var xaml = File.ReadAllText(ReferenceHostViewXamlPath);

        Assert.Contains("x:Name=\"CreateRowButton\"", xaml);
        Assert.Contains("x:Name=\"EditSelectedRowButton\"", xaml);
        Assert.Contains("x:Name=\"DeleteSelectedRowButton\"", xaml);
        Assert.Contains("Background=\"{StaticResource ShellTableGridLineBrush}\"", xaml);
        Assert.Contains("x:Name=\"ResetFiltersButton\"", xaml);
        Assert.Contains("x:Name=\"HeaderSettingsButton\"", xaml);
        Assert.Contains("Grid.Column=\"2\"", xaml);
        Assert.Contains("HorizontalAlignment=\"Right\"", xaml);
        Assert.True(xaml.IndexOf("x:Name=\"CreateRowButton\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"EditSelectedRowButton\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"DeleteSelectedRowButton\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"ResetFiltersButton\"", StringComparison.Ordinal));
        Assert.True(xaml.IndexOf("x:Name=\"ResetFiltersButton\"", StringComparison.Ordinal) < xaml.IndexOf("x:Name=\"HeaderSettingsButton\"", StringComparison.Ordinal));
    }

    [Fact]
    public void NavigationSidebar_UsesCompactSeparatedSectionHeaders()
    {
        var xaml = File.ReadAllText(NavigationSidebarViewXamlPath);
        var codeBehind = File.ReadAllText(NavigationSidebarViewCodeBehindPath);

        Assert.Contains("<x:Double x:Key=\"NavigationViewItemOnLeftMinHeight\">20</x:Double>", xaml);
        Assert.Contains("<Thickness x:Key=\"NavigationViewItemButtonMargin\">0</Thickness>", xaml);
        Assert.Contains("<Thickness x:Key=\"NavigationViewItemInnerHeaderMargin\">8,0</Thickness>", xaml);
        Assert.Contains("<Setter Property=\"MinHeight\" Value=\"{StaticResource ShellMenuItemMinHeight}\" />", xaml);
        Assert.Contains("<Setter Property=\"Padding\" Value=\"0\" />", xaml);
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"14\" />", xaml);
        Assert.Contains("<Setter Property=\"Margin\" Value=\"8,8,6,1\" />", xaml);
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"10\" />", xaml);
        Assert.Contains("<Setter Property=\"FontWeight\" Value=\"SemiBold\" />", xaml);
        Assert.Contains("<ControlTemplate TargetType=\"NavigationViewItemHeader\">", xaml);
        Assert.Contains("FontSize=\"{TemplateBinding FontSize}\"", xaml);
        Assert.Contains("private const double MenuItemLeftOffset = 10;", codeBehind);
        Assert.Contains("FontSize = 14,", codeBehind);
        Assert.Contains("Margin = new Thickness(MenuItemLeftOffset, 0, 0, 0)", codeBehind);
        Assert.Contains("SidebarNavigationView.MenuItems.Add(CreateSectionHeader(section.Title));", codeBehind);
        Assert.DoesNotContain("childItem.Margin = new Thickness(-14", codeBehind);
        Assert.Contains("Content = NormalizeSectionTitle(title)", codeBehind);
        Assert.Contains("return title.ToUpperInvariant();", codeBehind);
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
        Assert.Contains("_infoButton = CreateHeaderIconButton(\"\\uE946\", \"Информация о контракте\")", contractHost);
        Assert.Contains("ApplyDefaultActionButtonState(_infoButton, HasContractInfoSelection());", contractHost);
        Assert.Contains("_copyContractDataButton = CreateHeaderIconButton(\"\\uE8F3\", \"Скопировать данные выбранного контракта в буфер\")", contractHost);
        Assert.Contains("_copyCellSelectionButton = CreateHeaderIconButton(\"\\uE8C8\", \"Скопировать выделенный диапазон\")", contractHost);
        Assert.Contains("TableView.CopySelectedCellRangeToClipboard()", contractHost);
        Assert.Contains("ApplyDefaultActionButtonState(_copyContractDataButton, hasSelectedRow);", contractHost);
        Assert.Contains("ApplyDefaultActionButtonState(_copyCellSelectionButton, Store.HasActiveReference);", contractHost);
        Assert.Contains("ApplyCreateButtonState(_createEmployeeButton", contractHost);
        Assert.Contains("ApplyDefaultActionButtonState(_saveFiltersButton", contractHost);
        Assert.Contains("protected override int PrimaryHeaderActionCount => 3;", contractHost);
        Assert.True(contractHost.IndexOf("_createButton,", StringComparison.Ordinal) < contractHost.IndexOf("_editButton,", StringComparison.Ordinal));
        Assert.True(contractHost.IndexOf("_editButton,", StringComparison.Ordinal) < contractHost.IndexOf("_infoButton,", StringComparison.Ordinal));
        Assert.Contains("ApplyEditButtonState(_editButton", stageHost);
        Assert.Contains("_infoButton = CreateHeaderIconButton(\"\\uE946\", \"Информация о контракте\")", stageHost);
        Assert.Contains("ApplyDefaultActionButtonState(_infoButton, HasContractInfoSelection());", stageHost);
        Assert.Contains("_copyStageDataButton = CreateHeaderIconButton(\"\\uE8F3\", \"Скопировать данные выбранного этапа в буфер\")", stageHost);
        Assert.Contains("_copyCellSelectionButton = CreateHeaderIconButton(\"\\uE8C8\", \"Скопировать выделенный диапазон\")", stageHost);
        Assert.Contains("TableView.CopySelectedCellRangeToClipboard()", stageHost);
        Assert.Contains("ApplyDefaultActionButtonState(_copyStageDataButton, hasSelectedRow);", stageHost);
        Assert.Contains("ApplyDefaultActionButtonState(_copyCellSelectionButton, Store.HasActiveReference);", stageHost);
        Assert.Contains("ApplyCreateButtonState(_createEmployeeButton", stageHost);
        Assert.Contains("ApplyDefaultActionButtonState(_saveFiltersButton", stageHost);
        Assert.Contains("protected override int PrimaryHeaderActionCount => 2;", stageHost);
        Assert.Contains("ApplyEditButtonState(_editButton", revisionHost);
        Assert.Contains("_infoButton = CreateHeaderIconButton(\"\\uE946\", \"Информация о контракте\")", revisionHost);
        Assert.Contains("ApplyDefaultActionButtonState(_infoButton, HasContractInfoSelection());", revisionHost);
        Assert.Contains("_copyContractDataButton = CreateHeaderIconButton(\"\\uE8F3\", \"Скопировать данные выбранного контракта в буфер\")", revisionHost);
        Assert.Contains("_copyCellSelectionButton = CreateHeaderIconButton(\"\\uE8C8\", \"Скопировать выделенный диапазон\")", revisionHost);
        Assert.Contains("TableView.CopySelectedCellRangeToClipboard()", revisionHost);
        Assert.Contains("ApplyDefaultActionButtonState(_copyCellSelectionButton, Store.HasActiveReference);", revisionHost);
        Assert.Contains("ApplyDefaultActionButtonState(", revisionHost);
        Assert.Contains("protected override int PrimaryHeaderActionCount => 2;", revisionHost);
        Assert.Contains("ApplyEditButtonState(_editButton", employeeHost);
        Assert.Contains("ApplyDeleteButtonState(_deleteButton", employeeHost);
        Assert.Contains("ApplyCreateButtonState(_createButton", employeeHost);
        Assert.Contains("protected override int PrimaryHeaderActionCount => 3;", employeeHost);
        Assert.Contains("return [_createButton, _editButton, _deleteButton];", employeeHost);
        Assert.Contains("ApplyEditButtonState(_editButton", contragentHost);
        Assert.Contains("ApplyDeleteButtonState(_deleteButton", contragentHost);
        Assert.Contains("ApplyCreateButtonState(_createButton", contragentHost);
        Assert.Contains("ApplyDefaultActionButtonState(_fnsCompareButton", contragentHost);
        Assert.Contains("ApplyDefaultActionButtonState(_copyButton", contragentHost);
        Assert.Contains("protected override int PrimaryHeaderActionCount => 3;", contragentHost);
        Assert.Contains("return [_createButton, _editButton, _deleteButton,", contragentHost);
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
        Assert.Contains("TableView.ItemsSource = _rows?.Items ?? [];", codeBehind);
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
        Assert.Contains("StageStatusFilterOptionsProvider _stageStatusFilterOptionsProvider", stageHost);
        Assert.Contains("_stageStatusFilterOptionsProvider.LoadAsync()", stageHost);
        Assert.Contains("LoadStageTaskKindOptionsAsync", stageHost);
        Assert.Contains("FormatTaskKindOptionLabel", stageHost);
        Assert.Contains("OptionsRegistry.Get(\"StageStatus\")", stageHost);
        Assert.DoesNotContain("NormalizeStageStatusOptions", tablePageStore);
        Assert.DoesNotContain("\"StageStatus\"", tablePageStore);
        Assert.DoesNotContain("\"OrderStatus\"", tablePageStore);
        Assert.DoesNotContain("LoadTaskKindOptionsAsync", tablePageStore);
        Assert.DoesNotContain("FormatTaskKindOptionLabel", tablePageStore);
    }

    [Fact]
    public void StageHostView_PreparesStageEditContextThroughWorkflowFactory()
    {
        var stageHost = File.ReadAllText(StageHostViewPath);
        var workflowFactory = File.ReadAllText(Path.Combine(ProjectRoot, "src", "ViewModels", "Workflow", "ContractWorkflowFactory.cs"));

        Assert.Contains("private async Task<bool> PrepareStageEditContextAsync(TableDataRow sourceRow)", stageHost);
        Assert.Contains("_contractWorkflowFactory.CreateFromStageRowAsync(sourceRow)", stageHost);
        Assert.Contains("TryGetLong(row.GetValue(\"contract.id\"))", workflowFactory);
        Assert.Contains("TryGetLong(row.GetValue(\"contract_id\"))", workflowFactory);
        Assert.Contains("throw new InvalidOperationException(\"Contract workflow stage row must contain contract.id or contract_id.\")", workflowFactory);
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
