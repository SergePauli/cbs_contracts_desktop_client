using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Settings;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Settings;
using CbsContractsDesktopClient.Services.Shell;
using CbsContractsDesktopClient.ViewModels.Reports;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.Functional;
using CbsContractsDesktopClient.Views.Reports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace CbsContractsDesktopClient.Views.Shell;

public sealed class ActivityReportHostView : ContentHostViewBase
{
    private readonly ActivityReportStore _store;
    private readonly ActivityReportLoader _loader;
    private readonly ContractWorkflowFactory _workflowFactory;
    private readonly ContractWorkflowStore _workflowStore;
    private readonly IDataQueryService _dataQueryService;
    private readonly ILocalUserSettingsService _localUserSettingsService;
    private readonly LocalUserSettings _localUserSettings;
    private readonly ContractDetailView _detailView = new();
    private readonly TreeView _tree = new();
    private readonly ProgressRing _progress = new() { Width = 16, Height = 16 };
    private readonly InfoBar _message = new() { Severity = InfoBarSeverity.Error, IsClosable = true };
    private readonly CalendarDatePicker _startDate = CreateDatePicker();
    private readonly CalendarDatePicker _endDate = CreateDatePicker();
    private readonly Dictionary<TableDataRow, ActivityReportRow> _reportRows = [];
    private readonly List<CbsTableView> _sectionTables = [];
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _detailCts;
    private bool _initialLoadStarted;

    public ActivityReportHostView()
    {
        _store = App.Services.GetRequiredService<ActivityReportStore>();
        _loader = App.Services.GetRequiredService<ActivityReportLoader>();
        _workflowFactory = App.Services.GetRequiredService<ContractWorkflowFactory>();
        _workflowStore = App.Services.GetRequiredService<ContractWorkflowStore>();
        _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
        _localUserSettingsService = App.Services.GetRequiredService<ILocalUserSettingsService>();
        _localUserSettings = _localUserSettingsService.Get();

        _startDate.Date = _store.StartDate;
        _endDate.Date = _store.EndDate;
        _tree.HorizontalAlignment = HorizontalAlignment.Stretch;
        _tree.VerticalAlignment = VerticalAlignment.Stretch;
        _tree.ItemTemplate = BuildTreeTemplate();
        _progress.Visibility = Visibility.Collapsed;
        _detailView.Visibility = Visibility.Collapsed;
        Content = BuildContent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string? Route { get; set; }

    private UIElement BuildContent()
    {
        var root = new Grid { RowSpacing = 4 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Padding = new Thickness(8, 4, 8, 4), ColumnSpacing = 6 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        AddHeaderChild(header, new TextBlock { Text = "Период отчета", VerticalAlignment = VerticalAlignment.Center }, 0);
        AddHeaderChild(header, _startDate, 1);
        AddHeaderChild(header, new TextBlock { Text = "—", VerticalAlignment = VerticalAlignment.Center }, 2);
        AddHeaderChild(header, _endDate, 3);
        var loadButton = new Button { Content = "Сформировать", Height = 28, Padding = new Thickness(10, 2, 10, 2) };
        loadButton.Click += async (_, _) => await LoadAsync();
        AddHeaderChild(header, loadButton, 4);
        AddHeaderChild(header, _progress, 5);
        root.Children.Add(header);

        Grid.SetRow(_message, 1);
        root.Children.Add(_message);
        Grid.SetRow(_tree, 2);
        root.Children.Add(_tree);
        Grid.SetRow(_detailView, 3);
        root.Children.Add(_detailView);
        return root;
    }

    private async Task LoadAsync()
    {
        if (_startDate.Date is not DateTimeOffset start || _endDate.Date is not DateTimeOffset end)
        {
            _message.Message = "Укажите начало и окончание периода отчета.";
            _message.IsOpen = true;
            return;
        }

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _store.StartDate = start;
        _store.EndDate = end;
        _progress.IsActive = true;
        _progress.Visibility = Visibility.Visible;
        _message.IsOpen = false;
        await _store.LoadAsync(_loader, _loadCts.Token);
        if (_loadCts.IsCancellationRequested)
        {
            return;
        }

        _progress.IsActive = false;
        _progress.Visibility = Visibility.Collapsed;
        if (!string.IsNullOrWhiteSpace(_store.ErrorMessage))
        {
            _message.Message = _store.ErrorMessage;
            _message.IsOpen = true;
            return;
        }

        RenderSections();
    }

    private void RenderSections()
    {
        _reportRows.Clear();
        _sectionTables.Clear();
        var items = new List<ActivityReportTreeItem>();
        foreach (var section in _store.Sections)
        {
            foreach (var row in section.Rows)
            {
                _reportRows[row.DisplayRow] = row;
            }

            var table = BuildTable(section);
            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            header.Children.Add(new TextBlock { Text = section.Title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            header.Children.Add(new TextBlock { Text = section.Rows.Count.ToString(), Opacity = 0.65 });
            var expansionKey = section.Kind.ToString();
            var isExpanded = _localUserSettings.ActivityReportSectionExpansion.TryGetValue(expansionKey, out var savedExpansion)
                ? savedExpansion
                : section.Rows.Count > 0;
            var item = new ActivityReportTreeItem(
                header,
                [new ActivityReportTreeItem(table)],
                isExpanded);
            item.ExpansionChanged += async (_, _) => await SaveSectionExpansionAsync(expansionKey, item.IsExpanded);
            items.Add(item);
        }
        _tree.ItemsSource = items;
    }

    private async Task SaveSectionExpansionAsync(string key, bool isExpanded)
    {
        _localUserSettings.ActivityReportSectionExpansion[key] = isExpanded;
        await _localUserSettingsService.SaveAsync(_localUserSettings);
    }

    private CbsTableView BuildTable(ActivityReportSection section)
    {
        var table = new CbsTableView
        {
            Height = Math.Clamp(48 + section.Rows.Count * 22, 70, 280),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Density = CbsTableDensity.Compact,
            Columns = BuildColumns(section.Kind),
            ItemsSource = section.Rows.Select(row => row.DisplayRow).ToList(),
            LoadedCount = section.Rows.Count,
            TotalCount = section.Rows.Count,
            SupportsRowSelection = true,
            SupportsMultipleRowSelection = false,
            SupportsCellSelection = true,
            CanSelectRow = row => _reportRows[row].TargetRow is not null
        };
        table.RowSelectionChanged += Table_RowSelectionChanged;
        table.RowDoubleTapped += Table_RowDoubleTapped;
        _sectionTables.Add(table);
        return table;
    }

    private async void Table_RowSelectionChanged(object? sender, CbsTableRowSelectionChangedEventArgs e)
    {
        if (e.IsSelected && e.Row is not null)
        {
            ClearOtherTableSelections((CbsTableView)sender!);
            await SelectReportRowAsync(_reportRows[e.Row]);
        }
    }

    private async void Table_RowDoubleTapped(object? sender, CbsTableRowDoubleTappedEventArgs e)
    {
        var reportRow = _reportRows[e.Row];
        if (await SelectReportRowAsync(reportRow))
        {
            await ShowContractInfoDialogAsync();
        }
    }

    private async Task<bool> SelectReportRowAsync(ActivityReportRow reportRow)
    {
        _detailCts?.Cancel();
        if (reportRow.TargetRow is null)
        {
            _workflowStore.ClearRowDetailSelection();
            _detailView.Visibility = Visibility.Collapsed;
            return false;
        }

        var cancellationTokenSource = new CancellationTokenSource();
        _detailCts = cancellationTokenSource;
        try
        {
            var context = reportRow.TargetKind == ActivityReportTargetKind.Contract
                ? await _workflowFactory.CreateFromContractRowAsync(reportRow.TargetRow, cancellationTokenSource.Token)
                : await _workflowFactory.CreateFromStageRowAsync(reportRow.TargetRow, cancellationTokenSource.Token);
            if (cancellationTokenSource.IsCancellationRequested)
            {
                return false;
            }

            ContractRowDetailStrategy strategy = reportRow.TargetKind == ActivityReportTargetKind.Contract
                ? new ContractTableRowDetailStrategy()
                : new StageRowDetailStrategy();
            context.ApplyTo(_workflowStore, strategy);
            _detailView.Visibility = Visibility.Visible;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Не удалось загрузить сведения о контракте", ex.Message);
            return false;
        }
    }

    private async Task ShowContractInfoDialogAsync()
    {
        try
        {
            var contract = _workflowStore.SelectedContractEditState
                ?? throw new InvalidOperationException("Контракт не выбран.");
            var stage = _workflowStore.SelectedStageEditState
                ?? throw new InvalidOperationException("В контракте отсутствует этап для просмотра.");
            var audit = await ContractInfoAuditLoader.LoadAsync(
                _dataQueryService,
                contract.Id,
                contract.Status.Id == WorkflowStatusIds.Closed);
            var dialog = new ContractInfoDialog(
                stage,
                contract,
                _workflowStore.GetContractDocumentRevisionEditState(),
                audit)
            {
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Не удалось открыть информацию о контракте", ex.Message);
        }
    }

    private static IReadOnlyList<CbsTableColumnDefinition> BuildColumns(ActivityReportSectionKind kind)
    {
        var columns = new List<CbsTableColumnDefinition>
        {
            Column("num", "Номер", "7rem"),
            Column("is_gov", "Гос", "3rem", CbsTableColumnAlignment.Center, CbsTableBodyMode.BooleanIcon),
            DateColumn("start", "Дата", "7rem"),
            Column("contragent", "Контрагент", "18rem"),
            Column("cost", "Сумма", "8rem", CbsTableColumnAlignment.Right, bodyTemplateKey: "ActivityReportDeletedAmount")
        };
        if (kind is ActivityReportSectionKind.StatusChanges or ActivityReportSectionKind.DeadlineChanges)
        {
            columns.Add(Column("old_value", "Предыдущий", "8rem"));
            columns.Add(Column("new_value", "Новый", "8rem"));
        }
        else if (kind == ActivityReportSectionKind.AddedContracts)
        {
            columns.Add(Column("status", "Статус", "8rem"));
        }
        else if (kind == ActivityReportSectionKind.PendingStages)
        {
            columns.Add(Column("new_value", "Статус", "8rem"));
        }
        else if (kind is ActivityReportSectionKind.Funding or ActivityReportSectionKind.Payments)
        {
            columns.Add(Column("operation", "Операция", "12rem"));
        }
        columns.Add(Column(
            "when",
            GetEventTimeHeader(kind),
            "9rem"));
        columns.Add(Column("who", "Ответственный", "10rem"));
        if (kind == ActivityReportSectionKind.Comments)
        {
            columns.Add(Column("new_value", "Содержание", "18rem"));
        }
        return columns;
    }

    private static string GetEventTimeHeader(ActivityReportSectionKind kind) => kind switch
    {
        ActivityReportSectionKind.StatusChanges => "Изменён",
        ActivityReportSectionKind.PendingStages => "Передан",
        ActivityReportSectionKind.AddedContracts => "Добавлен",
        ActivityReportSectionKind.DeadlineChanges => "Изменён",
        ActivityReportSectionKind.Comments => "Добавлен",
        ActivityReportSectionKind.Funding => "Когда",
        ActivityReportSectionKind.Payments => "Внесена",
        _ => throw new InvalidOperationException($"Неизвестный раздел отчета: {kind}.")
    };

    private static CbsTableColumnDefinition DateColumn(string field, string header, string width) => new()
    {
        FieldKey = field,
        Header = header,
        DefaultWidth = width,
        IsSortable = false,
        IsFilterable = false,
        Filter = new CbsTableColumnFilterDefinition
        {
            IsEnabled = false,
            Mode = DataFilterMode.Date
        }
    };

    private static CbsTableColumnDefinition Column(
        string field,
        string header,
        string width,
        CbsTableColumnAlignment alignment = CbsTableColumnAlignment.Left,
        CbsTableBodyMode bodyMode = CbsTableBodyMode.Text,
        string? bodyTemplateKey = null) => new()
    {
        FieldKey = field,
        Header = header,
        DefaultWidth = width,
        Alignment = alignment,
        BodyMode = bodyMode,
        BodyTemplateKey = bodyTemplateKey,
        IsSortable = false,
        IsFilterable = false
    };

    private static DataTemplate BuildTreeTemplate()
    {
        const string xaml = """
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <TreeViewItem IsExpanded="{Binding IsExpanded, Mode=TwoWay}" ItemsSource="{Binding Children}">
                    <TreeViewItem.Content>
                        <ContentControl HorizontalContentAlignment="Stretch" Content="{Binding Content}" />
                    </TreeViewItem.Content>
                </TreeViewItem>
            </DataTemplate>
            """;
        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml);
    }

    private static CalendarDatePicker CreateDatePicker() => new()
    {
        DateFormat = "{day.integer(2)}.{month.integer(2)}.{year.full}",
        Height = 28,
        Padding = new Thickness(6, 0, 6, 0)
    };

    private static void AddHeaderChild(Grid grid, FrameworkElement child, int column)
    {
        Grid.SetColumn(child, column);
        grid.Children.Add(child);
    }

    private void ClearOtherTableSelections(CbsTableView selectedTable)
    {
        foreach (var table in _sectionTables)
        {
            if (!ReferenceEquals(table, selectedTable))
            {
                table.ClearSelection();
            }
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialLoadStarted || _store.Sections.Count > 0)
        {
            return;
        }

        _initialLoadStarted = true;
        try
        {
            await LoadAsync();
        }
        finally
        {
            _initialLoadStarted = false;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loadCts?.Cancel();
        _detailCts?.Cancel();
        _workflowStore.ClearRowDetailSelection();
    }
}
