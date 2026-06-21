using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Shared.Dates;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using Microsoft.Extensions.DependencyInjection;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;
using static CbsContractsDesktopClient.Shared.Dialogs.StageContractStatusDialogControls;
using static CbsContractsDesktopClient.Shared.Dialogs.StageContractDeadlineDialogOptions;
using static CbsContractsDesktopClient.Shared.Formatting.AppFormatters;
using CbsContractsDesktopClient.Views.Controls;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pauli.WinUiKit.Controls;

namespace CbsContractsDesktopClient.Views.Functional
{
    public sealed class StageCommerEditDialog : AppEditDialog
    {
        private StageEditState _stage;
        private ContractEditState? _contract;
        private readonly IHolidayRecalculationService _holidayRecalculationService;
        private readonly IReadOnlyList<CbsTableFilterOptionDefinition> _statusOptions;
        private readonly IReadOnlyList<ReferenceLookupItem> _taskKindItems;
        private IReadOnlyList<StageTaskOption> _taskOptions = [];
        private List<StageTaskRecord> _originalTasks = [];
        private StageEditDialogNavigationState? _navigationState;
        private readonly Func<StageEditDialogNavigationDirection, Task<StageEditDialogNavigationResult?>>? _navigateAsync;
        private IReadOnlyList<HolidayCalendarDay> _holidays = [];
        private readonly int? _profileId;
        private readonly CalendarInput _startAtEditor = new();
        private readonly CalendarInput _deadlineAtEditor = new();
        private readonly CalendarInput _paymentDeadlineAtEditor = new();
        private readonly CalendarInput _closedAtEditor = new();
        private readonly TextBox _durationBox = BuildNumberTextBox();
        private readonly TextBox _paymentDurationBox = BuildNumberTextBox();
        private readonly TextBox _costBox = BuildMoneyInputTextBox();
        private readonly ComboBox _deadlineKindBox = new();
        private readonly ComboBox _paymentDeadlineKindBox = new();
        private readonly Dropdown _statusBox = new();
        private readonly MultiSelect _tasksMultiSelect = new();
        private readonly StageCommerEditView _view = new();
        private readonly HashSet<long> _selectedTaskKindIds = [];
        private readonly TextBox _commentBox = new();
        private bool _isApplyingBusinessLogic;
        private bool _businessLogicHandlersAttached;
        private bool _deadlineAtEditedManually;
        private bool _paymentDeadlineAtEditedManually;

        public StageCommerEditDialog(
            StageEditState stage,
            ContractEditState? contract,
            IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions,
            IReadOnlyList<ReferenceLookupItem> taskKindItems,
            int? profileId,
            StageEditDialogNavigationState? navigationState = null,
            Func<StageEditDialogNavigationDirection, Task<StageEditDialogNavigationResult?>>? navigateAsync = null)
        {
            ArgumentNullException.ThrowIfNull(stage);
            ArgumentNullException.ThrowIfNull(statusOptions);
            ArgumentNullException.ThrowIfNull(taskKindItems);

            _stage = stage;
            _contract = contract;
            _holidayRecalculationService = App.Services.GetRequiredService<IHolidayRecalculationService>();
            _statusOptions = statusOptions;
            _taskKindItems = taskKindItems;
            _profileId = profileId;
            _navigationState = navigationState;
            _navigateAsync = navigateAsync;
            _view.PreviousButton.Click += StageNavigationButton_Click;
            _view.NextButton.Click += StageNavigationButton_Click;
            ResetTaskSelection();

            Resources["ContentDialogMinWidth"] = 780d;
            Resources["ContentDialogMaxWidth"] = 800d;
            Content = BuildContent(statusOptions);
            DialogChrome.Apply(this, _stage.GetEditDialogTitle());
            Loaded += StageCommerEditDialog_Loaded;
        }

        public long Id => _stage.Id;

        public bool ShouldCloseContract()
        {
            SyncStageStateFromEditors();
            return _stage.ShouldCloseContract(_contract, WorkflowStatusIds.Closed);
        }

        public IReadOnlyDictionary<string, object?> BuildContractClosePayload()
        {
            return StageCommerEditPayloadBuilder.BuildContractClosePayload(RequireContract().Id, _closedAtEditor.Date);
        }

        private async void StageCommerEditDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= StageCommerEditDialog_Loaded;

            try
            {
                _holidays = await _holidayRecalculationService.GetHolidayCalendarDaysAsync();
                ApplyBusinessLogicAfterFieldChange(applyInitialStart: false);
            }
            catch
            {
                _holidays = [];
            }
        }

        public IReadOnlyDictionary<string, object?> BuildPayload()
        {
            SyncStageStateFromEditors();
            return StageCommerEditPayloadBuilder.BuildForUpdate(
                _stage,
                _selectedTaskKindIds,
                _commentBox.Text,
                _profileId);
        }

        public override bool Validate()
        {
            if (_deadlineAtEditedManually
                && StageDeadlineBusinessRules.IsDeadlineManualMode(GetSelectedDeadlineKind())
                && _deadlineAtEditor.Date is DateTimeOffset deadlineAt
                && _startAtEditor.Date is DateTimeOffset startAt
                && deadlineAt.Date < startAt.Date)
            {
                ShowErrorInfo("Срок выполнения не может быть раньше даты начала этапа.");
                return false;
            }

            if (GetSelectedKey(_paymentDeadlineKindBox) is string paymentKind
                && paymentKind != StageDeadlineBusinessRules.PaymentCalendarPlan
                && paymentKind.Length > 0
                && TryGetInt(_paymentDurationBox.Text) is null)
            {
                ShowErrorInfo("Для выбранного режима оплаты укажите срок в днях.");
                return false;
            }

            if (GetSelectedKey(_paymentDeadlineKindBox) == StageDeadlineBusinessRules.PaymentCalendarPlan
                && _paymentDeadlineAtEditor.Date is null)
            {
                ShowErrorInfo("Для календарного плана укажите срок оплаты.");
                return false;
            }

            var fundedAt = _stage.FundedAt;
            if (_paymentDeadlineAtEditedManually
                && StageDeadlineBusinessRules.IsPaymentDeadlineManualMode(GetSelectedPaymentDeadlineKind())
                && _paymentDeadlineAtEditor.Date is DateTimeOffset paymentDeadlineAt
                && fundedAt is not null
                && paymentDeadlineAt.Date < fundedAt.Value.Date)
            {
                ShowErrorInfo("Срок оплаты не может быть раньше даты бухзакрытия.");
                return false;
            }

            ShowErrorInfo(string.Empty);
            return true;
        }

        private void SyncStageStateFromEditors()
        {
            _stage.Status = new StatusEditState(GetSelectedStatusOption()?.Value, GetSelectedStatusOption()?.Label);
            _stage.DeadlineKind = GetSelectedKey(_deadlineKindBox);
            _stage.DeadlineAt = _deadlineAtEditor.Date;
            _stage.StartAt = _startAtEditor.Date;
            _stage.Cost = TryParseMoney(_costBox.Text);
            _stage.PaymentDeadlineKind = GetSelectedKey(_paymentDeadlineKindBox);
            _stage.PaymentDeadlineAt = _paymentDeadlineAtEditor.Date;
            _stage.Duration = TryGetInt(_durationBox.Text);
            _stage.PaymentDuration = TryGetInt(_paymentDurationBox.Text);
            _stage.ClosedAt = _closedAtEditor.Date;
        }

        private UIElement BuildContent(IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions)
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
            _view.ContractCostValue.Text = FormatSummaryValue(FormatMoney(_contract?.Cost));
            _view.ContractStatusSlot.Content = BuildStatusBadge(
                RequireContract().Status.Name!,
                RequireContract().Status.Id,
                horizontalAlignment: HorizontalAlignment.Left);
            _view.SignedAtValue.Text = FormatSummaryValue(FormatDisplayDate(_contract?.SignedAt));
            _view.GovernmentalValue.Text = FormatSummaryValue(FormatBoolean(_contract?.Governmental));
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
            _view.DeadlineKindSlot.Content = _deadlineKindBox;
            _view.DurationSlot.Content = _durationBox;
            _view.DeadlineAtSlot.Content = _deadlineAtEditor;
            _view.CostSlot.Content = _costBox;
            _view.PaymentDeadlineKindSlot.Content = _paymentDeadlineKindBox;
            _view.PaymentDurationSlot.Content = _paymentDurationBox;
            _view.PaymentDeadlineAtSlot.Content = _paymentDeadlineAtEditor;
            _view.StatusSlot.Content = _statusBox;
            _view.StartAtSlot.Content = _startAtEditor;
            _view.ClosedAtSlot.Content = _closedAtEditor;
            _view.TasksSlot.Content = BuildTasksMultiSelectEditor();
            _view.CommentSlot.Content = _commentBox;
            _commentBox.PlaceholderText = _profileId is null
                ? "Комментарий недоступен: не получен profile_id пользователя"
                : "Комментарий";
            _commentBox.IsEnabled = _profileId is not null;
            AttachBusinessLogicHandlers();
        }

        private void UpdateStageSummaryPanel()
        {
            UpdateStageTitle();
            UpdateStageNavigationButtons();
            _view.PrepaymentValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.PrepaymentAt));
            _view.PaymentValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.PaymentAt));
            _view.FundedValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.FundedAt));
            _view.CompletedValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.CompletedAt));
            _view.RideOutValue.Text = FormatSummaryValue(FormatFlagDate(_stage.IsRideOut, _stage.RideOutAt));
            _view.SendedValue.Text = FormatSummaryValue(FormatFlagDate(_stage.IsSended, _stage.SendedAt));
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

        private static string FormatSummaryValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string FormatMoneyInput(decimal? value)
        {
            return value?.ToString("N2", CultureInfo.CurrentCulture) ?? string.Empty;
        }

        private static decimal? TryParseMoney(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var normalized = text.Trim().Replace(" ", string.Empty);
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out var currentCultureValue)
                ? currentCultureValue
                : decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantCultureValue)
                    ? invariantCultureValue
                    : null;
        }

        private void UpdateStageEditors()
        {
            _isApplyingBusinessLogic = true;
            try
            {
                ConfigureSelectCombo(_deadlineKindBox, DeadlineKindOptions(), _stage.DeadlineKind);
                ConfigureSelectCombo(_paymentDeadlineKindBox, PaymentDeadlineKindOptions(), _stage.PaymentDeadlineKind);
                ConfigureStatusDropdown(_statusBox, BuildStageStatusOptions(_statusOptions), _stage.Status.Id);

                _startAtEditor.Date = _stage.StartAt;
                _deadlineAtEditor.Date = _stage.DeadlineAt;
                _paymentDeadlineAtEditor.Date = _stage.PaymentDeadlineAt;
                _closedAtEditor.Date = _stage.ClosedAt;
                _durationBox.Text = _stage.Duration?.ToString() ?? string.Empty;
                _paymentDurationBox.Text = _stage.PaymentDuration?.ToString() ?? string.Empty;
                _costBox.Text = FormatMoneyInput(_stage.Cost);
            }
            finally
            {
                _isApplyingBusinessLogic = false;
            }

            ApplyBusinessLogicAfterFieldChange(applyInitialStart: true);
        }

        private void AttachBusinessLogicHandlers()
        {
            if (_businessLogicHandlersAttached)
            {
                return;
            }

            _businessLogicHandlersAttached = true;
            _deadlineKindBox.SelectionChanged += (_, _) => ApplyBusinessLogicAfterFieldChange(applyInitialStart: true);
            _paymentDeadlineKindBox.SelectionChanged += (_, _) => ApplyBusinessLogicAfterFieldChange(applyInitialStart: false);
            _statusBox.SelectionChanged += (_, _) => ApplyStatusBusinessLogic();
            _startAtEditor.DateChanged += (_, _) => ApplyBusinessLogicAfterFieldChange(applyInitialStart: false);
            _deadlineAtEditor.DateChanged += (_, _) =>
            {
                if (!_isApplyingBusinessLogic && StageDeadlineBusinessRules.IsDeadlineManualMode(GetSelectedDeadlineKind()))
                {
                    _deadlineAtEditedManually = true;
                }
            };
            _paymentDeadlineAtEditor.DateChanged += (_, _) =>
            {
                if (!_isApplyingBusinessLogic && StageDeadlineBusinessRules.IsPaymentDeadlineManualMode(GetSelectedPaymentDeadlineKind()))
                {
                    _paymentDeadlineAtEditedManually = true;
                }
            };
            _durationBox.TextChanged += (_, _) => ApplyBusinessLogicAfterFieldChange(applyInitialStart: false);
            _paymentDurationBox.TextChanged += (_, _) => ApplyBusinessLogicAfterFieldChange(applyInitialStart: false);
        }

        private void ApplyBusinessLogicAfterFieldChange(bool applyInitialStart)
        {
            if (_isApplyingBusinessLogic)
            {
                return;
            }

            _isApplyingBusinessLogic = true;
            try
            {
                if (applyInitialStart)
                {
                    ApplyInitialStartBusinessLogic();
                }

                UpdateDeadlineEditorState();
                ApplyDeadlineBusinessLogic();
                ApplyPaymentDeadlineBusinessLogic();
                ApplyStatusBusinessLogic();
            }
            finally
            {
                _isApplyingBusinessLogic = false;
            }
        }

        private void ApplyInitialStartBusinessLogic()
        {
            var deadlineKind = GetSelectedDeadlineKind();
            if (StageDeadlineBusinessRules.IsPaymentBasedDeadlineMode(deadlineKind))
            {
                _startAtEditor.Date = _stage.PaymentBaseDate;
                return;
            }

            if (_contract?.IsMultiStage == true || _startAtEditor.Date is not null)
            {
                return;
            }

            var nextStart = StageDeadlineBusinessRules.ResolveInitialStart(
                _contract?.IsMultiStage == true,
                _startAtEditor.Date,
                deadlineKind,
                _contract?.SignedAt,
                _stage.PaymentBaseDate);
            if (nextStart is null)
            {
                return;
            }

            _startAtEditor.Date = nextStart;
            if (GetSelectedStatusOption()?.Value is null)
            {
                SelectStatus(WorkflowStatusIds.InProgress);
            }
        }

        private void ApplyDeadlineBusinessLogic()
        {
            if (!StageDeadlineBusinessRules.IsDeadlineManualMode(GetSelectedDeadlineKind()))
            {
                _deadlineAtEditor.Date = StageDeadlineBusinessRules.CalculateDeadline(
                    GetSelectedDeadlineKind(),
                    _startAtEditor.Date,
                    TryGetInt(_durationBox.Text),
                    _holidays);
            }
        }

        private void ApplyPaymentDeadlineBusinessLogic()
        {
            var paymentKind = GetSelectedPaymentDeadlineKind();
            var paymentDuration = TryGetInt(_paymentDurationBox.Text);
            var fundedAt = _stage.FundedAt;
            var paymentDeadline = StageDeadlineBusinessRules.CalculatePaymentDeadline(
                paymentKind,
                fundedAt,
                paymentDuration,
                _holidays);

            if (paymentDeadline is not null)
            {
                _paymentDeadlineAtEditor.Date = paymentDeadline;
            }
            else if (StageDeadlineBusinessRules.ShouldClearPaymentDuration(paymentKind, paymentDuration))
            {
                if (!string.IsNullOrWhiteSpace(_paymentDurationBox.Text))
                {
                    _paymentDurationBox.Text = string.Empty;
                }
            }
            else if (paymentDuration is null
                && fundedAt is not null
                && _stage.Original.PaymentDeadlineAt is null)
            {
                _paymentDeadlineAtEditor.Date = null;
            }
        }

        private void UpdateDeadlineEditorState()
        {
            var deadlineManual = StageDeadlineBusinessRules.IsDeadlineManualMode(GetSelectedDeadlineKind());
            _startAtEditor.IsReadOnly = StageDeadlineBusinessRules.IsPaymentBasedDeadlineMode(GetSelectedDeadlineKind());
            _deadlineAtEditor.IsReadOnly = !deadlineManual;
            if (!deadlineManual)
            {
                _deadlineAtEditedManually = false;
            }

            var paymentDeadlineManual = StageDeadlineBusinessRules.IsPaymentDeadlineManualMode(GetSelectedPaymentDeadlineKind());
            _paymentDeadlineAtEditor.IsReadOnly = !paymentDeadlineManual;
            if (!paymentDeadlineManual)
            {
                _paymentDeadlineAtEditedManually = false;
            }
        }

        private void ApplyStatusBusinessLogic()
        {
            if (GetSelectedStatusOption()?.Value == WorkflowStatusIds.Closed && _closedAtEditor.Date is null)
            {
                _closedAtEditor.Date = DateTimeOffset.Now;
            }
        }

        private string? GetSelectedDeadlineKind()
        {
            return GetSelectedKey(_deadlineKindBox);
        }

        private string? GetSelectedPaymentDeadlineKind()
        {
            return GetSelectedKey(_paymentDeadlineKindBox);
        }

        private void SelectStatus(long statusId)
        {
            _statusBox.SelectedItem = (_statusBox.ItemsSource?.Cast<object>() ?? Enumerable.Empty<object>())
                .OfType<EnumSelectOption>()
                .FirstOrDefault(option => option.Value == statusId);
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
                ShowErrorInfo(FormatNavigationError("StageCommerEditDialog.RequestNavigation", ex));
            }
        }

        private void ApplyNavigationResult(StageEditDialogNavigationResult result)
        {
            var oldStage = _stage;
            var oldContract = _contract;
            var oldNavigationState = _navigationState;

            try
            {
                _stage = result.Stage;
                _contract = result.Contract;
                _navigationState = result.NavigationState;
                ResetTaskSelection();
                RenderStageContent("StageCommerEditDialog.ApplyNavigationResult.apply");
            }
            catch (Exception ex)
            {
                _stage = oldStage;
                _contract = oldContract;
                _navigationState = oldNavigationState;
                ResetTaskSelection();
                try
                {
                    RenderStageContent("StageCommerEditDialog.ApplyNavigationResult.restore");
                }
                catch (Exception restoreEx)
                {
                    throw new InvalidOperationException(
                        FormatNavigationError("StageCommerEditDialog.ApplyNavigationResult.restore", restoreEx),
                        restoreEx);
                }

                throw new InvalidOperationException(
                    FormatNavigationError("StageCommerEditDialog.ApplyNavigationResult.apply", ex),
                    ex);
            }
        }

        private void RenderStageContent(string location = "StageCommerEditDialog.RenderStageContent")
        {
            UpdateStageSummaryPanel();
            RenderStageEditors(location);
        }

        private void RenderStageEditors(string location)
        {
            UpdateStageEditors();
        }

        private void StageNavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: StageEditDialogNavigationDirection direction })
            {
                RequestNavigation(direction);
            }
        }

        private static string FormatNavigationError(string location, Exception ex)
        {
            return $"{location}: {ex.Message}";
        }

        private UIElement BuildTasksMultiSelectEditor()
        {
            return StageContractTaskDialogControls.ConfigureTasksMultiSelect(
                _tasksMultiSelect,
                _taskOptions,
                _selectedTaskKindIds,
                OnTaskSelectionChanged);
        }

        private void OnTaskSelectionChanged(object? sender, MultiSelectChangedEventArgs e)
        {
            StageContractTaskDialogControls.UpdateSelectedTaskKindIds(_selectedTaskKindIds, e);
        }

        private void ResetTaskSelection()
        {
            _selectedTaskKindIds.Clear();
            _originalTasks = _stage.Tasks
                .Select(static task => new StageTaskRecord(task.Id, task.ListKey, task.TaskKindId, task.Name ?? string.Empty))
                .ToList();
            _taskOptions = StageContractTaskDialogControls.CreateTaskOptions(_taskKindItems, _originalTasks);
            foreach (var option in _taskOptions.Where(static option => option.IsSelected))
            {
                _selectedTaskKindIds.Add(option.TaskKindId);
            }
        }

        private EnumSelectOption? GetSelectedStatusOption()
        {
            return StageContractStatusDialogControls.GetSelectedStatusOption(_statusBox);
        }

    }
}
