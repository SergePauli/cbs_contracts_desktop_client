using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pauli.WinUiKit.Controls;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;
using static CbsContractsDesktopClient.Shared.Dialogs.StageContractStatusDialogControls;
using static CbsContractsDesktopClient.Shared.Formatting.AppFormatters;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed class StageOziEditDialog : AppEditDialog
{
    private const long StatusDone = 4;
    private const long StatusClosed = 5;

    private readonly StageEditState _stage;
    private readonly ContractEditState? _contract;
    private readonly IReadOnlyList<CbsTableFilterOptionDefinition> _statusOptions;
    private readonly IReadOnlyList<StagePerformerOption> _performerOptions;
    private readonly int? _profileId;
    private readonly CalendarInput _rideOutAtEditor = new();
    private readonly CalendarInput _sendedAtEditor = new();
    private readonly CalendarInput _completedAtEditor = new();
    private readonly CalendarInput _closedAtEditor = new();
    private readonly ComboBox _statusBox = new();
    private readonly CheckBox _isRideOutBox = new() { Content = "Выехали" };
    private readonly CheckBox _isSendedBox = new() { Content = "Отправили" };
    private readonly CheckBox _toRegistryBox = new() { Content = "Реестр" };
    private readonly TextBox _registryQuarterBox = BuildNumberTextBox();
    private readonly TextBox _registryYearBox = BuildNumberTextBox();
    private readonly MultiSelect _performersMultiSelect = new();
    private readonly TextBox _commentBox = new();
    private bool _isApplyingBusinessLogic;

    public StageOziEditDialog(
        StageEditState stage,
        ContractEditState? contract,
        IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions,
        IReadOnlyList<ReferenceLookupItem> employeeItems,
        int? profileId)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(statusOptions);
        ArgumentNullException.ThrowIfNull(employeeItems);

        _stage = stage;
        _contract = contract;
        _statusOptions = statusOptions;
        _profileId = profileId;
        _performerOptions = CreatePerformerOptions(employeeItems, stage.Performers);
        Id = stage.Id;

        Resources["ContentDialogMinWidth"] = 760d;
        Resources["ContentDialogMaxWidth"] = 940d;
        Content = BuildContent();
        DialogChrome.Apply(this, _stage.GetEditDialogTitle());
    }

    public long Id { get; }

    public bool ShouldCloseContract()
    {
        SyncStageStateFromEditors();
        return _stage.ShouldCloseContract(_contract, StatusClosed);
    }

    public IReadOnlyDictionary<string, object?> BuildContractClosePayload()
    {
        return StageEditPayloadBuilderHelpers.BuildContractClosePayload(RequireContract().Id, _closedAtEditor.Date);
    }

    public IReadOnlyDictionary<string, object?> BuildPayload()
    {
        SyncStageStateFromEditors();
        return StageOziEditPayloadBuilder.BuildForUpdate(
            _stage,
            BuildSelectedPerformers(),
            _commentBox.Text,
            _profileId);
    }

    public IReadOnlyDictionary<string, object?> BuildTablePatch()
    {
        var patch = new Dictionary<string, object?>(BuildPayload(), StringComparer.OrdinalIgnoreCase);
        patch.Remove("comments_attributes");
        patch.Remove("performers_attributes");

        var selectedStatus = GetSelectedStatusOption();
        if (patch.ContainsKey("status_id") && selectedStatus?.Value is long statusId)
        {
            patch["status"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = statusId,
                ["name"] = selectedStatus.Label
            };
        }

        patch["performers"] = BuildSelectedPerformers()
            .Select(static performer => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = performer.Id,
                ["list_key"] = performer.ListKey,
                ["employee_id"] = performer.EmployeeId,
                ["name"] = performer.Name,
                ["priority"] = performer.Priority
            })
            .ToList();

        return patch;
    }

    public override bool Validate()
    {
        if (_isRideOutBox.IsChecked == true && _rideOutAtEditor.Date is null)
        {
            ShowErrorInfo("Укажите дату выезда к клиенту.");
            return false;
        }

        if (_isSendedBox.IsChecked == true && _sendedAtEditor.Date is null)
        {
            ShowErrorInfo("Укажите дату отправки документов.");
            return false;
        }

        if (GetSelectedStatusOption()?.Value == StatusClosed && _closedAtEditor.Date is null)
        {
            ShowErrorInfo("Для закрытого этапа укажите дату закрытия.");
            return false;
        }

        if (_toRegistryBox.IsChecked == true
            && (TryGetInt(_registryQuarterBox.Text) is not (>= 1 and <= 4)
                || TryGetInt(_registryYearBox.Text) is null))
        {
            ShowErrorInfo("Для реестра укажите квартал от 1 до 4 и год.");
            return false;
        }

        ShowErrorInfo(string.Empty);
        return true;
    }

    private UIElement BuildContent()
    {
        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 640,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var stack = new StackPanel { Spacing = 14 };
        scrollViewer.Content = stack;
        stack.Children.Add(BuildSummaryPanel());
        stack.Children.Add(BuildEditorsArea());

        return BuildEditContent(new Grid
        {
            MinWidth = 720,
            MaxWidth = 900,
            Children = { scrollViewer }
        });
    }

    private UIElement BuildSummaryPanel()
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(BuildDialogSectionTitle(RequireContract().GetSectionTitle()));

        var contractGrid = new Grid { ColumnSpacing = 18, RowSpacing = 6 };
        contractGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contractGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel { Spacing = 6 };
        left.Children.Add(BuildSummaryLine("Внешний номер", _contract?.ExternalNumber ?? string.Empty));
        left.Children.Add(BuildSummaryLine("Контрагент", _contract?.ContragentName ?? string.Empty));
        left.Children.Add(BuildSummaryLine("Дата подписания", FormatDisplayDate(_contract?.SignedAt)));

        var right = new StackPanel { Spacing = 6 };
        right.Children.Add(BuildSummaryElement("Статус", BuildStatusBadge(
            RequireContract().Status.Name!,
            RequireContract().Status.Id,
            horizontalAlignment: HorizontalAlignment.Left)));
        right.Children.Add(BuildAccentSummaryLine("Стоимость", FormatMoney(_contract?.Cost)));
        right.Children.Add(BuildSummaryLine("Особенности", BuildContractKindText()));

        contractGrid.Children.Add(left);
        Grid.SetColumn(right, 1);
        contractGrid.Children.Add(right);
        stack.Children.Add(contractGrid);

        stack.Children.Add(BuildDialogSectionTitle(
            _stage.GetSectionTitle(_contract),
            _stage.GetSectionTitleAmount(_contract)));

        var stageGrid = new Grid { ColumnSpacing = 18, RowSpacing = 6 };
        stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var stageLeft = new StackPanel { Spacing = 6 };
        stageLeft.Children.Add(BuildSummaryLine("Дата начала", FormatDisplayDate(_stage.StartAt)));
        stageLeft.Children.Add(BuildSummaryLine("Предоплата", FormatDisplayDate(_stage.PrepaymentAt ?? _stage.PaymentAt)));

        var stageRight = new StackPanel { Spacing = 6 };
        stageRight.Children.Add(BuildSummaryLine("Срок завершения", FormatDisplayDate(_stage.DeadlineAt)));
        stageRight.Children.Add(BuildSummaryLine("Прочее", BuildTasksText()));

        stageGrid.Children.Add(stageLeft);
        Grid.SetColumn(stageRight, 1);
        stageGrid.Children.Add(stageRight);
        stack.Children.Add(stageGrid);

        return new Border
        {
            Padding = new Thickness(0, 0, 0, 12),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Application.Current.Resources["ShellTableGridLineBrush"] as Brush,
            Child = stack
        };
    }

    private UIElement BuildEditorsArea()
    {
        _rideOutAtEditor.Date = _stage.RideOutAt;
        _sendedAtEditor.Date = _stage.SendedAt;
        _completedAtEditor.Date = _stage.CompletedAt;
        _closedAtEditor.Date = _stage.ClosedAt;
        _isRideOutBox.IsChecked = _stage.IsRideOut == true;
        _isSendedBox.IsChecked = _stage.IsSended == true;
        _toRegistryBox.IsChecked = _stage.RegistryQuarter is not null || _stage.RegistryYear is not null;
        _registryQuarterBox.Text = _stage.RegistryQuarter?.ToString() ?? string.Empty;
        _registryYearBox.Text = _stage.RegistryYear?.ToString() ?? string.Empty;

        ConfigureStatusCombo(_statusBox, BuildStageStatusOptions(_statusOptions), _stage.Status.Id);
        ConfigurePerformersMultiSelect();
        AttachBusinessLogicHandlers();
        ApplyBusinessLogic();

        var grid = new Grid { ColumnSpacing = 18, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel { Spacing = 10 };
        left.Children.Add(BuildSectionTitle("Выполнение"));
        left.Children.Add(BuildLabeledControl("Исполнители", _performersMultiSelect));
        left.Children.Add(BuildCheckDateRow(_isRideOutBox, _rideOutAtEditor));
        left.Children.Add(BuildCheckDateRow(_isSendedBox, _sendedAtEditor));
        left.Children.Add(BuildRegistryEditors());

        var right = new StackPanel { Spacing = 10 };
        right.Children.Add(BuildSectionTitle("Состояние"));
        right.Children.Add(BuildLabeledControl("Статус этапа", _statusBox));
        right.Children.Add(BuildLabeledControl("Работа выполнена", _completedAtEditor));
        right.Children.Add(BuildLabeledControl("Закрыт", _closedAtEditor));

        var leftPanel = BuildEditorGroupPanel(left, DialogEditorGroupTone.Accent);
        var rightPanel = BuildEditorGroupPanel(right, DialogEditorGroupTone.Muted);
        grid.Children.Add(leftPanel);
        Grid.SetColumn(rightPanel, 1);
        grid.Children.Add(rightPanel);

        var root = new StackPanel { Spacing = 12 };
        root.Children.Add(grid);
        _commentBox.PlaceholderText = _profileId is null
            ? "Комментарий недоступен: не получен profile_id пользователя"
            : "Комментарий";
        _commentBox.IsEnabled = _profileId is not null;
        root.Children.Add(BuildLabeledControl("Комментарий", _commentBox));
        return root;
    }

    private static UIElement BuildCheckDateRow(CheckBox checkBox, CalendarInput dateEditor)
    {
        checkBox.VerticalAlignment = VerticalAlignment.Center;
        dateEditor.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(checkBox);
        Grid.SetColumn(dateEditor, 1);
        grid.Children.Add(dateEditor);
        return grid;
    }

    private UIElement BuildRegistryEditors()
    {
        _toRegistryBox.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(_toRegistryBox);

        var quarterEditor = BuildInlineTextBox("Квартал", _registryQuarterBox);
        Grid.SetColumn(quarterEditor, 1);
        grid.Children.Add(quarterEditor);

        var yearEditor = BuildInlineTextBox("Год", _registryYearBox);
        Grid.SetColumn(yearEditor, 2);
        grid.Children.Add(yearEditor);
        return grid;
    }

    private static FrameworkElement BuildInlineTextBox(string label, TextBox textBox)
    {
        textBox.VerticalAlignment = VerticalAlignment.Center;

        var grid = new Grid { ColumnSpacing = 6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });

        Grid.SetColumn(textBox, 1);
        grid.Children.Add(textBox);
        return grid;
    }

    private void AttachBusinessLogicHandlers()
    {
        _isRideOutBox.Checked += (_, _) => ApplyBusinessLogic();
        _isRideOutBox.Unchecked += (_, _) => ApplyBusinessLogic();
        _isSendedBox.Checked += (_, _) => ApplyBusinessLogic();
        _isSendedBox.Unchecked += (_, _) => ApplyBusinessLogic();
        _toRegistryBox.Checked += (_, _) => ApplyBusinessLogic();
        _toRegistryBox.Unchecked += (_, _) => ApplyBusinessLogic();
        _statusBox.SelectionChanged += (_, _) => ApplyBusinessLogic();
    }

    private void ApplyBusinessLogic()
    {
        if (_isApplyingBusinessLogic)
        {
            return;
        }

        _isApplyingBusinessLogic = true;
        try
        {
            if (_isRideOutBox.IsChecked != true)
            {
                _rideOutAtEditor.Date = null;
            }
            _rideOutAtEditor.IsReadOnly = _isRideOutBox.IsChecked != true;

            if (_isSendedBox.IsChecked != true)
            {
                _sendedAtEditor.Date = null;
            }
            _sendedAtEditor.IsReadOnly = _isSendedBox.IsChecked != true;

            if (GetSelectedStatusOption()?.Value == StatusDone && _completedAtEditor.Date is null)
            {
                _completedAtEditor.Date = DateTimeOffset.Now;
            }

            if (GetSelectedStatusOption()?.Value == StatusClosed && _closedAtEditor.Date is null)
            {
                _closedAtEditor.Date = DateTimeOffset.Now;
            }

            var hasRegistry = _toRegistryBox.IsChecked == true;
            _registryQuarterBox.IsEnabled = hasRegistry;
            _registryYearBox.IsEnabled = hasRegistry;
            if (hasRegistry)
            {
                _registryQuarterBox.Text = string.IsNullOrWhiteSpace(_registryQuarterBox.Text)
                    ? ResolveDefaultRegistryQuarter().ToString()
                    : _registryQuarterBox.Text;
                _registryYearBox.Text = string.IsNullOrWhiteSpace(_registryYearBox.Text)
                    ? ResolveDefaultRegistryYear().ToString()
                    : _registryYearBox.Text;
            }
            else
            {
                _registryQuarterBox.Text = string.Empty;
                _registryYearBox.Text = string.Empty;
            }
        }
        finally
        {
            _isApplyingBusinessLogic = false;
        }
    }

    private void ConfigurePerformersMultiSelect()
    {
        _performersMultiSelect.Options = _performerOptions;
        _performersMultiSelect.Value = _performerOptions
            .Where(static option => option.IsSelected)
            .ToList();
        _performersMultiSelect.OptionLabel = nameof(StagePerformerOption.Name);
        _performersMultiSelect.Display = "chip";
        _performersMultiSelect.MaxSelectedLabels = 4;
        _performersMultiSelect.Placeholder = "Исполнители";
        _performersMultiSelect.Tooltip = "Исполнители";
    }

    private void SyncStageStateFromEditors()
    {
        _stage.Status = new StatusEditState(GetSelectedStatusOption()?.Value, GetSelectedStatusOption()?.Label);
        _stage.CompletedAt = _completedAtEditor.Date;
        _stage.ClosedAt = _closedAtEditor.Date;
        _stage.IsRideOut = _isRideOutBox.IsChecked == true;
        _stage.RideOutAt = _stage.IsRideOut == true ? _rideOutAtEditor.Date : null;
        _stage.IsSended = _isSendedBox.IsChecked == true;
        _stage.SendedAt = _stage.IsSended == true ? _sendedAtEditor.Date : null;
        _stage.RegistryQuarter = _toRegistryBox.IsChecked == true ? TryGetInt(_registryQuarterBox.Text) : null;
        _stage.RegistryYear = _toRegistryBox.IsChecked == true ? TryGetInt(_registryYearBox.Text) : null;
        _stage.Performers = BuildSelectedPerformers();
    }

    private IReadOnlyList<StagePerformerEditState> BuildSelectedPerformers()
    {
        var result = new List<StagePerformerEditState>();
        var priority = 0;
        foreach (var option in (_performersMultiSelect.Value ?? Array.Empty<object>()).OfType<StagePerformerOption>())
        {
            result.Add(new StagePerformerEditState(
                option.ExistingId,
                option.ExistingListKey,
                option.EmployeeId,
                option.Name,
                priority));
            priority++;
        }

        return result;
    }

    private int ResolveDefaultRegistryQuarter()
    {
        var date = _stage.StartAt ?? DateTimeOffset.Now;
        return ((date.Month - 1) / 3) + 1;
    }

    private int ResolveDefaultRegistryYear()
    {
        return (_contract?.SignedAt ?? _stage.StartAt ?? DateTimeOffset.Now).Year;
    }

    private string BuildContractKindText()
    {
        var parts = new List<string>();
        if (_contract?.Governmental == true)
        {
            parts.Add("государственный");
        }

        if (_contract?.IsMultiStage == true)
        {
            parts.Add("многоэтапный");
        }

        return parts.Count == 0 ? "-" : string.Join("; ", parts);
    }

    private string BuildTasksText()
    {
        return _stage.Tasks.Count == 0
            ? "НЕТ"
            : string.Join("; ", _stage.Tasks.Select(static task => task.Name).Where(static name => !string.IsNullOrWhiteSpace(name)));
    }

    private EnumSelectOption? GetSelectedStatusOption()
    {
        return StageContractStatusDialogControls.GetSelectedStatusOption(_statusBox);
    }

    private ContractEditState RequireContract()
    {
        return _contract
            ?? throw new InvalidOperationException("Stage edit dialog requires selected contract edit state.");
    }

    private static IReadOnlyList<StagePerformerOption> CreatePerformerOptions(
        IReadOnlyList<ReferenceLookupItem> employeeItems,
        IReadOnlyList<StagePerformerEditState> selectedPerformers)
    {
        var selectedByEmployee = selectedPerformers
            .Where(static performer => performer.EmployeeId is not null)
            .ToDictionary(static performer => performer.EmployeeId!.Value);

        var options = employeeItems
            .Select(item =>
            {
                var employeeId = JsonDataReader.TryGetLong(item.Id) ?? 0;
                selectedByEmployee.TryGetValue(employeeId, out var selected);
                return new StagePerformerOption(
                    employeeId,
                    item.DisplayName,
                    selected?.Id,
                    selected?.ListKey,
                    selected?.Priority,
                    selected is not null);
            })
            .Where(static item => item.EmployeeId > 0 && !string.IsNullOrWhiteSpace(item.Name))
            .ToList();

        var knownEmployeeIds = options.Select(static option => option.EmployeeId).ToHashSet();
        options.AddRange(selectedPerformers
            .Where(performer => performer.EmployeeId is long employeeId && !knownEmployeeIds.Contains(employeeId))
            .Select(performer => new StagePerformerOption(
                performer.EmployeeId!.Value,
                performer.Name,
                performer.Id,
                performer.ListKey,
                performer.Priority,
                true)));

        return options
            .OrderByDescending(static option => option.IsSelected)
            .ThenBy(static option => option.Priority ?? int.MaxValue)
            .ThenBy(static option => option.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static int? TryGetInt(string? value)
    {
        return int.TryParse(value, out var parsedValue) ? parsedValue : null;
    }

    private sealed record StagePerformerOption(
        long EmployeeId,
        string Name,
        long? ExistingId,
        string? ExistingListKey,
        int? Priority,
        bool IsSelected);
}
