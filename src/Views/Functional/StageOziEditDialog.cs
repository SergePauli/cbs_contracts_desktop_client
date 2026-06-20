using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
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

    private StageEditState _stage;
    private ContractEditState? _contract;
    private readonly IReadOnlyList<CbsTableFilterOptionDefinition> _statusOptions;
    private readonly IReadOnlyList<ReferenceLookupItem> _employeeItems;
    private IReadOnlyList<StagePerformerOption> _performerOptions;
    private StageEditDialogNavigationState? _navigationState;
    private readonly Func<StageEditDialogNavigationDirection, Task<StageEditDialogNavigationResult?>>? _navigateAsync;
    private readonly int? _profileId;
    private readonly CalendarInput _rideOutAtEditor = new();
    private readonly CalendarInput _sendedAtEditor = new();
    private readonly CalendarInput _completedAtEditor = new();
    private readonly CalendarInput _closedAtEditor = new();
    private readonly Dropdown _statusBox = new();
    private readonly CheckBox _isRideOutBox = new() { Content = "Выехали" };
    private readonly CheckBox _isSendedBox = new() { Content = "Отправили" };
    private readonly CheckBox _toRegistryBox = new() { Content = "Реестр" };
    private readonly TextBox _registryQuarterBox = BuildNumberTextBox();
    private readonly TextBox _registryYearBox = BuildNumberTextBox();
    private readonly MultiSelect _performersMultiSelect = new();
    private readonly TextBox _commentBox = new();
    private readonly StageOziEditView _view = new();
    private bool _isApplyingBusinessLogic;
    private bool _businessLogicHandlersAttached;

    public StageOziEditDialog(
        StageEditState stage,
        ContractEditState? contract,
        IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions,
        IReadOnlyList<ReferenceLookupItem> employeeItems,
        int? profileId,
        StageEditDialogNavigationState? navigationState = null,
        Func<StageEditDialogNavigationDirection, Task<StageEditDialogNavigationResult?>>? navigateAsync = null)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(statusOptions);
        ArgumentNullException.ThrowIfNull(employeeItems);

        _stage = stage;
        _contract = contract;
        _statusOptions = statusOptions;
        _employeeItems = employeeItems;
        _profileId = profileId;
        _navigationState = navigationState;
        _navigateAsync = navigateAsync;
        _performerOptions = CreatePerformerOptions(employeeItems, stage.Performers);
        _view.PreviousButton.Click += StageNavigationButton_Click;
        _view.NextButton.Click += StageNavigationButton_Click;

        Resources["ContentDialogMinWidth"] = 760d;
        Resources["ContentDialogMaxWidth"] = 860d;
        Content = BuildContent();
        DialogChrome.Apply(this, _stage.GetEditDialogTitle());
    }

    public long Id => _stage.Id;

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

        scrollViewer.Content = _view;
        InitializeStaticView();
        RenderStageContent();
        return BuildEditContent(scrollViewer);
    }

    private void InitializeStaticView()
    {
        _view.ContractTitleSlot.Content = BuildDialogSectionTitle(RequireContract().GetSectionTitle());
        _view.ExternalNumberValue.Text = FormatSummaryValue(_contract?.ExternalNumber ?? string.Empty);
        _view.ContragentValue.Text = FormatSummaryValue(_contract?.ContragentName ?? string.Empty);
        _view.SignedAtValue.Text = FormatSummaryValue(FormatDisplayDate(_contract?.SignedAt));
        _view.ContractStatusSlot.Content = BuildStatusBadge(
            RequireContract().Status.Name!,
            RequireContract().Status.Id,
            horizontalAlignment: HorizontalAlignment.Left);
        _view.ContractCostValue.Text = FormatSummaryValue(FormatMoney(_contract?.Cost));
        _view.ContractKindValue.Text = FormatSummaryValue(BuildContractKindText());
        InitializeNavigationButton(_view.PreviousButton, "Предыдущий этап", StageEditDialogNavigationDirection.Previous);
        InitializeNavigationButton(_view.NextButton, "Следующий этап", StageEditDialogNavigationDirection.Next);
        InitializeEditorSlots();
    }

    private static void InitializeNavigationButton(
        Button button,
        string tooltip,
        StageEditDialogNavigationDirection direction)
    {
        button.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 99, 102, 241));
        button.BorderBrush = button.Background;
        button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 79, 70, 229));
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 67, 56, 202));
        button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 79, 70, 229));
        button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 67, 56, 202));
        ToolTipService.SetToolTip(button, tooltip);
        button.Tag = direction;
    }

    private void InitializeEditorSlots()
    {
        _view.PerformersSlot.Content = _performersMultiSelect;
        _view.RideOutCheckSlot.Content = _isRideOutBox;
        _view.RideOutAtSlot.Content = _rideOutAtEditor;
        _view.SendedCheckSlot.Content = _isSendedBox;
        _view.SendedAtSlot.Content = _sendedAtEditor;
        _view.RegistryCheckSlot.Content = _toRegistryBox;
        _view.RegistryQuarterSlot.Content = _registryQuarterBox;
        _view.RegistryYearSlot.Content = _registryYearBox;
        _view.StatusSlot.Content = _statusBox;
        _view.CompletedAtSlot.Content = _completedAtEditor;
        _view.ClosedAtSlot.Content = _closedAtEditor;
        _view.CommentSlot.Content = _commentBox;
        _commentBox.PlaceholderText = _profileId is null
            ? "Комментарий недоступен: не получен profile_id пользователя"
            : "Комментарий";
        _commentBox.IsEnabled = _profileId is not null;
        AttachBusinessLogicHandlers();
    }

    private void RenderStageContent()
    {
        UpdateStageSummaryPanel();
        UpdateStageEditors();
    }

    private void UpdateStageSummaryPanel()
    {
        UpdateStageTitle();
        UpdateStageNavigationButtons();
        _view.StartAtValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.StartAt));
        _view.PrepaymentValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.PrepaymentAt ?? _stage.PaymentAt));
        _view.DeadlineAtValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.DeadlineAt));
        _view.TasksValue.Text = FormatSummaryValue(BuildTasksText());
    }

    private void UpdateStageTitle()
    {
        _view.StageTitleValue.Inlines.Clear();
        var title = _stage.GetSectionTitle(_contract);
        var accentText = _stage.GetSectionTitleAmount(_contract);
        if (string.IsNullOrWhiteSpace(accentText))
        {
            _view.StageTitleValue.Text = title;
            return;
        }

        _view.StageTitleValue.Text = string.Empty;
        _view.StageTitleValue.Inlines.Add(new Run { Text = title + " " });
        _view.StageTitleValue.Inlines.Add(new Run
        {
            Text = accentText,
            Foreground = Application.Current.Resources["ShellAccentBrush"] as Brush
        });
    }

    private void UpdateStageNavigationButtons()
    {
        var state = _navigationState ?? new StageEditDialogNavigationState(false, false);
        var hasNavigation = state.CanPrevious || state.CanNext;
        _view.PreviousButton.Visibility = hasNavigation ? Visibility.Visible : Visibility.Collapsed;
        _view.NextButton.Visibility = hasNavigation ? Visibility.Visible : Visibility.Collapsed;
        _view.PreviousButton.IsEnabled = state.CanPrevious;
        _view.NextButton.IsEnabled = state.CanNext;
    }

    private void UpdateStageEditors()
    {
        _isApplyingBusinessLogic = true;
        try
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
            ConfigureStatusDropdown(_statusBox, BuildStageStatusOptions(_statusOptions), _stage.Status.Id);
        }
        finally
        {
            _isApplyingBusinessLogic = false;
        }

        ConfigurePerformersMultiSelect();
        ApplyBusinessLogic();
    }

    private static string FormatSummaryValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private void AttachBusinessLogicHandlers()
    {
        if (_businessLogicHandlersAttached)
        {
            return;
        }

        _businessLogicHandlersAttached = true;
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

    private async void RequestNavigation(StageEditDialogNavigationDirection direction)
    {
        try
        {
            if (_navigateAsync is null)
            {
                return;
            }

            var result = await _navigateAsync(direction);
            if (result is null)
            {
                return;
            }

            ApplyNavigationResult(result);
        }
        catch (Exception ex)
        {
            ShowErrorInfo(ex.Message);
        }
    }

    private void ApplyNavigationResult(StageEditDialogNavigationResult result)
    {
        var oldStage = _stage;
        var oldContract = _contract;
        var oldNavigationState = _navigationState;
        var oldPerformerOptions = _performerOptions;

        try
        {
            _stage = result.Stage;
            _contract = result.Contract;
            _navigationState = result.NavigationState;
            _performerOptions = CreatePerformerOptions(_employeeItems, _stage.Performers);
            RenderStageContent();
        }
        catch
        {
            _stage = oldStage;
            _contract = oldContract;
            _navigationState = oldNavigationState;
            _performerOptions = oldPerformerOptions;
            RenderStageContent();
            throw;
        }
    }

    private void StageNavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: StageEditDialogNavigationDirection direction })
        {
            RequestNavigation(direction);
        }
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
