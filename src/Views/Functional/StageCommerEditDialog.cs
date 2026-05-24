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
using static CbsContractsDesktopClient.Shared.Dates.BusinessCalendar;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pauli.WinUiKit.Controls;

namespace CbsContractsDesktopClient.Views.Functional
{
    public sealed class StageCommerEditDialog : AppEditDialog
    {
        private const long StatusPending = 2;
        private const long StatusClosed = 5;

        private readonly StageEditState _stage;
        private readonly ContractEditState? _contract;
        private readonly IHolidayRecalculationService _holidayRecalculationService;
        private readonly IReadOnlyList<CbsTableFilterOptionDefinition> _statusOptions;
        private readonly IReadOnlyList<StageTaskOption> _taskOptions;
        private readonly List<StageTaskRecord> _originalTasks;
        private IReadOnlyList<HolidayCalendarDay> _holidays = [];
        private readonly int? _profileId;
        private readonly CalendarInput _startAtEditor = new();
        private readonly CalendarInput _deadlineAtEditor = new();
        private readonly CalendarInput _paymentDeadlineAtEditor = new();
        private readonly CalendarInput _closedAtEditor = new();
        private readonly TextBox _durationBox = BuildNumberTextBox();
        private readonly TextBox _paymentDurationBox = BuildNumberTextBox();
        private readonly ComboBox _deadlineKindBox = new();
        private readonly ComboBox _paymentDeadlineKindBox = new();
        private readonly ComboBox _statusBox = new();
        private readonly MultiSelect _tasksMultiSelect = new();
        private readonly HashSet<long> _selectedTaskKindIds = [];
        private readonly TextBox _commentBox = new();
        private readonly string? _listKey;
        private bool _isApplyingBusinessLogic;
        private bool _businessLogicHandlersAttached;
        private bool _deadlineAtEditedManually;
        private bool _paymentDeadlineAtEditedManually;

        public StageCommerEditDialog(
            StageEditState stage,
            ContractEditState? contract,
            IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions,
            IReadOnlyList<ReferenceLookupItem> taskKindItems,
            int? profileId)
        {
            ArgumentNullException.ThrowIfNull(stage);
            ArgumentNullException.ThrowIfNull(statusOptions);
            ArgumentNullException.ThrowIfNull(taskKindItems);

            _stage = stage;
            _contract = contract;
            _holidayRecalculationService = App.Services.GetRequiredService<IHolidayRecalculationService>();
            _statusOptions = statusOptions;
            _profileId = profileId;
            Id = stage.Id;
            _listKey = stage.ListKey;
            _originalTasks = stage.Tasks
                .Select(static task => new StageTaskRecord(task.Id, task.ListKey, task.TaskKindId, task.Name ?? string.Empty))
                .ToList();
            _taskOptions = CreateTaskOptions(taskKindItems, _originalTasks);
            foreach (var option in _taskOptions.Where(static option => option.IsSelected))
            {
                _selectedTaskKindIds.Add(option.TaskKindId);
            }

            Resources["ContentDialogMinWidth"] = 780d;
            Resources["ContentDialogMaxWidth"] = 980d;
            Content = BuildContent(statusOptions);
            DialogChrome.Apply(this, _stage.GetEditDialogTitle());
            Loaded += StageCommerEditDialog_Loaded;
        }

        public long Id { get; }

        public bool ShouldCloseContract()
        {
            SyncStageStateFromEditors();
            return _stage.ShouldCloseContract(_contract, StatusClosed);
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

        public IReadOnlyDictionary<string, object?> BuildTablePatch()
        {
            var patch = new Dictionary<string, object?>(BuildPayload(), StringComparer.OrdinalIgnoreCase);

            patch.Remove("comments_attributes");
            patch.Remove("tasks_attributes");

            var selectedStatus = GetSelectedStatusOption();
            if (patch.ContainsKey("status_id") && selectedStatus?.Value is long statusId)
            {
                patch["status"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = statusId,
                    ["name"] = selectedStatus.Label
                };
            }

            var tasks = BuildSelectedTaskReadModels();
            if (tasks is not null)
            {
                patch["tasks"] = tasks;
            }

            return patch;
        }

        public override bool Validate()
        {
            if (_deadlineAtEditedManually
                && IsDeadlineManualMode(GetSelectedDeadlineKind())
                && _deadlineAtEditor.Date is DateTimeOffset deadlineAt
                && _startAtEditor.Date is DateTimeOffset startAt
                && deadlineAt.Date < startAt.Date)
            {
                ShowErrorInfo("Срок выполнения не может быть раньше даты начала этапа.");
                return false;
            }

            if (GetSelectedKey(_paymentDeadlineKindBox) is string paymentKind
                && paymentKind != "c_plan"
                && paymentKind.Length > 0
                && TryGetInt(_paymentDurationBox.Text) is null)
            {
                ShowErrorInfo("Для выбранного режима оплаты укажите срок в днях.");
                return false;
            }

            if (GetSelectedKey(_paymentDeadlineKindBox) == "c_plan"
                && _paymentDeadlineAtEditor.Date is null)
            {
                ShowErrorInfo("Для календарного плана укажите срок оплаты.");
                return false;
            }

            var fundedAt = _stage.FundedAt;
            if (_paymentDeadlineAtEditedManually
                && IsPaymentDeadlineManualMode(GetSelectedPaymentDeadlineKind())
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
            _stage.PaymentDeadlineKind = GetSelectedKey(_paymentDeadlineKindBox);
            _stage.PaymentDeadlineAt = _paymentDeadlineAtEditor.Date;
            _stage.Duration = TryGetInt(_durationBox.Text);
            _stage.PaymentDuration = TryGetInt(_paymentDurationBox.Text);
            _stage.ClosedAt = _closedAtEditor.Date;
        }

        private UIElement BuildContent(IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions)
        {
            var root = new Grid
            {
                MinWidth = 740,
                MaxWidth = 940
            };

            var scrollViewer = new ScrollViewer
            {
                MaxHeight = 640,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var stack = new StackPanel
            {
                Spacing = 14
            };
            scrollViewer.Content = stack;

            stack.Children.Add(BuildSummaryPanel());
            stack.Children.Add(BuildEditorsArea(statusOptions));

            root.Children.Add(scrollViewer);
            return BuildEditContent(root);
        }

        private UIElement BuildSummaryPanel()
        {
            var stack = new StackPanel
            {
                Spacing = 12
            };

            stack.Children.Add(BuildDialogSectionTitle(RequireContract().GetSectionTitle()));

            var contractGrid = new Grid
            {
                ColumnSpacing = 18,
                RowSpacing = 6
            };
            contractGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contractGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var left = new StackPanel { Spacing = 6 };
            left.Children.Add(BuildSummaryLine("Внешний номер", _contract?.ExternalNumber ?? string.Empty));
            left.Children.Add(BuildSummaryLine("Контрагент", _contract?.ContragentName ?? string.Empty));
            left.Children.Add(BuildAccentSummaryLine("Стоимость", FormatMoney(_contract?.Cost)));

            var right = new StackPanel { Spacing = 6 };
            right.Children.Add(BuildSummaryElement("Статус", BuildStatusBadge(
                RequireContract().Status.Name!,
                RequireContract().Status.Id,
                horizontalAlignment: HorizontalAlignment.Left)));
            right.Children.Add(BuildSummaryLine("Дата подписания", FormatDisplayDate(_contract?.SignedAt)));
            right.Children.Add(BuildSummaryLine("Госконтракт", FormatBoolean(_contract?.Governmental)));

            contractGrid.Children.Add(left);
            Grid.SetColumn(right, 1);
            contractGrid.Children.Add(right);
            stack.Children.Add(contractGrid);

            stack.Children.Add(BuildDialogSectionTitle(
                _stage.GetSectionTitle(_contract),
                _stage.GetSectionTitleAmount(_contract)));

            var stageGrid = new Grid
            {
                ColumnSpacing = 18,
                RowSpacing = 6
            };
            stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var stageLeft = new StackPanel { Spacing = 6 };
            stageLeft.Children.Add(BuildSummaryLine("Предоплата", FormatDisplayDate(_stage.PrepaymentAt)));
            stageLeft.Children.Add(BuildSummaryLine("Оплата", FormatDisplayDate(_stage.PaymentAt)));

            var stageMiddle = new StackPanel { Spacing = 6 };
            stageMiddle.Children.Add(BuildSummaryLine("Бух. закрытие", FormatDisplayDate(_stage.FundedAt)));
            stageMiddle.Children.Add(BuildSummaryLine("Работа выполнена", FormatDisplayDate(_stage.CompletedAt)));

            var stageRight = new StackPanel { Spacing = 6 };
            stageRight.Children.Add(BuildSummaryLine("Выезд", FormatFlagDate(_stage.IsRideOut, _stage.RideOutAt)));
            stageRight.Children.Add(BuildSummaryLine("Отправка", FormatFlagDate(_stage.IsSended, _stage.SendedAt)));

            stageGrid.Children.Add(stageLeft);
            Grid.SetColumn(stageMiddle, 1);
            stageGrid.Children.Add(stageMiddle);
            Grid.SetColumn(stageRight, 2);
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

        private UIElement BuildEditorsArea(IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions)
        {
            var stack = new StackPanel
            {
                Spacing = 12
            };

            stack.Children.Add(BuildEditorsGrid(statusOptions));
            stack.Children.Add(BuildWideEditorsColumn());
            return stack;
        }

        private UIElement BuildEditorsGrid(IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions)
        {
            var grid = new Grid
            {
                ColumnSpacing = 18,
                RowSpacing = 12
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _startAtEditor.Date = _stage.StartAt;
            _deadlineAtEditor.Date = _stage.DeadlineAt;
            _paymentDeadlineAtEditor.Date = _stage.PaymentDeadlineAt;
            _closedAtEditor.Date = _stage.ClosedAt;
            _durationBox.Text = _stage.Duration?.ToString() ?? string.Empty;
            _paymentDurationBox.Text = _stage.PaymentDuration?.ToString() ?? string.Empty;

            ConfigureSelectCombo(_deadlineKindBox, DeadlineKindOptions(), _stage.DeadlineKind);
            ConfigureSelectCombo(_paymentDeadlineKindBox, PaymentDeadlineKindOptions(), _stage.PaymentDeadlineKind);
            ConfigureStatusCombo(_statusBox, BuildStageStatusOptions(statusOptions), _stage.Status.Id);

            AttachBusinessLogicHandlers();
            ApplyBusinessLogicAfterFieldChange(applyInitialStart: true);

            var left = new StackPanel { Spacing = 10 };
            left.Children.Add(BuildSectionTitle("Срок выполнения"));
            left.Children.Add(BuildLabeledControl("Режим срока", _deadlineKindBox));
            left.Children.Add(BuildLabeledControl("Дней", _durationBox));
            left.Children.Add(BuildLabeledControl("Срок выполнения", _deadlineAtEditor));

            var middle = new StackPanel { Spacing = 10 };
            middle.Children.Add(BuildSectionTitle("Оплата"));
            middle.Children.Add(BuildLabeledControl("Режим оплаты", _paymentDeadlineKindBox));
            middle.Children.Add(BuildLabeledControl("Дней", _paymentDurationBox));
            middle.Children.Add(BuildLabeledControl("Срок оплаты", _paymentDeadlineAtEditor));

            var right = new StackPanel { Spacing = 10 };
            right.Children.Add(BuildSectionTitle("Состояние"));
            right.Children.Add(BuildLabeledControl("Статус этапа", _statusBox));
            right.Children.Add(BuildLabeledControl("Дата начала", _startAtEditor));
            right.Children.Add(BuildLabeledControl("Закрыт", _closedAtEditor));

            var deadlinePanel = BuildEditorGroupPanel(left, DialogEditorGroupTone.Neutral);
            var paymentPanel = BuildEditorGroupPanel(middle, DialogEditorGroupTone.Accent);
            var statePanel = BuildEditorGroupPanel(right, DialogEditorGroupTone.Muted);

            grid.Children.Add(deadlinePanel);
            Grid.SetColumn(paymentPanel, 1);
            grid.Children.Add(paymentPanel);
            Grid.SetColumn(statePanel, 2);
            grid.Children.Add(statePanel);
            return grid;
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
                if (!_isApplyingBusinessLogic && IsDeadlineManualMode(GetSelectedDeadlineKind()))
                {
                    _deadlineAtEditedManually = true;
                }
            };
            _paymentDeadlineAtEditor.DateChanged += (_, _) =>
            {
                if (!_isApplyingBusinessLogic && IsPaymentDeadlineManualMode(GetSelectedPaymentDeadlineKind()))
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
            if (_contract?.IsMultiStage == true || _startAtEditor.Date is not null)
            {
                return;
            }

            var deadlineKind = GetSelectedDeadlineKind();
            DateTimeOffset? nextStart = null;

            if (deadlineKind is "calendar_plan" or "calendar_days" or "working_days")
            {
                nextStart = _contract?.SignedAt;
            }
            else if (deadlineKind is "calendar_prepayment" or "working_prepayment")
            {
                nextStart = _stage.PaymentBaseDate;
            }

            if (nextStart is null)
            {
                return;
            }

            _startAtEditor.Date = nextStart;
            if (GetSelectedStatusOption()?.Value is null)
            {
                SelectStatus(StatusPending);
            }
        }

        private void ApplyDeadlineBusinessLogic()
        {
            var deadlineKind = GetSelectedDeadlineKind();
            var startAt = _startAtEditor.Date;
            var duration = TryGetInt(_durationBox.Text);

            if (deadlineKind == "calendar_days" && duration is int calendarDuration && startAt is not null)
            {
                _deadlineAtEditor.Date = startAt.Value.Date.AddDays(calendarDuration);
            }
            else if (deadlineKind == "working_days" && duration is int workingDuration && startAt is not null)
            {
                _deadlineAtEditor.Date = AddWorkingDaysToDate(startAt.Value, workingDuration, _holidays);
            }
            else if ((duration is null || startAt is null) && deadlineKind != "calendar_plan")
            {
                _deadlineAtEditor.Date = null;
            }
        }

        private void ApplyPaymentDeadlineBusinessLogic()
        {
            var paymentKind = GetSelectedPaymentDeadlineKind();
            var paymentDuration = TryGetInt(_paymentDurationBox.Text);
            var fundedAt = _stage.FundedAt;

            if (paymentKind == "c_days" && paymentDuration is int calendarDuration && fundedAt is not null)
            {
                _paymentDeadlineAtEditor.Date = fundedAt.Value.Date.AddDays(calendarDuration);
            }
            else if (paymentKind == "w_days" && paymentDuration is int workingDuration && fundedAt is not null)
            {
                _paymentDeadlineAtEditor.Date = AddWorkingDaysToDate(fundedAt.Value, workingDuration, _holidays);
            }
            else if (string.IsNullOrWhiteSpace(paymentKind) || paymentKind == "c_plan")
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
            var deadlineManual = IsDeadlineManualMode(GetSelectedDeadlineKind());
            _deadlineAtEditor.IsReadOnly = !deadlineManual;
            if (!deadlineManual)
            {
                _deadlineAtEditedManually = false;
            }

            var paymentDeadlineManual = IsPaymentDeadlineManualMode(GetSelectedPaymentDeadlineKind());
            _paymentDeadlineAtEditor.IsReadOnly = !paymentDeadlineManual;
            if (!paymentDeadlineManual)
            {
                _paymentDeadlineAtEditedManually = false;
            }
        }

        private void ApplyStatusBusinessLogic()
        {
            if (GetSelectedStatusOption()?.Value == StatusClosed && _closedAtEditor.Date is null)
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

        private static bool IsDeadlineManualMode(string? deadlineKind)
        {
            return string.Equals(deadlineKind, "calendar_plan", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPaymentDeadlineManualMode(string? paymentDeadlineKind)
        {
            return string.Equals(paymentDeadlineKind, "c_plan", StringComparison.OrdinalIgnoreCase);
        }

        private void SelectStatus(long statusId)
        {
            foreach (var item in _statusBox.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag is EnumSelectOption option && option.Value == statusId)
                {
                    _statusBox.SelectedItem = item;
                    return;
                }
            }
        }

        private ContractEditState RequireContract()
        {
            return _contract
                ?? throw new InvalidOperationException("Stage edit dialog requires selected contract edit state.");
        }

        private UIElement BuildWideEditorsColumn()
        {
            var stack = new StackPanel { Spacing = 10 };

            stack.Children.Add(BuildLabeledControl("Прочие задачи", BuildTasksMultiSelectEditor()));

            _commentBox.PlaceholderText = _profileId is null
                ? "Комментарий недоступен: не получен profile_id пользователя"
                : "Комментарий";
            _commentBox.IsEnabled = _profileId is not null;
            stack.Children.Add(BuildLabeledControl("Комментарий", _commentBox));

            return stack;
        }

        private UIElement BuildTasksMultiSelectEditor()
        {
            _tasksMultiSelect.Options = _taskOptions;
            _tasksMultiSelect.Value = _taskOptions
                .Where(option => _selectedTaskKindIds.Contains(option.TaskKindId))
                .ToList();
            _tasksMultiSelect.Display = "chip";
            _tasksMultiSelect.MaxSelectedLabels = 4;
            _tasksMultiSelect.Placeholder = "Выбрать";
            _tasksMultiSelect.Tooltip = "Прочие задачи";
            _tasksMultiSelect.SelectionChanged += OnTaskSelectionChanged;
            return _tasksMultiSelect;
        }

        private void OnTaskSelectionChanged(object? sender, MultiSelectChangedEventArgs e)
        {
            _selectedTaskKindIds.Clear();
            foreach (var option in e.Value.OfType<StageTaskOption>())
            {
                _selectedTaskKindIds.Add(option.TaskKindId);
            }
        }

        private IReadOnlyList<Dictionary<string, object?>>? BuildSelectedTaskReadModels()
        {
            var originalKinds = _originalTasks
                .Select(static item => item.TaskKindId)
                .Where(static id => id is not null)
                .Select(static id => id!.Value)
                .ToHashSet();
            if (_selectedTaskKindIds.SetEquals(originalKinds))
            {
                return null;
            }

            return _taskOptions
                .Where(option => _selectedTaskKindIds.Contains(option.TaskKindId))
                .Select(option => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["task_kind_id"] = option.TaskKindId,
                    ["name"] = option.Name
                })
                .ToList();
        }

        private static IReadOnlyList<StageTaskOption> CreateTaskOptions(
            IReadOnlyList<ReferenceLookupItem> taskKindItems,
            IReadOnlyList<StageTaskRecord> selectedTasks)
        {
            var selectedKinds = selectedTasks
                .Select(static item => item.TaskKindId)
                .Where(static id => id is not null)
                .Select(static id => id!.Value)
                .ToHashSet();

            return taskKindItems
                .Where(static item => string.IsNullOrWhiteSpace(item.Code))
                .Select(item => new StageTaskOption(
                    TaskKindId: TryGetLong(item.Id) ?? 0,
                    Name: item.DisplayName,
                    IsSelected: TryGetLong(item.Id) is long id && selectedKinds.Contains(id)))
                .Where(static item => item.TaskKindId > 0 && !string.IsNullOrWhiteSpace(item.Name))
                .OrderBy(static item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private EnumSelectOption? GetSelectedStatusOption()
        {
            return StageContractStatusDialogControls.GetSelectedStatusOption(_statusBox);
        }

        private sealed record StageTaskOption(long TaskKindId, string Name, bool IsSelected);

        private sealed record StageTaskRecord(long? Id, string? ListKey, long? TaskKindId, string Name);

    }
}
