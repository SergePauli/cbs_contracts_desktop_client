using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pauli.WinUiKit.Controls;
using Windows.UI;

namespace CbsContractsDesktopClient.Views.Functional
{
    public sealed class StageCommerEditDialog : ContentDialog
    {
        private const long StatusPending = 2;
        private const long StatusClosed = 5;

        private readonly ReferenceDataRow _sourceRow;
        private readonly ReferenceDataRow _displayRow;
        private readonly ReferenceDataRow? _contractRow;
        private readonly IReadOnlyList<CbsTableFilterOptionDefinition> _statusOptions;
        private readonly IReadOnlyList<StageTaskOption> _taskOptions;
        private readonly List<StageTaskRecord> _originalTasks;
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
        private readonly TextBlock _errorText = new();
        private readonly string? _listKey;
        private bool _isApplyingBusinessLogic;
        private bool _businessLogicHandlersAttached;

        public StageCommerEditDialog(
            ReferenceDataRow sourceRow,
            ReferenceDataRow? displayRow,
            ReferenceDataRow? contractRow,
            IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions,
            IReadOnlyList<ReferenceLookupItem> taskKindItems,
            int? profileId)
        {
            ArgumentNullException.ThrowIfNull(sourceRow);
            ArgumentNullException.ThrowIfNull(statusOptions);
            ArgumentNullException.ThrowIfNull(taskKindItems);

            _sourceRow = sourceRow;
            _displayRow = displayRow ?? sourceRow;
            _contractRow = contractRow;
            _statusOptions = statusOptions;
            _profileId = profileId;
            Id = TryGetLong(sourceRow.GetValue("id"))
                ?? throw new InvalidOperationException("У выбранного этапа отсутствует ID.");
            _listKey = sourceRow.GetValue("list_key")?.ToString();
            _originalTasks = ReadStageTasks(sourceRow).ToList();
            _taskOptions = CreateTaskOptions(taskKindItems, _originalTasks);
            foreach (var option in _taskOptions.Where(static option => option.IsSelected))
            {
                _selectedTaskKindIds.Add(option.TaskKindId);
            }

            PrimaryButtonText = "Сохранить";
            CloseButtonText = "Отмена";
            DefaultButton = ContentDialogButton.Primary;
            Resources["ContentDialogMinWidth"] = 780d;
            Resources["ContentDialogMaxWidth"] = 980d;
            Content = BuildContent(statusOptions);
            DialogChrome.Apply(this, BuildTitleText(_displayRow));
        }

        public long Id { get; }

        public IReadOnlyDictionary<string, object?> BuildPayload()
        {
            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = Id,
                ["status_id"] = GetSelectedStatusOption()?.Value,
                ["deadline_kind"] = (_deadlineKindBox.SelectedItem as StageSelectOption)?.Key,
                ["deadline_at"] = FormatDate(_deadlineAtEditor.Date),
                ["start_at"] = FormatDate(_startAtEditor.Date),
                ["payment_deadline_kind"] = (_paymentDeadlineKindBox.SelectedItem as StageSelectOption)?.Key,
                ["payment_deadline_at"] = FormatDate(_paymentDeadlineAtEditor.Date),
                ["duration"] = TryGetInt(_durationBox.Text),
                ["payment_duration"] = TryGetInt(_paymentDurationBox.Text),
                ["closed_at"] = FormatDate(_closedAtEditor.Date)
            };

            if (!string.IsNullOrWhiteSpace(_listKey))
            {
                payload["list_key"] = _listKey;
            }

            var tasksDelta = BuildTaskAttributesDelta();
            if (tasksDelta.Count > 0)
            {
                payload["tasks_attributes"] = tasksDelta;
            }

            var comment = _commentBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(comment) && _profileId is int profileId)
            {
                payload["comments_attributes"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["content"] = comment,
                        ["profile_id"] = profileId
                    }
                };
            }

            return payload;
        }

        public bool Validate()
        {
            if ((_paymentDeadlineKindBox.SelectedItem as StageSelectOption)?.Key is string paymentKind
                && paymentKind != "c_plan"
                && paymentKind.Length > 0
                && TryGetInt(_paymentDurationBox.Text) is null)
            {
                ShowErrorInfo("Для выбранного режима оплаты укажите срок в днях.");
                return false;
            }

            if ((_paymentDeadlineKindBox.SelectedItem as StageSelectOption)?.Key == "c_plan"
                && _paymentDeadlineAtEditor.Date is null)
            {
                ShowErrorInfo("Для календарного плана укажите срок оплаты.");
                return false;
            }

            ShowErrorInfo(string.Empty);
            return true;
        }

        public void ShowErrorInfo(string message)
        {
            _errorText.Text = message;
            _errorText.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
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

            _errorText.Foreground = new SolidColorBrush(Colors.IndianRed);
            _errorText.TextWrapping = TextWrapping.Wrap;
            _errorText.Visibility = Visibility.Collapsed;
            stack.Children.Add(_errorText);

            root.Children.Add(scrollViewer);
            return root;
        }

        private UIElement BuildSummaryPanel()
        {
            var stack = new StackPanel
            {
                Spacing = 12
            };

            stack.Children.Add(BuildSectionTitle("Контракт"));

            var contractGrid = new Grid
            {
                ColumnSpacing = 18,
                RowSpacing = 6
            };
            contractGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contractGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var left = new StackPanel { Spacing = 6 };
            left.Children.Add(BuildSummaryLine("Номер", GetDisplayText("contract.name", "contract.id")));
            left.Children.Add(BuildSummaryLine("Внешний номер", GetDisplayText("contract.external_number")));
            left.Children.Add(BuildSummaryLine("Контрагент", GetDisplayText("contract.contragent.name", "contragent.name")));
            left.Children.Add(BuildSummaryLine("Стоимость", FormatMoney(_displayRow.GetValue("contract.cost") ?? _sourceRow.GetValue("contract.cost") ?? _sourceRow.GetValue("cost"))));

            var right = new StackPanel { Spacing = 6 };
            right.Children.Add(BuildSummaryElement("Статус", BuildStatusBadge(
                ResolveContractStatusName(),
                ResolveContractStatusId(),
                horizontalAlignment: HorizontalAlignment.Left)));
            right.Children.Add(BuildSummaryLine("Дата подписания", FormatDisplayDate(_displayRow.GetValue("contract.signed_at") ?? _sourceRow.GetValue("contract.signed_at"))));
            right.Children.Add(BuildSummaryLine("Госконтракт", FormatBoolean(_displayRow.GetValue("contract.governmental") ?? _sourceRow.GetValue("contract.governmental"))));
            right.Children.Add(BuildSummaryLine("Основная работа", GetDisplayText("contract.task_kind.name", "task_kind.name")));

            contractGrid.Children.Add(left);
            Grid.SetColumn(right, 1);
            contractGrid.Children.Add(right);
            stack.Children.Add(contractGrid);

            stack.Children.Add(BuildSectionTitle("Этап"));

            var stageGrid = new Grid
            {
                ColumnSpacing = 18,
                RowSpacing = 6
            };
            stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var stageLeft = new StackPanel { Spacing = 6 };
            stageLeft.Children.Add(BuildSummaryLine("Стоимость", FormatMoney(_sourceRow.GetValue("cost"))));
            stageLeft.Children.Add(BuildSummaryLine("Предоплата", FormatDisplayDate(_sourceRow.GetValue("prepayment_at"))));
            stageLeft.Children.Add(BuildSummaryLine("Оплата", FormatDisplayDate(_sourceRow.GetValue("payment_at"))));

            var stageMiddle = new StackPanel { Spacing = 6 };
            stageMiddle.Children.Add(BuildSummaryLine("Бух. закрытие", FormatDisplayDate(_sourceRow.GetValue("funded_at"))));
            stageMiddle.Children.Add(BuildSummaryLine("Работа выполнена", FormatDisplayDate(_sourceRow.GetValue("completed_at"))));
            stageMiddle.Children.Add(BuildSummaryLine("Счёт", FormatDisplayDate(_sourceRow.GetValue("invoice_at"))));

            var stageRight = new StackPanel { Spacing = 6 };
            stageRight.Children.Add(BuildSummaryLine("Выезд", FormatFlagDate(_sourceRow.GetValue("is_ride_out"), _sourceRow.GetValue("ride_out_at"))));
            stageRight.Children.Add(BuildSummaryLine("Отправка", FormatFlagDate(_sourceRow.GetValue("is_sended"), _sourceRow.GetValue("sended_at"))));
            stageRight.Children.Add(BuildSummaryLine("Бух. закрыт", FormatBoolean(_sourceRow.GetValue("is_funded"))));

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

            _startAtEditor.Date = ParseDate(_sourceRow.GetValue("start_at"));
            _deadlineAtEditor.Date = ParseDate(_sourceRow.GetValue("deadline_at"));
            _paymentDeadlineAtEditor.Date = ParseDate(_sourceRow.GetValue("payment_deadline_at"));
            _closedAtEditor.Date = ParseDate(_sourceRow.GetValue("closed_at"));
            _durationBox.Text = _sourceRow.GetValue("duration")?.ToString() ?? string.Empty;
            _paymentDurationBox.Text = _sourceRow.GetValue("payment_duration")?.ToString() ?? string.Empty;

            ConfigureCombo(_deadlineKindBox, DeadlineKindOptions(), _sourceRow.GetValue("deadline_kind")?.ToString());
            ConfigureCombo(_paymentDeadlineKindBox, PaymentDeadlineKindOptions(), _sourceRow.GetValue("payment_deadline_kind")?.ToString());
            ConfigureStatusCombo(_statusBox, BuildStatusOptions(statusOptions), TryGetLong(_sourceRow.GetValue("status.id") ?? _sourceRow.GetValue("status_id")));

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

            var deadlinePanel = BuildEditorGroupPanel(left, EditorGroupTone.Neutral);
            var paymentPanel = BuildEditorGroupPanel(middle, EditorGroupTone.Accent);
            var statePanel = BuildEditorGroupPanel(right, EditorGroupTone.Muted);

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
            if (IsMultiStageContract() || _startAtEditor.Date is not null)
            {
                return;
            }

            var deadlineKind = GetSelectedDeadlineKind();
            DateTimeOffset? nextStart = null;

            if (deadlineKind is "calendar_plan" or "calendar_days" or "working_days")
            {
                nextStart = ParseDate(
                    _contractRow?.GetValue("signed_at")
                    ?? _displayRow.GetValue("contract.signed_at")
                    ?? _sourceRow.GetValue("contract.signed_at"));
            }
            else if (deadlineKind is "calendar_prepayment" or "working_prepayment")
            {
                nextStart = GetPaymentBaseDate();
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
                _deadlineAtEditor.Date = AddWorkingDaysToDate(startAt.Value.Date, workingDuration);
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
            var fundedAt = ParseDate(_sourceRow.GetValue("funded_at"));

            if (paymentKind == "c_days" && paymentDuration is int calendarDuration && fundedAt is not null)
            {
                _paymentDeadlineAtEditor.Date = fundedAt.Value.Date.AddDays(calendarDuration);
            }
            else if (paymentKind == "w_days" && paymentDuration is int workingDuration && fundedAt is not null)
            {
                _paymentDeadlineAtEditor.Date = AddWorkingDaysToDate(fundedAt.Value.Date, workingDuration);
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
                && string.IsNullOrWhiteSpace(_sourceRow.GetValue("payment_deadline_at")?.ToString()))
            {
                _paymentDeadlineAtEditor.Date = null;
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
            return (_deadlineKindBox.SelectedItem as StageSelectOption)?.Key;
        }

        private string? GetSelectedPaymentDeadlineKind()
        {
            return (_paymentDeadlineKindBox.SelectedItem as StageSelectOption)?.Key;
        }

        private void SelectStatus(long statusId)
        {
            foreach (var item in _statusBox.Items.OfType<ComboBoxItem>())
            {
                if (item.Tag is StageSelectOption option && option.Value == statusId)
                {
                    _statusBox.SelectedItem = item;
                    return;
                }
            }
        }

        private bool IsMultiStageContract()
        {
            if (TryGetBool(_contractRow?.GetValue("multyStage"))
                ?? TryGetBool(_contractRow?.GetValue("multiStage"))
                ?? TryGetBool(_contractRow?.GetValue("is_multistage"))
                ?? TryGetBool(_displayRow.GetValue("contract.multyStage"))
                ?? TryGetBool(_displayRow.GetValue("contract.multiStage"))
                ?? TryGetBool(_sourceRow.GetValue("contract.multyStage"))
                ?? TryGetBool(_sourceRow.GetValue("contract.multiStage"))
                ?? false)
            {
                return true;
            }

            return TryGetArrayCount(_contractRow, "stages") is int contractStagesCount && contractStagesCount > 1;
        }

        private DateTimeOffset? GetPaymentBaseDate()
        {
            return ParseDate(_sourceRow.GetValue("prepayment_at"))
                ?? ParseDate(_sourceRow.GetValue("payment_at"));
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

        private IReadOnlyList<Dictionary<string, object?>> BuildTaskAttributesDelta()
        {
            var selectedKinds = _selectedTaskKindIds.ToHashSet();

            var originalKinds = _originalTasks
                .Select(static item => item.TaskKindId)
                .Where(static id => id is not null)
                .Select(static id => id!.Value)
                .ToHashSet();

            var added = selectedKinds
                .Where(kind => !originalKinds.Contains(kind))
                .Select(kind => new Dictionary<string, object?>
                {
                    ["list_key"] = Guid.NewGuid().ToString(),
                    ["task_kind_id"] = kind
                });

            var removed = _originalTasks
                .Where(task => task.TaskKindId is long kind && !selectedKinds.Contains(kind))
                .Select(task =>
                {
                    var payload = new Dictionary<string, object?>
                    {
                        ["_destroy"] = "1"
                    };
                    if (task.Id is not null)
                    {
                        payload["id"] = task.Id;
                    }

                    if (!string.IsNullOrWhiteSpace(task.ListKey))
                    {
                        payload["list_key"] = task.ListKey;
                    }

                    return payload;
                });

            return added.Concat(removed).ToList();
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

        private static IReadOnlyList<StageTaskRecord> ReadStageTasks(ReferenceDataRow row)
        {
            if (!row.Values.TryGetValue("tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return tasks.EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.Object)
                .Select(static item => new StageTaskRecord(
                    Id: TryGetJsonLong(item, "id"),
                    ListKey: TryGetJsonString(item, "list_key"),
                    TaskKindId: TryGetJsonLong(item, "task_kind_id"),
                    Name: TryGetJsonString(item, "name") ?? string.Empty))
                .ToList();
        }

        private static IReadOnlyList<StageSelectOption> DeadlineKindOptions()
        {
            return
            [
                new("calendar_plan", "Календарный план", null),
                new("calendar_days", "Календарные дни", null),
                new("calendar_prepayment", "Календарные от предоплаты", null),
                new("working_days", "Рабочие дни", null),
                new("working_prepayment", "Рабочие от предоплаты", null)
            ];
        }

        private static IReadOnlyList<StageSelectOption> PaymentDeadlineKindOptions()
        {
            return
            [
                new(null, "Не задан", null),
                new("c_plan", "Календарный план", null),
                new("c_days", "Календарные дни", null),
                new("w_days", "Рабочие дни", null)
            ];
        }

        private static IReadOnlyList<StageSelectOption> BuildStatusOptions(IReadOnlyList<CbsTableFilterOptionDefinition> options)
        {
            var allowed = new HashSet<long> { 2, 4, 5, 6, 7 };
            var result = new List<StageSelectOption>
            {
                new(null, "Пустой", null)
            };

            result.AddRange(options
                .Select(option => new StageSelectOption(null, option.Label, TryGetLong(option.Value)))
                .Where(option => option.Value is long id && allowed.Contains(id))
                .OrderBy(option => option.Value));

            return result;
        }

        private static void ConfigureCombo(ComboBox comboBox, IReadOnlyList<StageSelectOption> options, string? key)
        {
            comboBox.DisplayMemberPath = nameof(StageSelectOption.Label);
            comboBox.ItemsSource = options;
            comboBox.SelectedItem = options.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                ?? options.FirstOrDefault();
            comboBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        private static void ConfigureStatusCombo(ComboBox comboBox, IReadOnlyList<StageSelectOption> options, long? value)
        {
            comboBox.Items.Clear();
            comboBox.DisplayMemberPath = string.Empty;
            comboBox.SelectedValuePath = string.Empty;
            comboBox.HorizontalAlignment = HorizontalAlignment.Stretch;

            ComboBoxItem? selectedItem = null;
            foreach (var option in options)
            {
                var item = new ComboBoxItem
                {
                    Tag = option,
                    Content = BuildStatusBadge(option.Label, option.Value, horizontalAlignment: HorizontalAlignment.Stretch)
                };
                comboBox.Items.Add(item);

                if (option.Value == value || (value is null && option.Value is null))
                {
                    selectedItem = item;
                }
            }

            comboBox.SelectedItem = selectedItem ?? comboBox.Items.Cast<object>().FirstOrDefault();
        }

        private StageSelectOption? GetSelectedStatusOption()
        {
            return (_statusBox.SelectedItem as ComboBoxItem)?.Tag as StageSelectOption;
        }

        

        private static UIElement BuildLabeledControl(string label, UIElement control)
        {
            var stack = new StackPanel
            {
                Spacing = 5
            };
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            stack.Children.Add(control);
            return stack;
        }

        private static Border BuildEditorGroupPanel(UIElement content, EditorGroupTone tone)
        {
            var backgroundKey = tone switch
            {
                EditorGroupTone.Accent => "ShellAccentPanelBackgroundAltBrush",
                EditorGroupTone.Muted => "ShellMutedPanelBackgroundBrush",
                _ => "ShellPanelBackgroundBrush"
            };

            return new Border
            {
                Padding = new Thickness(10, 8, 10, 10),
                Background = Application.Current.Resources[backgroundKey] as Brush,
                BorderBrush = Application.Current.Resources["ShellPanelBorderBrush"] as Brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = content
            };
        }

        private static UIElement BuildSectionTitle(string title)
        {
            return new Border
            {
                Margin = new Thickness(0, -4, 0, -2),
                Padding = new Thickness(4, 0, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = title,
                    FontSize = 11,
                    LineHeight = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = Application.Current.Resources["ShellTableHeaderTextBrush"] as Brush,
                    TextWrapping = TextWrapping.NoWrap
                }
            };
        }

        private static UIElement BuildSummaryLine(string label, string value)
        {
            return BuildSummaryElement(
                label,
                new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(value) ? "-" : value,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                });
        }

        private static UIElement BuildSummaryElement(string label, FrameworkElement valueElement)
        {
            var grid = new Grid
            {
                ColumnSpacing = 8
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(135) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            grid.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = Application.Current.Resources["ShellSecondaryTextBrush"] as Brush
            });

            Grid.SetColumn(valueElement, 1);
            grid.Children.Add(valueElement);
            return grid;
        }

        private static Border BuildStatusBadge(string statusName, long? statusId, HorizontalAlignment horizontalAlignment)
        {
            var colors = ResolveStatusBadgeColors(statusId);
            return new Border
            {
                Margin = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(6, 1, 6, 1),
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = horizontalAlignment,
                MaxWidth = 160,
                Background = new SolidColorBrush(colors.Background),
                Child = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(statusName) ? "-" : statusName,
                    Foreground = new SolidColorBrush(colors.Foreground),
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap
                }
            };
        }

        private static (Color Background, Color Foreground) ResolveStatusBadgeColors(long? statusId)
        {
            return statusId switch
            {
                4 or 5 => (Color.FromArgb(255, 201, 233, 212), Color.FromArgb(255, 64, 64, 64)),
                6 => (Color.FromArgb(255, 255, 205, 210), Color.FromArgb(255, 64, 64, 64)),
                1 or 2 => (Color.FromArgb(254, 194, 237, 246), Color.FromArgb(255, 64, 64, 64)),
                3 => (Color.FromArgb(254, 246, 227, 194), Color.FromArgb(255, 64, 64, 64)),
                _ => (Color.FromArgb(255, 222, 226, 230), Color.FromArgb(255, 64, 64, 64))
            };
        }

        private static TextBox BuildNumberTextBox()
        {
            return new TextBox
            {
                InputScope = new InputScope
                {
                    Names =
                    {
                        new InputScopeName(InputScopeNameValue.Number)
                    }
                }
            };
        }

        private string GetText(params string[] fieldKeys)
        {
            foreach (var fieldKey in fieldKeys)
            {
                var value = _sourceRow.GetValue(fieldKey)?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private string GetDisplayText(params string[] fieldKeys)
        {
            foreach (var fieldKey in fieldKeys)
            {
                var value = _displayRow.GetValue(fieldKey)?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return GetText(fieldKeys);
        }

        private static string FirstText(params object?[] values)
        {
            foreach (var value in values)
            {
                var text = value?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private string ResolveContractStatusName()
        {
            var directName = FirstText(
                _contractRow?.GetValue("status.name"),
                _displayRow.GetValue("contract.status.name"),
                _sourceRow.GetValue("contract.status.name"));
            if (!string.IsNullOrWhiteSpace(directName))
            {
                return directName;
            }

            var rawStatus = _contractRow?.GetValue("status")
                ?? _displayRow.GetValue("contract.status")
                ?? _sourceRow.GetValue("contract.status");
            if (rawStatus is not null && TryGetLong(rawStatus) is null)
            {
                var text = rawStatus.ToString();
                if (!string.IsNullOrWhiteSpace(text) && !text.TrimStart().StartsWith('{'))
                {
                    return text;
                }
            }

            var statusId = ResolveContractStatusId();
            return FindStatusLabel(statusId);
        }

        private long? ResolveContractStatusId()
        {
            return TryGetLong(_contractRow?.GetValue("status.id"))
                ?? TryGetLong(_contractRow?.GetValue("status_id"))
                ?? TryGetLong(_displayRow.GetValue("contract.status.id"))
                ?? TryGetLong(_sourceRow.GetValue("contract.status.id"))
                ?? TryGetLong(_displayRow.GetValue("contract.status_id"))
                ?? TryGetLong(_sourceRow.GetValue("contract.status_id"));
        }

        private string FindStatusLabel(long? statusId)
        {
            if (statusId is null)
            {
                return string.Empty;
            }

            return _statusOptions
                .FirstOrDefault(option => TryGetLong(option.Value) == statusId)
                ?.Label
                ?? string.Empty;
        }

        private static string BuildTitleText(ReferenceDataRow row)
        {
            var stageName = row.GetValue("name")?.ToString();
            return string.IsNullOrWhiteSpace(stageName)
                ? "Редактирование этапа"
                : $"Редактирование этапа {stageName}";
        }

        private static string FormatMoney(object? value)
        {
            return value switch
            {
                long longValue => longValue.ToString("N0", CultureInfo.CurrentCulture) + " руб.",
                int intValue => intValue.ToString("N0", CultureInfo.CurrentCulture) + " руб.",
                decimal decimalValue => decimalValue.ToString("N2", CultureInfo.CurrentCulture) + " руб.",
                string text when decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue)
                    => parsedValue.ToString("N2", CultureInfo.CurrentCulture) + " руб.",
                _ => value?.ToString() ?? string.Empty
            };
        }

        private static string FormatDisplayDate(object? value)
        {
            var date = ParseDate(value);
            return date?.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture) ?? "-";
        }

        private static string FormatBoolean(object? value)
        {
            return TryGetBool(value) == true ? "Да" : "Нет";
        }

        private static string FormatFlagDate(object? flagValue, object? dateValue)
        {
            var hasFlag = TryGetBool(flagValue) == true;
            var date = FormatDisplayDate(dateValue);
            if (!hasFlag)
            {
                return date == "-" ? "Нет" : date;
            }

            return date == "-" ? "Да" : date;
        }

        private static string? FormatDate(DateTimeOffset? value)
        {
            return value is null
                ? null
                : value.Value.Date.ToString("ddd MMM dd yyyy", CultureInfo.InvariantCulture);
        }

        private static DateTimeOffset? ParseDate(object? value)
        {
            var text = value?.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var offset))
            {
                return offset;
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
            {
                return new DateTimeOffset(date);
            }

            return null;
        }

        private static DateTimeOffset AddWorkingDaysToDate(DateTimeOffset current, int days)
        {
            var result = current.Date.AddDays(-1);
            var addedDays = 0;
            while (addedDays < days)
            {
                result = result.AddDays(1);
                if (result.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    continue;
                }

                addedDays++;
            }

            return result;
        }

        private static int? TryGetArrayCount(ReferenceDataRow? row, string fieldKey)
        {
            if (row is null
                || !row.Values.TryGetValue(fieldKey, out var value)
                || value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return value.GetArrayLength();
        }

        private static long? TryGetLong(object? value)
        {
            return value switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string text when long.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static int? TryGetInt(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsedValue)
                    ? parsedValue
                    : null;
        }

        private static bool? TryGetBool(object? value)
        {
            return value switch
            {
                bool boolValue => boolValue,
                string text when bool.TryParse(text, out var parsedValue) => parsedValue,
                string text when long.TryParse(text, out var numericValue) => numericValue != 0,
                long int64Value => int64Value != 0,
                int int32Value => int32Value != 0,
                decimal decimalValue => decimalValue != 0,
                _ => null
            };
        }

        private static long? TryGetJsonLong(JsonElement item, string propertyName)
        {
            if (!item.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt64(out var number) => number,
                JsonValueKind.String when long.TryParse(value.GetString(), out var number) => number,
                _ => null
            };
        }

        private static string? TryGetJsonString(JsonElement item, string propertyName)
        {
            return item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private sealed record StageSelectOption(string? Key, string Label, long? Value);

        private sealed record StageTaskOption(long TaskKindId, string Name, bool IsSelected);

        private sealed record StageTaskRecord(long? Id, string? ListKey, long? TaskKindId, string Name);

        private enum EditorGroupTone
        {
            Neutral,
            Accent,
            Muted
        }
    }
}
