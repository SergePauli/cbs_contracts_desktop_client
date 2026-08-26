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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Shell;

public sealed partial class ActivityReportHostView : ContentHostViewBase
{
    private readonly ActivityReportStore _store;
    private readonly ActivityReportLoader _loader;
    private readonly ContractWorkflowFactory _workflowFactory;
    private readonly ContractWorkflowStore _workflowStore;
    private readonly IDataQueryService _dataQueryService;
    private readonly ILocalUserSettingsService _localUserSettingsService;
    private readonly LocalUserSettings _localUserSettings;
    private readonly Dictionary<TableDataRow, ActivityReportRow> _reportRows = [];
    private readonly List<CbsTableView> _sectionTables = [];
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _detailCts;
    private bool _initialLoadStarted;
    private bool _isRenderingSections;

    public CbsTableView? StatusChangesTable { get; private set; }
    public CbsTableView? PendingStagesTable { get; private set; }
    public CbsTableView? AddedContractsTable { get; private set; }
    public CbsTableView? DeadlineChangesTable { get; private set; }
    public CbsTableView? CommentsTable { get; private set; }
    public CbsTableView? FundingTable { get; private set; }
    public CbsTableView? PaymentsTable { get; private set; }

    public bool StatusChangesIsExpanded { get; set; }
    public bool PendingStagesIsExpanded { get; set; }
    public bool AddedContractsIsExpanded { get; set; }
    public bool DeadlineChangesIsExpanded { get; set; }
    public bool CommentsIsExpanded { get; set; }
    public bool FundingIsExpanded { get; set; }
    public bool PaymentsIsExpanded { get; set; }

    public ActivityReportHostView()
    {
        InitializeComponent();
        _store = App.Services.GetRequiredService<ActivityReportStore>();
        _loader = App.Services.GetRequiredService<ActivityReportLoader>();
        _workflowFactory = App.Services.GetRequiredService<ContractWorkflowFactory>();
        _workflowStore = App.Services.GetRequiredService<ContractWorkflowStore>();
        _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
        _localUserSettingsService = App.Services.GetRequiredService<ILocalUserSettingsService>();
        _localUserSettings = _localUserSettingsService.Get();

        StartDatePicker.Date = _store.StartDate;
        EndDatePicker.Date = _store.EndDate;
        RegisterSectionExpansion(ActivityReport_StatusChanges);
        RegisterSectionExpansion(ActivityReport_PendingStages);
        RegisterSectionExpansion(ActivityReport_AddedContracts);
        RegisterSectionExpansion(ActivityReport_DeadlineChanges);
        RegisterSectionExpansion(ActivityReport_Comments);
        RegisterSectionExpansion(ActivityReport_Funding);
        RegisterSectionExpansion(ActivityReport_Payments);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string? Route { get; set; }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (StartDatePicker.Date is not DateTimeOffset start || EndDatePicker.Date is not DateTimeOffset end)
        {
            MessageBar.Message = "Укажите начало и окончание периода отчета.";
            MessageBar.IsOpen = true;
            return;
        }

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _store.StartDate = start;
        _store.EndDate = end;
        LoadProgress.IsActive = true;
        LoadProgress.Visibility = Visibility.Visible;
        MessageBar.IsOpen = false;
        await _store.LoadAsync(_loader, _loadCts.Token);
        if (_loadCts.IsCancellationRequested)
        {
            return;
        }

        LoadProgress.IsActive = false;
        LoadProgress.Visibility = Visibility.Collapsed;
        if (!string.IsNullOrWhiteSpace(_store.ErrorMessage))
        {
            MessageBar.Message = _store.ErrorMessage;
            MessageBar.IsOpen = true;
            return;
        }

        RenderSections();
    }

    private void RenderSections()
    {
        _reportRows.Clear();
        _sectionTables.Clear();
        _isRenderingSections = true;
        try
        {
            foreach (var section in _store.Sections)
            {
                foreach (var row in section.Rows)
                {
                    _reportRows[row.DisplayRow] = row;
                }

                var isExpanded = _localUserSettings.ActivityReportSectionExpansion.TryGetValue(section.Kind.ToString(), out var savedExpansion)
                    ? savedExpansion
                    : section.Rows.Count > 0;
                SetSection(section, BuildTable(section, $"ActivityReport_{section.Kind}"), isExpanded);
            }

            Bindings.Update();
        }
        finally
        {
            _isRenderingSections = false;
        }
    }

    private void RegisterSectionExpansion(TreeViewNode branch) =>
        branch.RegisterPropertyChangedCallback(TreeViewNode.IsExpandedProperty, OnSectionExpansionChanged);

    private async void OnSectionExpansionChanged(DependencyObject sender, DependencyProperty property)
    {
        if (_isRenderingSections)
        {
            return;
        }

        var branch = (TreeViewNode)sender;
        await SaveSectionExpansionAsync(GetSectionKey(branch), branch.IsExpanded);
    }

    private async Task SaveSectionExpansionAsync(string key, bool isExpanded)
    {
        _localUserSettings.ActivityReportSectionExpansion[key] = isExpanded;
        await _localUserSettingsService.SaveAsync(_localUserSettings);
    }

    private CbsTableView BuildTable(ActivityReportSection section, string name)
    {
        var table = new CbsTableView
        {
            Name = name,
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
            DetailView.Visibility = Visibility.Collapsed;
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
            if (!_workflowFactory.IsLatest(context))
            {
                return false;
            }

            context.ApplyTo(_workflowStore, strategy);
            DetailView.Visibility = Visibility.Visible;
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

    private void SetSection(ActivityReportSection section, CbsTableView table, bool isExpanded)
    {
        switch (section.Kind)
        {
            case ActivityReportSectionKind.StatusChanges:
                StatusChangesTable = table;
                StatusChangesIsExpanded = isExpanded;
                StatusChangesCount.Text = section.Rows.Count.ToString();
                break;
            case ActivityReportSectionKind.PendingStages:
                PendingStagesTable = table;
                PendingStagesIsExpanded = isExpanded;
                PendingStagesCount.Text = section.Rows.Count.ToString();
                break;
            case ActivityReportSectionKind.AddedContracts:
                AddedContractsTable = table;
                AddedContractsIsExpanded = isExpanded;
                AddedContractsCount.Text = section.Rows.Count.ToString();
                break;
            case ActivityReportSectionKind.DeadlineChanges:
                DeadlineChangesTable = table;
                DeadlineChangesIsExpanded = isExpanded;
                DeadlineChangesCount.Text = section.Rows.Count.ToString();
                break;
            case ActivityReportSectionKind.Comments:
                CommentsTable = table;
                CommentsIsExpanded = isExpanded;
                CommentsCount.Text = section.Rows.Count.ToString();
                break;
            case ActivityReportSectionKind.Funding:
                FundingTable = table;
                FundingIsExpanded = isExpanded;
                FundingCount.Text = section.Rows.Count.ToString();
                break;
            case ActivityReportSectionKind.Payments:
                PaymentsTable = table;
                PaymentsIsExpanded = isExpanded;
                PaymentsCount.Text = section.Rows.Count.ToString();
                break;
            default:
                throw new InvalidOperationException($"Неизвестный раздел отчета: {section.Kind}.");
        }
    }

    private string GetSectionKey(TreeViewNode branch) => branch switch
    {
        _ when ReferenceEquals(branch, ActivityReport_StatusChanges) => ActivityReportSectionKind.StatusChanges.ToString(),
        _ when ReferenceEquals(branch, ActivityReport_PendingStages) => ActivityReportSectionKind.PendingStages.ToString(),
        _ when ReferenceEquals(branch, ActivityReport_AddedContracts) => ActivityReportSectionKind.AddedContracts.ToString(),
        _ when ReferenceEquals(branch, ActivityReport_DeadlineChanges) => ActivityReportSectionKind.DeadlineChanges.ToString(),
        _ when ReferenceEquals(branch, ActivityReport_Comments) => ActivityReportSectionKind.Comments.ToString(),
        _ when ReferenceEquals(branch, ActivityReport_Funding) => ActivityReportSectionKind.Funding.ToString(),
        _ when ReferenceEquals(branch, ActivityReport_Payments) => ActivityReportSectionKind.Payments.ToString(),
        _ => throw new InvalidOperationException("Неизвестная ветка отчета активности.")
    };

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
