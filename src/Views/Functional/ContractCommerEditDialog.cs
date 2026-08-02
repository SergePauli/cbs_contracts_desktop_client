using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Orders;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Stores.Orders;
using CbsContractsDesktopClient.Shared.Dates;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.Shared.Formatting;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.Orders;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pauli.WinUiKit.Controls;
using Windows.Storage.Pickers;
using Windows.System;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;
using static CbsContractsDesktopClient.Shared.Dialogs.StageContractDeadlineDialogOptions;
using static CbsContractsDesktopClient.Shared.Dialogs.StageContractStatusDialogControls;

namespace CbsContractsDesktopClient.Views.Functional
{
    public sealed class ContractCommerEditDialog : AppEditDialog
    {
        private readonly ContractWorkflowStore _workflowStore;
        private TableDataRow _contract;
        private readonly ContractCommentWorkflow _commentWorkflow;
        private readonly IReadOnlyList<CbsTableFilterOptionDefinition> _taskKindOptions;
        private readonly IReadOnlyList<ReferenceLookupItem> _stageTaskKindItems;
        private readonly IReadOnlyList<CbsTableFilterOptionDefinition> _contractStatusOptions;
        private readonly IReadOnlyList<CbsTableFilterOptionDefinition> _stageStatusOptions;
        private readonly Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>> _loadContragentOptionsAsync;
        private readonly IHolidayRecalculationService _holidayRecalculationService;
        private readonly bool _isCreateMode;
        private readonly Dropdown _taskKindBox = new();
        private readonly Dropdown _statusBox = new();
        private readonly CalendarInput _signedAtEditor = new();
        private readonly TextBox _yearBox = BuildNumberTextBox();
        private readonly TextBox _orderBox = new();
        private readonly TextBox _costBox = new();
        private readonly TextBox _commentBox = new();
        private readonly Dictionary<long, CommentBox> _stageCommentBoxes = [];
        private readonly Dictionary<long, StageSupplyView> _stageSupplyViews = [];
        private readonly StageSupplyEditWorkflow _stageSupplyEditWorkflow;
        private readonly IDataQueryService _dataQueryService;
        private CommentBox? _contractCommentsBox;
        private readonly CheckBox _governmentalBox = new();
        private readonly CheckBox _revisionPresentBox = new();
        private readonly TextBox _externalNumberBox = new();
        private readonly TextBox _revisionDescriptionBox = new();
        private readonly CalendarInput _deadlineAtEditor = new();
        private readonly CalendarInput _closedAtEditor = new();
        private readonly TextBox _revisionDocLinkBox = new();
        private readonly TextBox _revisionScanLinkBox = new();
        private readonly TextBox _revisionProtocolLinkBox = new();
        private readonly TextBox _revisionZipLinkBox = new();
        private readonly CheckBox _extAgreementBox = new();
        private readonly CheckBox _multiStageBox = new();
        private readonly Button _resetChangesButton = new();
        private AutoSuggestBox? _contragentBox;
        private IReadOnlyList<CbsTableFilterOptionDefinition> _contragentOptions = [];
        private IReadOnlyList<HolidayCalendarDay> _holidays = [];
        private CbsTableFilterOptionDefinition? _selectedContragentOption;
        private string _contragentInput = string.Empty;
        private IReadOnlyList<RevisionEditState> RevisionEditors =>
            _workflowStore.ContractRevisionEditStates
                .Where(static revision => revision.Priority > 0 && !revision.IsDestroyed)
                .ToList();

        private IReadOnlyList<StageEditState> StageEditors => _workflowStore.ContractStageEditStates
            .Where(static stage => !stage.IsDestroyed)
            .ToList();
        private TreeView? _stagesTree;
        private StackPanel? _revisionsStack;
        private TabView? _tabs;
        private TabViewItem? _contractTab;
        private TabViewItem? _stagesTab;
        private TabViewItem? _revisionsTab;
        private ContractCommerEditView? _view;
        private Button? _contractDocAttachButton;
        private Button? _contractScanAttachButton;
        private Button? _contractProtocolAttachButton;
        private Dropdown? _firstStageDeadlineKindBox;
        private Dropdown? _initialStageFocusTarget;
        private FrameworkElement? _initialRevisionFocusTarget;
        private readonly bool _openStagesTabOnLoad;
        private readonly bool _openRevisionsTabOnLoad;
        private bool _isUpdatingExtAgreementBox;
        private bool _isSyncingTaskKindSelection;
        private bool _contractClosePreviewApplied;
        private bool _contractCloseCommentApplied;

        public ContractCommerEditDialog(
            ContractWorkflowStore workflowStore,
            TableDataRow contract,
            IReadOnlyList<CbsTableFilterOptionDefinition> taskKindOptions,
            IReadOnlyList<ReferenceLookupItem> stageTaskKindItems,
            IReadOnlyList<CbsTableFilterOptionDefinition> contractStatusOptions,
            IReadOnlyList<CbsTableFilterOptionDefinition> stageStatusOptions,
            Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>> loadContragentOptionsAsync,
            bool isCreateMode = false,
            bool openStagesTabOnLoad = false,
            bool openRevisionsTabOnLoad = false)
        {
            ArgumentNullException.ThrowIfNull(workflowStore);
            ArgumentNullException.ThrowIfNull(contract);
            ArgumentNullException.ThrowIfNull(taskKindOptions);
            ArgumentNullException.ThrowIfNull(stageTaskKindItems);
            ArgumentNullException.ThrowIfNull(contractStatusOptions);
            ArgumentNullException.ThrowIfNull(stageStatusOptions);
            ArgumentNullException.ThrowIfNull(loadContragentOptionsAsync);

            _workflowStore = workflowStore;
            _contract = contract;
            _commentWorkflow = App.Services.GetRequiredService<ContractCommentWorkflow>();
            _stageSupplyEditWorkflow = App.Services.GetRequiredService<StageSupplyEditWorkflow>();
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _taskKindOptions = taskKindOptions;
            _stageTaskKindItems = stageTaskKindItems;
            _contractStatusOptions = contractStatusOptions;
            _stageStatusOptions = stageStatusOptions;
            _loadContragentOptionsAsync = loadContragentOptionsAsync;
            _holidayRecalculationService = App.Services.GetRequiredService<IHolidayRecalculationService>();
            _isCreateMode = isCreateMode;
            _openStagesTabOnLoad = openStagesTabOnLoad;
            _openRevisionsTabOnLoad = openRevisionsTabOnLoad;
            ResetStageEditorsFromContract();
            ResetRevisionEditorsFromContract();
            FullSizeDesired = false;
            HorizontalAlignment = HorizontalAlignment.Center;
            Title = BuildDialogTitle();
            Resources["ContentDialogMinWidth"] = 1220d;
            Resources["ContentDialogMinHeight"] = 620d;
            Resources["ContentDialogMaxWidth"] = 1220d;
            Content = BuildEditContent(BuildContent());
            Loaded += ContractCommerEditDialog_Loaded;
            DialogChrome.Apply(this);
            if (!string.IsNullOrWhiteSpace(_workflowStore.ContractEditGraphRepairMessage))
            {
                ShowErrorInfo(_workflowStore.ContractEditGraphRepairMessage);
            }
        }

        public ObservableCollection<string> ContragentSuggestionLabels { get; } = [];

        public override bool Validate()
        {
            ShowErrorInfo(string.Empty);
            CommitContragentInput(_contragentBox?.Text ?? _contragentInput);
            _taskKindBox.CommitText();
            _statusBox.CommitText();

            if (GetSelectedTaskKindOption()?.Id is null)
            {
                ShowErrorInfo("Выберите тип контракта.");
                return false;
            }

            if (TryReadContractYear() is null)
            {
                ShowErrorInfo("Укажите год контракта.");
                return false;
            }

            if (_selectedContragentOption is null || TryGetLong(_selectedContragentOption.Value) is null)
            {
                ShowErrorInfo("Выберите контрагента.");
                return false;
            }

            if (GetSelectedContractStatusId() is null)
            {
                ShowErrorInfo("Выберите статус контракта.");
                return false;
            }

            if (_workflowStore.ContractStageEditStates.Where(static stage => !stage.IsDestroyed).Count() == 0)
            {
                ShowErrorInfo("Контракт должен содержать хотя бы один этап.");
                return false;
            }

            return true;
        }

        public IReadOnlyDictionary<string, object?> BuildPayload(int? profileId)
        {
            SyncEditorsToWorkflowStore();
            return _workflowStore.BuildContractCommerPayload(new ContractCommerEditPayloadInput(
                IsCreateMode: _isCreateMode,
                Id: TryGetLong(_contract.GetValue("id")),
                ListKey: _contract.GetValue("list_key")?.ToString(),
                TaskKindId: GetSelectedTaskKindOption()?.Id,
                Code: GetSelectedTaskKindOption()?.Code,
                Year: TryReadContractYear(),
                Order: TryGetLong(_contract.GetValue("order")),
                ContragentId: _selectedContragentOption is null ? null : TryGetLong(_selectedContragentOption.Value),
                StatusId: GetSelectedContractStatusId(),
                SignedAt: _signedAtEditor.Date,
                Comment: _commentBox.Text,
                Governmental: _governmentalBox.IsChecked == true,
                ExternalNumber: _externalNumberBox.Text,
                DeadlineAt: _deadlineAtEditor.Date,
                ClosedAt: _closedAtEditor.Date,
                ProfileId: profileId));
        }

        private FrameworkElement BuildContent()
        {
            ConfigureTaskKindCombo();
            ConfigureYearBox();
            ConfigureOrderBox();
            ConfigureContragentState();
            ConfigureCostBox();
            ConfigureStatusCombo();
            ConfigureSignedAtEditor();
            ConfigureCommentBox();
            ConfigureFlagBoxes();
            ConfigureResetChangesButton();

            var view = new ContractCommerEditView();
            _view = view;
            _tabs = view.Tabs;
            _contractTab = view.ContractTabItem;
            _stagesTab = view.StagesTabItem;
            _revisionsTab = view.RevisionsTabItem;

            view.TaskKindSlot.Content = _taskKindBox;
            view.YearSlot.Content = _yearBox;
            view.OrderSlot.Content = _orderBox;
            view.ContragentSlot.Content = BuildContragentEditor();
            view.StatusSlot.Content = _statusBox;
            view.SignedAtSlot.Content = _signedAtEditor;
            view.CostSlot.Content = _costBox;
            view.CommentSlot.Content = _commentBox;
            view.ExtAgreementSlot.Content = BuildFlagHost(_extAgreementBox, "ДС");
            view.MultiStageSlot.Content = BuildFlagHost(_multiStageBox, "МЭ");
            view.ResetChangesSlot.Content = _isCreateMode ? null : _resetChangesButton;
            PopulateContractTab(view);
            view.StagesTabSlot.Content = BuildStagesTabContent();
            view.RevisionsTabSlot.Content = BuildRevisionsTabContent();

            return view;
        }

        private void PopulateContractTab(ContractCommerEditView view)
        {
            ResetMainTabEditorsFromContract();
            ConfigureMainTabEditors();

            view.GovernmentalSlot.Content = BuildInputLineCheckBox(_governmentalBox, "ГосКонтракт");
            view.RevisionPresentSlot.Content = BuildInputLineCheckBox(_revisionPresentBox, "В наличии");
            view.RevisionDescriptionSlot.Content = _revisionDescriptionBox;
            view.ExternalNumberSlot.Content = _externalNumberBox;
            view.DeadlineAtSlot.Content = _deadlineAtEditor;
            view.ClosedAtSlot.Content = _closedAtEditor;
            view.DocLinkRowSlot.Content = BuildFileRow("Исходник", "\uf000", _revisionDocLinkBox, button => _contractDocAttachButton = button);
            view.ScanLinkRowSlot.Content = BuildFileRow("Скан", "\uea90", _revisionScanLinkBox, button => _contractScanAttachButton = button);
            view.ProtocolLinkRowSlot.Content = BuildFileRow("Протокол", "\ue9a4", _revisionProtocolLinkBox, button => _contractProtocolAttachButton = button);

            ConfigureContractFileAttachTabChain();
        }

        private void ConfigureYearBox()
        {
            var year = TryGetLong(_contract.GetValue("year")) ?? DateTime.Now.Year;
            _yearBox.Text = (year % 100).ToString("00");
            _yearBox.MaxLength = 2;
            _yearBox.MinWidth = 38;
            _yearBox.TextAlignment = TextAlignment.Right;
            _yearBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            _yearBox.PreviewKeyDown += OnYearBoxPreviewKeyDown;
            _yearBox.PointerWheelChanged += OnYearBoxPointerWheelChanged;
        }

        private void ConfigureOrderBox()
        {
            var order = TryGetLong(_contract.GetValue("order"));
            _orderBox.Text = order is null ? string.Empty : order.Value.ToString("000");
            _orderBox.IsReadOnly = true;
            _orderBox.IsTabStop = false;
            _orderBox.MinWidth = 38;
            _orderBox.TextAlignment = TextAlignment.Right;
            _orderBox.HorizontalAlignment = HorizontalAlignment.Stretch;

            if (order is null)
            {
                ToolTipService.SetToolTip(_orderBox, "Будет присвоен после сохранения");
            }
        }

        private void ConfigureCostBox()
        {
            var stagesCost = SumStageCosts();
            _costBox.Text = FormatMoneyInput(stagesCost);
            ConfigureMoneyTextBox(_costBox);
        }

        private decimal? SumStageCosts()
        {
            decimal sum = 0;
            var hasStageCost = false;
            foreach (var stage in StageEditors)
            {
                if (stage.Cost is null)
                {
                    continue;
                }

                sum += stage.Cost.Value;
                hasStageCost = true;
            }

            return hasStageCost ? sum : null;
        }

        private void ConfigureTaskKindCombo()
        {
            var options = BuildTaskKindOptions();
            var selectedCode = GetText(_contract, "task_kind.code", "code");

            _taskKindBox.DisplayMemberPath = nameof(TaskKindSelectOption.Label);
            _taskKindBox.TextMemberPath = nameof(TaskKindSelectOption.Code);
            _taskKindBox.MatchMemberPath = nameof(TaskKindSelectOption.Code);
            _taskKindBox.IsClearButtonEnabled = false;
            _taskKindBox.TabTarget = _yearBox;
            _taskKindBox.MinWidth = 48;
            _taskKindBox.Items.Clear();
            _isSyncingTaskKindSelection = true;
            try
            {
                foreach (var option in options)
                {
                    _taskKindBox.Items.Add(option);

                    if (string.Equals(option.Code, selectedCode, StringComparison.OrdinalIgnoreCase))
                    {
                        _taskKindBox.SelectedItem = option;
                    }
                }
            }
            finally
            {
                _isSyncingTaskKindSelection = false;
            }

            _taskKindBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            _taskKindBox.SelectionChanged -= TaskKindBox_SelectionChanged;
            _taskKindBox.SelectionChanged += TaskKindBox_SelectionChanged;
            _taskKindBox.SelectionCommitted -= TaskKindBox_SelectionCommitted;
            _taskKindBox.SelectionCommitted += TaskKindBox_SelectionCommitted;
        }

        private void TaskKindBox_SelectionChanged(object? sender, EventArgs e)
        {
            SyncSingleStageTaskKindFromContract();
            RefreshStagesStack();
        }

        private void TaskKindBox_SelectionCommitted(object? sender, EventArgs e)
        {
            if (!_isSyncingTaskKindSelection && _taskKindBox.SelectedItem is TaskKindSelectOption)
            {
                FocusYearBox();
            }
        }

        private void FocusYearBox()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _yearBox.Focus(FocusState.Programmatic);
                _yearBox.SelectAll();
            });
        }

        private TaskKindSelectOption? GetSelectedTaskKindOption()
        {
            return _taskKindBox.SelectedItem as TaskKindSelectOption;
        }

        private long? GetSelectedContractStatusId()
        {
            return (_statusBox.SelectedItem as EnumSelectOption)?.Value;
        }

        private int? TryReadContractYear()
        {
            if (!int.TryParse(_yearBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var twoDigitYear))
            {
                return null;
            }

            return 2000 + Math.Clamp(twoDigitYear, 0, 99);
        }

        private void SyncEditorsToWorkflowStore()
        {
            SyncSingleStageTaskKindFromContract();
            GetContractRevisionEditState().IsPresent = _revisionPresentBox.IsChecked == true;
            GetContractRevisionEditState().Description = NormalizeEditorText(_revisionDescriptionBox.Text);
            GetContractRevisionEditState().DocLink = NormalizeEditorText(_revisionDocLinkBox.Text);
            GetContractRevisionEditState().ScanLink = NormalizeEditorText(_revisionScanLinkBox.Text);
            GetContractRevisionEditState().ProtocolLink = NormalizeEditorText(_revisionProtocolLinkBox.Text);
            GetContractRevisionEditState().ZipLink = NormalizeEditorText(_revisionZipLinkBox.Text);
            _workflowStore.SetContractStageEditStates(_workflowStore.ContractStageEditStates);
            _workflowStore.SetContractRevisionEditStates(_workflowStore.ContractRevisionEditStates);
        }

        private async void ContractCommerEditDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ContractCommerEditDialog_Loaded;
            try
            {
                _holidays = await _holidayRecalculationService.GetHolidayCalendarDaysAsync();
                ApplyStageDeadlineBusinessLogicToAll(applyInitialStart: false);
            }
            catch
            {
                _holidays = [];
            }

            if (_openStagesTabOnLoad)
            {
                FocusInitialStageEditor();
                return;
            }

            if (_openRevisionsTabOnLoad)
            {
                FocusInitialRevisionEditor();
                return;
            }

            if (string.IsNullOrWhiteSpace(_taskKindBox.Text))
            {
                return;
            }

            DispatcherQueue.TryEnqueue(() => _statusBox.Focus(FocusState.Programmatic));
        }

        private void FocusInitialStageEditor()
        {
            if (_tabs is not null && _stagesTab is not null)
            {
                _tabs.SelectedItem = _stagesTab;
            }

            DispatcherQueue.TryEnqueue(() => (_initialStageFocusTarget ?? _firstStageDeadlineKindBox)?.FocusInput());
        }

        private void FocusInitialRevisionEditor()
        {
            SelectRevisionsTab();
            DispatcherQueue.TryEnqueue(() => _initialRevisionFocusTarget?.Focus(FocusState.Programmatic));
        }

        private void ConfigureStatusCombo()
        {
            var statusId = ResolveStatusId();
            var statusOptions = BuildStatusOptions(_contractStatusOptions, includeEmpty: false);
            if (statusOptions.All(option => option.Value != statusId))
            {
                throw new InvalidOperationException($"Contract status options must contain status id {statusId}.");
            }

            _statusBox.MinWidth = 120;
            _statusBox.OnFocus = StatusBox_OnFocus;
            _statusBox.OnTab = StatusBox_OnTab;
            ConfigureStatusDropdown(_statusBox, statusOptions, statusId, option => ApplyContractStatusBusinessLogic(option));
        }

        private void StatusBox_OnFocus(Dropdown dropdown, RoutedEventArgs args)
        {
            dropdown.OpenDropDown();
        }

        private void StatusBox_OnTab(Dropdown dropdown, KeyRoutedEventArgs args)
        {
            dropdown.CloseDropDown();
            dropdown.CommitText();
            FocusSignedAtEditor();
            args.Handled = true;
        }

        private void ApplyContractStatusBusinessLogic(EnumSelectOption? option)
        {
            if (option is null)
            {
                return;
            }

            if (string.Equals(option.Label, "Подписан", StringComparison.CurrentCultureIgnoreCase))
            {
                _signedAtEditor.Date ??= DateTimeOffset.Now;
                return;
            }

            if (string.Equals(option.Label, "В проекте", StringComparison.CurrentCultureIgnoreCase))
            {
                _signedAtEditor.Date = null;
            }
        }

        private void FocusSignedAtEditor()
        {
            DispatcherQueue.TryEnqueue(() => _signedAtEditor.FocusInput());
        }

        private long ResolveStatusId()
        {
            var statusId = TryGetLong(_contract.GetValue("status.id")) ?? TryGetLong(_contract.GetValue("status_id"));
            if (statusId is long existingStatusId)
            {
                return existingStatusId;
            }

            if (!_isCreateMode)
            {
                throw new InvalidOperationException("Contract edit row must contain status_id or status.id.");
            }

            return _contractStatusOptions
                .Where(static option => string.Equals(option.Label, "В проекте", StringComparison.CurrentCultureIgnoreCase))
                .Select(static option => TryGetLong(option.Value))
                .FirstOrDefault(static id => id is not null)
                ?? throw new InvalidOperationException("Contract status options must contain 'В проекте'.");
        }

        private void ConfigureSignedAtEditor()
        {
            _signedAtEditor.Date = AppFormatters.ParseDate(_contract.GetValue("signed_at"));
            _signedAtEditor.DateChanged += SignedAtEditor_DateChanged;
            _signedAtEditor.OnTab = SignedAtEditor_OnTab;
        }

        private void SignedAtEditor_DateChanged(object? sender, EventArgs e)
        {
            ApplyStageDeadlineBusinessLogicToAll(applyInitialStart: true);
        }

        private void SignedAtEditor_OnTab(CalendarInput editor, KeyRoutedEventArgs args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _commentBox.Focus(FocusState.Programmatic);
                _commentBox.Select(_commentBox.Text.Length, 0);
            });
            args.Handled = true;
        }

        private void ConfigureCommentBox()
        {
            _commentBox.PlaceholderText = string.Empty;
            _commentBox.MinWidth = 640;
            _commentBox.Width = 640;
            _commentBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            _commentBox.KeyDown += ContractCommentBox_KeyDown;
        }

        private async void ContractCommentBox_KeyDown(object sender, KeyRoutedEventArgs args)
        {
            var contractId = TryGetLong(_contract.GetValue("id"));
            if (args.Key != VirtualKey.Enter || contractId is null or <= 0)
            {
                return;
            }

            args.Handled = true;
            if (string.IsNullOrWhiteSpace(_commentBox.Text))
            {
                return;
            }

            _commentBox.IsEnabled = false;
            try
            {
                var result = await _commentWorkflow.SaveContractCommentAsync(
                    contractId.Value,
                    _contract.GetValue("list_key")?.ToString(),
                    _commentBox.Text);
                _contract = result.Contract;
                _commentBox.Text = string.Empty;
                if (_contractCommentsBox is not null)
                {
                    _contractCommentsBox.Comments = result.Comments;
                }

                ShowErrorInfo(string.Empty);
            }
            catch (Exception ex)
            {
                ShowErrorInfo(ex.Message);
            }
            finally
            {
                _commentBox.IsEnabled = true;
            }
        }

        private void ConfigureFlagBoxes()
        {
            SetExtAgreementChecked(RevisionEditors.Count > 0);
            _extAgreementBox.Checked -= ExtAgreementBox_Checked;
            _extAgreementBox.Unchecked -= ExtAgreementBox_Unchecked;
            _extAgreementBox.PreviewKeyDown -= ExtAgreementBox_PreviewKeyDown;
            _extAgreementBox.Checked += ExtAgreementBox_Checked;
            _extAgreementBox.Unchecked += ExtAgreementBox_Unchecked;
            _extAgreementBox.PreviewKeyDown += ExtAgreementBox_PreviewKeyDown;
            ToolTipService.SetToolTip(_extAgreementBox, "Дополнительные соглашения");

            SetMultiStageChecked(IsMultiStageContract());
            _multiStageBox.IsHitTestVisible = false;
            _multiStageBox.IsTabStop = false;
            ToolTipService.SetToolTip(_multiStageBox, "Многоэтапный контракт");
        }

        private void ConfigureResetChangesButton()
        {
            var background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68));
            var hoverBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 38, 38));
            var pressedBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 185, 28, 28));
            var disabledBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 205, 210));
            var foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
            var disabledForeground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 156, 63, 63));

            _resetChangesButton.Width = 24;
            _resetChangesButton.MinHeight = 0;
            _resetChangesButton.Height = 24;
            _resetChangesButton.Padding = new Thickness(0);
            _resetChangesButton.Background = background;
            _resetChangesButton.Foreground = foreground;
            _resetChangesButton.HorizontalAlignment = HorizontalAlignment.Left;
            _resetChangesButton.VerticalAlignment = VerticalAlignment.Center;
            _resetChangesButton.Resources["ButtonBackground"] = background;
            _resetChangesButton.Resources["ButtonBackgroundPointerOver"] = hoverBackground;
            _resetChangesButton.Resources["ButtonBackgroundPressed"] = pressedBackground;
            _resetChangesButton.Resources["ButtonBackgroundDisabled"] = disabledBackground;
            _resetChangesButton.Resources["ButtonBorderBrushPointerOver"] = hoverBackground;
            _resetChangesButton.Resources["ButtonBorderBrushPressed"] = pressedBackground;
            _resetChangesButton.Resources["ButtonBorderBrushDisabled"] = disabledBackground;
            _resetChangesButton.Resources["ButtonForeground"] = foreground;
            _resetChangesButton.Resources["ButtonForegroundPointerOver"] = foreground;
            _resetChangesButton.Resources["ButtonForegroundPressed"] = foreground;
            _resetChangesButton.Resources["ButtonForegroundDisabled"] = disabledForeground;
            _resetChangesButton.Content = new FontIcon
            {
                Glyph = "\uE72C",
                FontSize = 12
            };
            ToolTipService.SetToolTip(
                _resetChangesButton,
                "Отменить несохраненные изменения формы");
            _resetChangesButton.Click += ResetChangesButton_Click;
        }

        private FrameworkElement BuildContragentEditor()
        {
            _contragentBox = DialogLookupEditors.BuildAutoSuggestBox(
                nameof(ContragentSuggestionLabels),
                () => _contragentInput,
                UpdateContragentOptionsAsync,
                TrySelectContragentSuggestion,
                CommitContragentInput,
                () => _contragentInput,
                minWidth: 390,
                maxWidth: 390,
                maxSuggestionListHeight: 240,
                bindingSource: this);
            _contragentBox.PreviewKeyDown -= ContragentBox_PreviewKeyDown;
            _contragentBox.PreviewKeyDown += ContragentBox_PreviewKeyDown;
            return _contragentBox;
        }

        private void ContragentBox_PreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (args.Key != VirtualKey.Tab)
            {
                return;
            }

            CommitContragentInput(_contragentBox?.Text);
            FocusStatusBox();
            args.Handled = true;
        }

        private void FocusStatusBox()
        {
            DispatcherQueue.TryEnqueue(() => _statusBox.Focus(FocusState.Programmatic));
        }

        private void ConfigureContragentState()
        {
            var contragentId = TryGetLong(_contract.GetValue("contragent.id")) ?? TryGetLong(_contract.GetValue("contragent_id"));
            var contragentName = GetText(_contract, "contragent.full_name", "contragent.name");
            _contragentInput = contragentName ?? string.Empty;
            _selectedContragentOption = contragentId is null || string.IsNullOrWhiteSpace(contragentName)
                ? null
                : new CbsTableFilterOptionDefinition
                {
                    Value = contragentId.Value,
                    Label = contragentName
                };
            _contragentOptions = _selectedContragentOption is null
                ? []
                : [_selectedContragentOption];
            RefreshContragentSuggestionLabels();
        }

        private async Task UpdateContragentOptionsAsync(string rawInput)
        {
            var searchText = rawInput?.Trim() ?? string.Empty;
            _contragentInput = rawInput ?? string.Empty;

            if (string.IsNullOrWhiteSpace(searchText)
                || string.Equals(searchText, _selectedContragentOption?.Label, StringComparison.CurrentCultureIgnoreCase))
            {
                _contragentOptions = _selectedContragentOption is null
                    ? []
                    : [_selectedContragentOption];
                RefreshContragentSuggestionLabels();
                return;
            }

            var options = await _loadContragentOptionsAsync(searchText, CancellationToken.None);
            _contragentOptions = MergeSelectedContragentOption(options);
            RefreshContragentSuggestionLabels();
        }

        private bool TrySelectContragentSuggestion(string? label)
        {
            var option = FindContragentOption(label);
            if (option is null)
            {
                return false;
            }

            SelectContragentOption(option);
            FocusStatusBox();
            return true;
        }

        private void CommitContragentInput(string? rawInput)
        {
            var option = FindContragentOption(rawInput);
            if (option is not null)
            {
                SelectContragentOption(option);
                return;
            }

            _contragentInput = _selectedContragentOption?.Label ?? string.Empty;
        }

        private void SelectContragentOption(CbsTableFilterOptionDefinition option)
        {
            _selectedContragentOption = option;
            _contragentInput = option.Label;
            _contragentOptions = MergeSelectedContragentOption(_contragentOptions);
            RefreshContragentSuggestionLabels();
        }

        private CbsTableFilterOptionDefinition? FindContragentOption(string? label)
        {
            var normalizedLabel = label?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(normalizedLabel)
                ? null
                : _contragentOptions.FirstOrDefault(option =>
                    string.Equals(option.Label, normalizedLabel, StringComparison.CurrentCultureIgnoreCase));
        }

        private IReadOnlyList<CbsTableFilterOptionDefinition> MergeSelectedContragentOption(
            IReadOnlyList<CbsTableFilterOptionDefinition> options)
        {
            if (_selectedContragentOption is null
                || options.Any(option => Equals(option.Value, _selectedContragentOption.Value)))
            {
                return options;
            }

            return [_selectedContragentOption, .. options];
        }

        private void RefreshContragentSuggestionLabels()
        {
            ContragentSuggestionLabels.Clear();
            foreach (var label in _contragentOptions
                .Select(static option => option.Label)
                .Where(static label => !string.IsNullOrWhiteSpace(label)))
            {
                ContragentSuggestionLabels.Add(label);
            }
        }

        private void OnYearBoxPreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (args.Key == VirtualKey.Up)
            {
                ChangeYear(1);
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.Down)
            {
                ChangeYear(-1);
                args.Handled = true;
            }
        }

        private void OnYearBoxPointerWheelChanged(object sender, PointerRoutedEventArgs args)
        {
            var wheelDelta = args.GetCurrentPoint(_yearBox).Properties.MouseWheelDelta;
            if (wheelDelta == 0)
            {
                return;
            }

            ChangeYear(wheelDelta > 0 ? 1 : -1);
            args.Handled = true;
        }

        private void ChangeYear(int delta)
        {
            var year = TryGetLong(_yearBox.Text) ?? DateTime.Now.Year % 100;
            var nextYear = (year + delta) % 100;
            if (nextYear < 0)
            {
                nextYear += 100;
            }

            _yearBox.Text = nextYear.ToString("00");
            _yearBox.SelectAll();
        }

        private void ResetChangesButton_Click(object sender, RoutedEventArgs e)
        {
            BuildResetConfirmationMenu().ShowAt(_resetChangesButton);
        }

        private MenuFlyout BuildResetConfirmationMenu()
        {
            var menu = new MenuFlyout();

            var resetItem = new MenuFlyoutItem
            {
                Text = "Сбросить изменения"
            };
            resetItem.Click += (_, _) => ResetEditorsFromContract();
            menu.Items.Add(resetItem);

            menu.Items.Add(new MenuFlyoutItem
            {
                Text = "Отмена"
            });

            return menu;
        }

        private void ResetEditorsFromContract()
        {
            ResetTaskKindFromContract();
            _yearBox.Text = ((TryGetLong(_contract.GetValue("year")) ?? DateTime.Now.Year) % 100).ToString("00");
            _orderBox.Text = TryGetLong(_contract.GetValue("order")) is long order
                ? order.ToString("000")
                : string.Empty;
            ConfigureContragentState();
            if (_contragentBox is not null)
            {
                _contragentBox.Text = _contragentInput;
            }

            ConfigureStatusCombo();
            _signedAtEditor.Date = AppFormatters.ParseDate(_contract.GetValue("signed_at"));
            _commentBox.Text = string.Empty;
            ResetMainTabEditorsFromContract();
            ResetStageEditorsFromContract();
            RefreshContractCostBox();
            RefreshStagesStack();
            ResetRevisionEditorsFromContract();
            SetExtAgreementChecked(RevisionEditors.Count > 0);
            RefreshRevisionsStack();
            SetMultiStageChecked(IsMultiStageContract());
        }

        private void ResetMainTabEditorsFromContract()
        {
            _governmentalBox.IsChecked = TryGetBool(_contract.GetValue("governmental")) == true;
            _externalNumberBox.Text = GetText(_contract, "external_number") ?? string.Empty;
            _deadlineAtEditor.Date = AppFormatters.ParseDate(_contract.GetValue("deadline_at"));
            _closedAtEditor.Date = AppFormatters.ParseDate(_contract.GetValue("closed_at"));

            var contractRevision = GetContractRevisionEditState();
            _revisionPresentBox.IsChecked = contractRevision.IsPresent;
            _revisionDescriptionBox.Text = contractRevision.Description ?? string.Empty;
            _revisionDocLinkBox.Text = contractRevision.DocLink ?? string.Empty;
            _revisionScanLinkBox.Text = contractRevision.ScanLink ?? string.Empty;
            _revisionProtocolLinkBox.Text = contractRevision.ProtocolLink ?? string.Empty;
            _revisionZipLinkBox.Text = contractRevision.ZipLink ?? string.Empty;
        }

        private RevisionEditState GetContractRevisionEditState()
        {
            var revision = _workflowStore.ContractRevisionEditStates.FirstOrDefault(static revision => revision.Priority == 0);
            if (revision is not null)
            {
                return revision;
            }

            throw new InvalidOperationException("Contract edit graph must contain revision with priority 0.");
        }

        private void ResetRevisionEditorsFromContract()
        {
            _workflowStore.SetContractRevisionEditStates(
                _workflowStore.ContractRevisionEditStates
                    .Where(static revision => revision.Priority == 0 || !revision.IsDestroyed));
        }

        private void ResetStageEditorsFromContract()
        {
            _workflowStore.ResetEditGraph();
            if (StageEditors.Count == 0)
            {
                throw new InvalidOperationException("Contract edit graph must contain at least one stage.");
            }

            SetMultiStageChecked(IsMultiStageContract());
        }

        private void AddStage(long priority)
        {
            var sourceStage = StageEditors.FirstOrDefault(stage => stage.Priority == priority - 1)
                ?? StageEditors.LastOrDefault();
            if (sourceStage is null)
            {
                return;
            }

            try
            {
                _workflowStore.AddStageAfter(sourceStage);
                SetMultiStageChecked(true);
                RefreshStagesStack();
                RefreshContractCostBox();
            }
            catch (InvalidOperationException ex)
            {
                ShowErrorInfo(ex.Message);
            }
        }

        private void DeleteStage(StageEditState stage)
        {
            try
            {
                _workflowStore.DeleteStage(stage);
                SetMultiStageChecked(StageEditors.Count > 1);
                RefreshStagesStack();
                RefreshContractCostBox();
            }
            catch (InvalidOperationException ex)
            {
                ShowErrorInfo(ex.Message);
            }
        }

        private void NormalizeStageNumbering()
        {
            _workflowStore.SetContractStageEditStates(StageEditors);
        }

        private void SetActiveStage(StageEditState selectedStage)
        {
            _workflowStore.SetActiveStage(selectedStage);
            RefreshStagesStack();
        }

        private void EnsureSingleActiveStage()
        {
            _workflowStore.SetContractStageEditStates(StageEditors);
        }

        private void ResetTaskKindFromContract()
        {
            var selectedCode = GetText(_contract, "task_kind.code", "code");
            _isSyncingTaskKindSelection = true;
            try
            {
                _taskKindBox.SelectedItem = _taskKindBox.Items
                    .OfType<TaskKindSelectOption>()
                    .FirstOrDefault(option => string.Equals(option.Code, selectedCode, StringComparison.OrdinalIgnoreCase));
                if (_taskKindBox.SelectedItem is null)
                {
                    _taskKindBox.Text = string.Empty;
                }
            }
            finally
            {
                _isSyncingTaskKindSelection = false;
            }

            SyncSingleStageTaskKindFromContract();
        }

        private void ExtAgreementBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingExtAgreementBox || RevisionEditors.Count > 0)
            {
                return;
            }

            AddRevision(1);
            SelectRevisionsTab();
        }

        private void ExtAgreementBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingExtAgreementBox)
            {
                return;
            }

            if (RevisionEditors.Count > 0)
            {
                SetExtAgreementChecked(true);
            }
        }

        private void ExtAgreementBox_PreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (args.Key != VirtualKey.Tab)
            {
                return;
            }

            FocusGovernmentalBox();
            args.Handled = true;
        }

        private void FocusGovernmentalBox()
        {
            if (_tabs is not null && _contractTab is not null)
            {
                _tabs.SelectedItem = _contractTab;
            }

            DispatcherQueue.TryEnqueue(() => _governmentalBox.Focus(FocusState.Programmatic));
        }

        private void AddRevision(long number)
        {
            var sourceRevision = _workflowStore.ContractRevisionEditStates.FirstOrDefault(revision => revision.Priority == number - 1)
                ?? _workflowStore.ContractRevisionEditStates.FirstOrDefault();
            if (sourceRevision is null)
            {
                return;
            }

            try
            {
                _workflowStore.AddRevisionAfter(sourceRevision);
                SetExtAgreementChecked(true);
                RefreshRevisionsStack();
            }
            catch (InvalidOperationException ex)
            {
                ShowErrorInfo(ex.Message);
            }
        }

        private void DeleteRevision(RevisionEditState revision)
        {
            try
            {
                _workflowStore.DeleteRevision(revision);
                if (RevisionEditors.Count == 0)
                {
                    SetExtAgreementChecked(false);
                }

                RefreshRevisionsStack();
            }
            catch (InvalidOperationException ex)
            {
                ShowErrorInfo(ex.Message);
            }
        }

        private void SetExtAgreementChecked(bool isChecked)
        {
            _isUpdatingExtAgreementBox = true;
            _extAgreementBox.IsChecked = isChecked;
            _isUpdatingExtAgreementBox = false;
        }

        private void SetMultiStageChecked(bool isChecked)
        {
            _multiStageBox.IsChecked = isChecked;
        }

        private void SelectRevisionsTab()
        {
            if (_tabs is not null && _revisionsTab is not null)
            {
                _tabs.SelectedItem = _revisionsTab;
            }
        }

        private bool IsMultiStageContract()
        {
            return StageEditors.Count > 1
                || StageEditors.Any(static stage => stage.Priority > 0);
        }

        private static FrameworkElement BuildFlagHost(CheckBox checkBox, string label)
        {
            checkBox.VerticalAlignment = VerticalAlignment.Center;
            checkBox.HorizontalAlignment = HorizontalAlignment.Left;
            checkBox.HorizontalContentAlignment = HorizontalAlignment.Left;
            checkBox.MinHeight = 0;
            checkBox.MinWidth = 0;
            checkBox.Width = 48;
            checkBox.Padding = new Thickness(0);
            checkBox.Margin = new Thickness(0);
            checkBox.Content = new TextBlock
            {
                Text = label,
                Margin = new Thickness(3, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            return new Grid
            {
                Width = 48,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    checkBox
                }
            };
        }

        private UIElement BuildStagesTabContent()
        {
            _stagesTree = new TreeView
            {
                Margin = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ItemTemplate = (DataTemplate?)_view?.Resources["StageTreeItemTemplate"]
            };
            RefreshStagesStack();
            return _stagesTree;
        }

        private void RefreshStagesStack()
        {
            if (_stagesTree is null)
            {
                return;
            }

            NormalizeStageNumbering();
            _firstStageDeadlineKindBox = null;
            _stageCommentBoxes.Clear();
            _contractCommentsBox = null;
            var items = StageEditors
                .OrderBy(static stage => stage.Priority)
                .Select(BuildStageTreeItem)
                .ToList();
            items.Add(BuildContractCommentsTreeItem());
            _stagesTree.ItemsSource = items;
        }

        private ContractStageTreeItem BuildStageTreeItem(StageEditState stage)
        {
            if (stage.Priority == 0)
            {
                SyncStageTaskKindFromContract(stage);
            }

            var header = BuildStageTreeHeader(GetStageTreeName(stage));
            return new ContractStageTreeItem(
                header,
                [
                    new ContractStageTreeItem(BuildStageSection(stage, header)),
                    BuildStageCommentsTreeItem(stage),
                    BuildStageSupplyTreeItem(stage)
                ],
                stage.Used);
        }

        private ContractStageTreeItem BuildStageCommentsTreeItem(StageEditState stage)
        {
            return new ContractStageTreeItem(
                BuildStageTreeHeader("Комментарии"),
                [new ContractStageTreeItem(BuildStageCommentsBox(stage))]);
        }

        private ContractStageTreeItem BuildStageSupplyTreeItem(StageEditState stage)
        {
            return new ContractStageTreeItem(
                BuildStageTreeHeader("Поставка"),
                [
                    new ContractStageTreeItem(
                        BuildStageSupplyContent(stage),
                        contentMargin: new Thickness(-56, 0, 0, 0))
                ]);
        }

        private FrameworkElement BuildStageSupplyContent(StageEditState stage)
        {
            if (stage.Id <= 0)
            {
                return new TextBlock
                {
                    Text = "Поставка станет доступна после сохранения этапа.",
                    FontSize = 11,
                    Margin = new Thickness(4)
                };
            }

            if (_stageSupplyViews.TryGetValue(stage.Id, out var existing))
            {
                return existing;
            }

            var view = new StageSupplyView(new StageSupplyStore(_dataQueryService, stage.Id));
            view.CreateRequested += async (_, args) =>
                await ShowStageSupplyFlyoutAsync(stage, view, null, args.Anchor);
            view.EditRequested += async (_, args) =>
                await ShowStageSupplyFlyoutAsync(stage, view, args.Row, args.Anchor);
            view.DeleteRequested += async (_, args) =>
                await DeleteStageSupplyAsync(view, args.Row);
            view.LoadFailed += (_, args) => ShowErrorInfo($"Не удалось загрузить поставку этапа: {args.Exception.Message}");
            _stageSupplyViews[stage.Id] = view;
            return view;
        }

        private async Task ShowStageSupplyFlyoutAsync(
            StageEditState stage,
            StageSupplyView supplyView,
            TableDataRow? sourceRow,
            FrameworkElement anchor)
        {
            try
            {
                var viewModel = await _stageSupplyEditWorkflow.CreateViewModelAsync(stage.Id, sourceRow);
                var flyout = new StageSupplyEditFlyout(_stageSupplyEditWorkflow, viewModel);
                if (await flyout.ShowAsync(anchor))
                    await supplyView.ReloadAsync(flyout.SavedRowId);
            }
            catch (Exception ex)
            {
                ShowErrorInfo($"Не удалось открыть позицию поставки: {ex.Message}");
            }
        }

        private async Task DeleteStageSupplyAsync(StageSupplyView supplyView, TableDataRow sourceRow)
        {
            try
            {
                await _stageSupplyEditWorkflow.DeleteAsync(sourceRow);
                await supplyView.ReloadAsync();
            }
            catch (Exception ex)
            {
                ShowErrorInfo($"Не удалось удалить позицию поставки: {ex.Message}");
            }
        }

        private CommentBox BuildStageCommentsBox(StageEditState stage)
        {
            var commentBox = BuildCompactCommentBox(ReadStageComments(stage));
            if (stage.Id > 0)
            {
                _stageCommentBoxes[stage.Id] = commentBox;
            }

            return commentBox;
        }

        private ContractStageTreeItem BuildContractCommentsTreeItem()
        {
            _contractCommentsBox = BuildCompactCommentBox(ReadContractComments());
            return new ContractStageTreeItem(
                BuildStageTreeHeader("Комментарии контракта"),
                [new ContractStageTreeItem(_contractCommentsBox)]);
        }

        private static CommentBox BuildCompactCommentBox(IReadOnlyList<TableDataRow> comments)
        {
            return new CommentBox
            {
                Height = 220,
                MaxHeight = 220,
                VerticalAlignment = VerticalAlignment.Stretch,
                Comments = comments
            };
        }

        private IReadOnlyList<TableDataRow> ReadStageComments(StageEditState stage)
        {
            if (stage.Id <= 0)
            {
                return [];
            }

            var stages = TryGetArray(_contract, "stages");
            if (stages is null)
            {
                return [];
            }

            foreach (var stageElement in stages.Value.EnumerateArray())
            {
                if (stageElement.ValueKind == JsonValueKind.Object
                    && TryGetLong(stageElement, "id") == stage.Id
                    && TryGetArray(stageElement, "comments") is JsonElement comments)
                {
                    return ToCommentRows(comments);
                }
            }

            return [];
        }

        private IReadOnlyList<TableDataRow> ReadContractComments()
        {
            var comments = TryGetArray(_contract, "comments");
            return comments is null ? [] : ToCommentRows(comments.Value);
        }

        private static IReadOnlyList<TableDataRow> ToCommentRows(JsonElement comments)
        {
            if (comments.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return comments
                .EnumerateArray()
                .Where(static comment => comment.ValueKind == JsonValueKind.Object)
                .Select(ToTableDataRow)
                .ToList();
        }

        private static TextBlock BuildStageTreeHeader(string stageName)
        {
            return new TextBlock
            {
                Text = stageName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.NoWrap
            };
        }

        private string GetStageTreeName(StageEditState stage)
        {
            if (stage.Id > 0)
            {
                return !string.IsNullOrWhiteSpace(stage.Name)
                    ? stage.Name
                    : throw new InvalidOperationException("Stage read model must contain Stage.name for contract stages tree.");
            }

            if (string.IsNullOrWhiteSpace(stage.TaskKind.Name))
            {
                return "Новый этап";
            }

            if (StageEditors.Count == 1 && stage.Priority == 0)
            {
                return stage.TaskKind.Name;
            }

            var priority = stage.Priority
                ?? throw new InvalidOperationException("New stage edit state must contain priority for tree name.");
            return $"Э{priority}_{stage.TaskKind.Name}";
        }

        private UIElement BuildStageSection(StageEditState stage, TextBlock treeHeader)
        {
            var grid = new Grid
            {
                ColumnSpacing = 8,
                RowSpacing = 8
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(106) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(193) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(106) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(123) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(106) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var stageStartEditor = BuildDateEditor(stage.StartAt, value => stage.StartAt = value);
            AddGridChild(grid, BuildInputLineCheckBox(BuildActiveStageCheckBox(stage), "АЭ"), 0, 0);
            AddGridChild(grid, BuildLabeledControl("Начало", stageStartEditor, spacing: 3), 0, 1);
            var stageTaskKindDropdown = BuildStageTaskKindDropdown(stage, treeHeader);
            var stageStatusDropdown = BuildStageStatusDropdown(stage);
            var stageDeadlineKindDropdown = BuildStageDeadlineKindDropdown(stage);
            if (_openStagesTabOnLoad && ReferenceEquals(stage, _workflowStore.SelectedStageEditState))
            {
                _initialStageFocusTarget = stageDeadlineKindDropdown;
            }

            var stageDurationEditor = BuildStageDurationEditor(stage.Duration, value => stage.Duration = value);
            var stageDeadlineEditor = BuildDateEditor(stage.DeadlineAt, value => stage.DeadlineAt = value);
            var stageCostEditor = BuildStageCostEditor(stage);
            stageTaskKindDropdown.TabTarget = stageStatusDropdown;
            stageStatusDropdown.TabTarget = stageDeadlineKindDropdown;
            stageDeadlineKindDropdown.OnTab = (dropdown, args) => StageDeadlineKind_OnTab(dropdown, stage, stageStartEditor, stageDurationEditor, stageDeadlineEditor, args);
            stageDeadlineKindDropdown.SelectionChanged += (_, _) =>
            {
                ApplyStageStartMode(stage, stageStartEditor);
                ApplyStageDeadlineMode(stage, stageDurationEditor, stageDeadlineEditor);
                SyncStageDeadlineEditorsFromBusinessRules(stage, stageStartEditor, stageDeadlineEditor, applyInitialStart: true);
            };
            stageDurationEditor.TextChanged += (_, _) => SyncStageDeadlineEditorsFromBusinessRules(stage, stageStartEditor, stageDeadlineEditor, applyInitialStart: false);
            stageStartEditor.DateChanged += (_, _) => SyncStageDeadlineEditorsFromBusinessRules(stage, stageStartEditor, stageDeadlineEditor, applyInitialStart: false);
            ConfigureTabTo(stageDurationEditor, stageCostEditor);
            stageDeadlineEditor.OnTab = (_, args) => FocusStageCostEditor(stageCostEditor, args);
            ApplyStageStartMode(stage, stageStartEditor);
            ApplyStageDeadlineMode(stage, stageDurationEditor, stageDeadlineEditor);
            AddGridChild(grid, BuildLabeledControl("Тип", stageTaskKindDropdown, spacing: 3), 0, 2);
            AddGridChild(grid, BuildLabeledControl("Статус", stageStatusDropdown, spacing: 3), 0, 3);
            AddGridChild(grid, BuildLabeledControl("Режим срока*", stageDeadlineKindDropdown, spacing: 3), 0, 4);
            AddGridChild(grid, BuildLabeledControl(
                "Дней",
                stageDurationEditor,
                spacing: 3), 0, 5);
            AddGridChild(grid, BuildLabeledControl("Срок", stageDeadlineEditor, spacing: 3), 0, 6);
            AddGridChild(grid, BuildLabeledControl("Сумма", stageCostEditor, spacing: 3), 0, 7);
            AddGridChild(grid, BuildLabeledControl("Бух. закрытие", BuildDateEditor(stage.FundedAt), spacing: 3), 0, 8);

            var paymentRow = BuildStagePaymentRow(stage);
            Grid.SetRow(paymentRow, 1);
            Grid.SetColumnSpan(paymentRow, 10);
            grid.Children.Add(paymentRow);

            var comment = BuildLabeledControl("Комментарий этапа", BuildStageCommentEditor(stage), spacing: 3);
            AddGridChild(grid, comment, 2, 0, 8);

            var separator = BuildSectionSeparator(null);
            Grid.SetRow(separator, 3);
            Grid.SetColumnSpan(separator, 10);
            grid.Children.Add(separator);

            return grid;
        }

        private FrameworkElement BuildStagePaymentRow(StageEditState stage)
        {
            var grid = new Grid
            {
                ColumnSpacing = 8
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(159) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(106) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(336) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(106) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(116) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(106) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var paymentDeadlineKindDropdown = BuildStagePaymentDeadlineKindDropdown(stage);
            var paymentDurationEditor = BuildStageDurationEditor(stage.PaymentDuration, value => stage.PaymentDuration = value);
            var paymentDeadlineEditor = BuildDateEditor(stage.PaymentDeadlineAt, value => stage.PaymentDeadlineAt = value);
            paymentDeadlineKindDropdown.OnTab = (dropdown, args) => StagePaymentDeadlineKind_OnTab(dropdown, stage, paymentDurationEditor, paymentDeadlineEditor, args);
            paymentDeadlineKindDropdown.SelectionChanged += (_, _) =>
            {
                ApplyStagePaymentDeadlineMode(stage, paymentDurationEditor, paymentDeadlineEditor);
                SyncStagePaymentDeadlineEditorsFromBusinessRules(stage, paymentDurationEditor, paymentDeadlineEditor);
            };
            paymentDurationEditor.TextChanged += (_, _) => SyncStagePaymentDeadlineEditorsFromBusinessRules(stage, paymentDurationEditor, paymentDeadlineEditor);
            ApplyStagePaymentDeadlineMode(stage, paymentDurationEditor, paymentDeadlineEditor);

            AddGridChild(grid, BuildLabeledControl("Режим оплаты", paymentDeadlineKindDropdown, spacing: 3), 0, 0);
            AddGridChild(grid, BuildLabeledControl(
                "Дней",
                paymentDurationEditor,
                spacing: 3), 0, 1);
            AddGridChild(grid, BuildLabeledControl("СрокОп", paymentDeadlineEditor, spacing: 3), 0, 2);
            AddGridChild(grid, BuildLabeledControl("Дополнительные задачи", BuildStageTasksMultiSelectEditor(stage), spacing: 3), 0, 3);
            AddGridChild(grid, BuildLabeledControl("Выезды", BuildReadonlyTextBox(FormatStageFlagText(stage.IsRideOut, stage.RideOutAt)), spacing: 3), 0, 4);
            AddGridChild(grid, BuildLabeledControl("Выполнены", BuildReadonlyTextBox(FormatStageFlagText(stage.CompletedAt is not null, stage.CompletedAt)), spacing: 3), 0, 5);
            AddGridChild(grid, BuildLabeledControl("Отправлены", BuildReadonlyTextBox(FormatStageFlagText(stage.IsSended, stage.SendedAt)), spacing: 3), 0, 6);

            return grid;
        }

        private UIElement BuildRevisionsTabContent()
        {
            _revisionsStack = new StackPanel
            {
                Padding = new Thickness(8),
                Spacing = 10
            };
            RefreshRevisionsStack();
            return _revisionsStack;
        }

        private void RefreshRevisionsStack()
        {
            if (_revisionsStack is null)
            {
                return;
            }

            _revisionsStack.Children.Clear();
            if (RevisionEditors.Count == 0)
            {
                _revisionsStack.Children.Add(BuildPlaceholder("Дополнительные соглашения отсутствуют."));
                return;
            }

            foreach (var revision in RevisionEditors.OrderBy(static revision => revision.Priority))
            {
                _revisionsStack.Children.Add(BuildRevisionSection(revision));
            }
        }

        private UIElement BuildRevisionSection(RevisionEditState revision)
        {
            var grid = new Grid
            {
                ColumnSpacing = 8,
                RowSpacing = 8
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(390) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var numberBox = new TextBox
            {
                Text = FormatRevisionNumber(revision.Priority),
                IsReadOnly = true,
                IsTabStop = false,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var number = (FrameworkElement)BuildLabeledControl("Номер", numberBox, spacing: 3);
            grid.Children.Add(number);

            var presentBox = new CheckBox
            {
                IsChecked = revision.IsPresent
            };
            presentBox.Checked += (_, _) => revision.IsPresent = true;
            presentBox.Unchecked += (_, _) => revision.IsPresent = false;
            var present = BuildInputLineCheckBox(presentBox, "В наличии");
            Grid.SetColumn(present, 1);
            grid.Children.Add(present);

            var descriptionBox = new TextBox
            {
                Text = revision.Description ?? string.Empty,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            if (_openRevisionsTabOnLoad
                && _workflowStore.FocusedRevisionPriority == revision.Priority)
            {
                _initialRevisionFocusTarget = descriptionBox;
            }

            descriptionBox.TextChanged += (_, _) => revision.Description = descriptionBox.Text ?? string.Empty;
            var description = (FrameworkElement)BuildLabeledControl(
                "Тип документа",
                BuildRevisionDescriptionEditor(
                    descriptionBox,
                    () => AddRevision(revision.Priority + 1),
                    () => DeleteRevision(revision)),
                spacing: 3);
            Grid.SetColumn(description, 2);
            Grid.SetColumnSpan(description, 2);
            grid.Children.Add(description);

            var docLink = BuildFileRow("Исходник", "\uf000", BuildRevisionFileTextBox(revision.DocLink, value => revision.DocLink = value));
            Grid.SetRow(docLink, 1);
            Grid.SetColumnSpan(docLink, 4);
            grid.Children.Add(docLink);

            var scanLink = BuildFileRow("Скан", "\uea90", BuildRevisionFileTextBox(revision.ScanLink, value => revision.ScanLink = value));
            Grid.SetRow(scanLink, 2);
            Grid.SetColumnSpan(scanLink, 4);
            grid.Children.Add(scanLink);

            var protocolLink = BuildFileRow("Протокол", "\ue9a4", BuildRevisionFileTextBox(revision.ProtocolLink, value => revision.ProtocolLink = value));
            Grid.SetRow(protocolLink, 3);
            Grid.SetColumnSpan(protocolLink, 4);
            grid.Children.Add(protocolLink);

            var separator = BuildSectionSeparator(null);
            Grid.SetRow(separator, 4);
            Grid.SetColumnSpan(separator, 4);
            grid.Children.Add(separator);

            return grid;
        }

        private void ConfigureMainTabEditors()
        {
            _governmentalBox.Content = null;
            _governmentalBox.HorizontalAlignment = HorizontalAlignment.Left;
            _governmentalBox.VerticalAlignment = VerticalAlignment.Center;
            _governmentalBox.MinHeight = 0;
            ToolTipService.SetToolTip(_governmentalBox, "Государственный контракт");

            _revisionPresentBox.Content = null;
            _revisionPresentBox.HorizontalAlignment = HorizontalAlignment.Left;
            _revisionPresentBox.VerticalAlignment = VerticalAlignment.Center;
            _revisionPresentBox.MinHeight = 0;
            _revisionPresentBox.Checked -= RevisionPresentBox_Checked;
            _revisionPresentBox.Unchecked -= RevisionPresentBox_Unchecked;
            _revisionPresentBox.Checked += RevisionPresentBox_Checked;
            _revisionPresentBox.Unchecked += RevisionPresentBox_Unchecked;
            ToolTipService.SetToolTip(_revisionPresentBox, "Документ в наличии");

            _revisionDescriptionBox.MinWidth = 260;
            _revisionDescriptionBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            _revisionDescriptionBox.TextChanged -= RevisionDescriptionBox_TextChanged;
            _revisionDescriptionBox.TextChanged += RevisionDescriptionBox_TextChanged;

            _externalNumberBox.MinWidth = 260;
            _externalNumberBox.HorizontalAlignment = HorizontalAlignment.Stretch;

            _deadlineAtEditor.OnTab = DeadlineAtEditor_OnTab;
            _closedAtEditor.OnTab = ClosedAtEditor_OnTab;

            ConfigureFileTextBox(_revisionDocLinkBox);
            ConfigureFileTextBox(_revisionScanLinkBox);
            ConfigureFileTextBox(_revisionProtocolLinkBox);
            ConfigureFileTextBox(_revisionZipLinkBox);
            _revisionDocLinkBox.TextChanged -= RevisionDocLinkBox_TextChanged;
            _revisionScanLinkBox.TextChanged -= RevisionScanLinkBox_TextChanged;
            _revisionProtocolLinkBox.TextChanged -= RevisionProtocolLinkBox_TextChanged;
            _revisionZipLinkBox.TextChanged -= RevisionZipLinkBox_TextChanged;
            _revisionDocLinkBox.TextChanged += RevisionDocLinkBox_TextChanged;
            _revisionScanLinkBox.TextChanged += RevisionScanLinkBox_TextChanged;
            _revisionProtocolLinkBox.TextChanged += RevisionProtocolLinkBox_TextChanged;
            _revisionZipLinkBox.TextChanged += RevisionZipLinkBox_TextChanged;
        }

        private void RevisionPresentBox_Checked(object sender, RoutedEventArgs e)
        {
            GetContractRevisionEditState().IsPresent = true;
        }

        private void RevisionPresentBox_Unchecked(object sender, RoutedEventArgs e)
        {
            GetContractRevisionEditState().IsPresent = false;
        }

        private void RevisionDescriptionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            GetContractRevisionEditState().Description = NormalizeEditorText(_revisionDescriptionBox.Text);
        }

        private void RevisionDocLinkBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            GetContractRevisionEditState().DocLink = NormalizeEditorText(_revisionDocLinkBox.Text);
        }

        private void RevisionScanLinkBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            GetContractRevisionEditState().ScanLink = NormalizeEditorText(_revisionScanLinkBox.Text);
        }

        private void RevisionProtocolLinkBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            GetContractRevisionEditState().ProtocolLink = NormalizeEditorText(_revisionProtocolLinkBox.Text);
        }

        private void RevisionZipLinkBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            GetContractRevisionEditState().ZipLink = NormalizeEditorText(_revisionZipLinkBox.Text);
        }

        private void DeadlineAtEditor_OnTab(CalendarInput editor, KeyRoutedEventArgs args)
        {
            DispatcherQueue.TryEnqueue(() => _closedAtEditor.FocusInput());
            args.Handled = true;
        }

        private void ClosedAtEditor_OnTab(CalendarInput editor, KeyRoutedEventArgs args)
        {
            DispatcherQueue.TryEnqueue(() => _contractDocAttachButton?.Focus(FocusState.Programmatic));
            args.Handled = true;
        }

        private void ConfigureContractFileAttachTabChain()
        {
            ConfigureAttachButtonTab(_contractDocAttachButton, () => _contractScanAttachButton?.Focus(FocusState.Programmatic));
            ConfigureAttachButtonTab(_contractScanAttachButton, () => _contractProtocolAttachButton?.Focus(FocusState.Programmatic));
            ConfigureAttachButtonTab(_contractProtocolAttachButton, FocusFirstStageDeadlineKindBox);
        }

        private static void ConfigureAttachButtonTab(Button? button, Action focusNext)
        {
            if (button is null)
            {
                return;
            }

            button.PreviewKeyDown -= AttachButton_PreviewKeyDown;
            button.PreviewKeyDown += AttachButton_PreviewKeyDown;
            button.Tag = focusNext;
        }

        private static void AttachButton_PreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (args.Key != VirtualKey.Tab || sender is not Button { Tag: Action focusNext })
            {
                return;
            }

            focusNext();
            args.Handled = true;
        }

        private void FocusFirstStageDeadlineKindBox()
        {
            if (_tabs is not null && _stagesTab is not null)
            {
                _tabs.SelectedItem = _stagesTab;
            }

            DispatcherQueue.TryEnqueue(() => _firstStageDeadlineKindBox?.FocusInput());
        }

        private static void AddGridChild(
            Grid grid,
            UIElement child,
            int row,
            int column,
            int columnSpan = 1)
        {
            var element = (FrameworkElement)child;
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            if (columnSpan > 1)
            {
                Grid.SetColumnSpan(element, columnSpan);
            }

            grid.Children.Add(child);
        }

        private static TextBox BuildReadonlyTextBox(string? text)
        {
            return new TextBox
            {
                Text = text ?? string.Empty,
                IsReadOnly = true,
                IsTabStop = false,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private CheckBox BuildActiveStageCheckBox(StageEditState stage)
        {
            var hasChoice = StageEditors.Count > 1;
            var checkBox = new CheckBox
            {
                IsChecked = stage.Used,
                IsEnabled = hasChoice,
                IsTabStop = hasChoice,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MinHeight = 0,
                MinWidth = 0,
                Padding = new Thickness(0),
                Margin = new Thickness(0)
            };
            checkBox.Checked += (_, _) => SetActiveStage(stage);
            checkBox.Unchecked += (_, _) =>
            {
                if (stage.Used)
                {
                    checkBox.IsChecked = true;
                    return;
                }

                EnsureSingleActiveStage();
            };

            return checkBox;
        }

        private static CalendarInput BuildDateEditor(DateTimeOffset? date, Action<DateTimeOffset?>? updateDate = null)
        {
            var editor = new CalendarInput
            {
                Date = date,
                IsReadOnly = true,
                IsTabStop = false
            };
            if (updateDate is not null)
            {
                editor.DateChanged += (_, args) => updateDate(args.NewDate);
            }

            return editor;
        }


        private Dropdown BuildStageTaskKindDropdown(StageEditState stage, TextBlock treeHeader)
        {
            var dropdown = new Dropdown
            {
                DisplayMemberPath = nameof(TaskKindSelectOption.Label),
                TextMemberPath = nameof(TaskKindSelectOption.Code),
                MatchMemberPath = nameof(TaskKindSelectOption.Code),
                IsClearButtonEnabled = false,
                MinWidth = 48,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var options = BuildTaskKindOptions();
            dropdown.ItemsSource = options;
            dropdown.SelectedItem = options.FirstOrDefault(option => string.Equals(option.Code, stage.TaskKind.Code, StringComparison.OrdinalIgnoreCase))
                ?? options.FirstOrDefault(option => string.Equals(option.Label, FormatTaskKind(stage), StringComparison.CurrentCultureIgnoreCase));
            if (stage.Priority == 0)
            {
                dropdown.IsHitTestVisible = false;
                dropdown.IsTabStop = false;
            }

            dropdown.SelectionChanged += (_, _) =>
            {
                if (dropdown.SelectedItem is not TaskKindSelectOption option)
                {
                    return;
                }

                stage.TaskKind = new TaskKindEditState(option.Id, ExtractTaskKindName(option), option.Code);
                treeHeader.Text = GetStageTreeName(stage);
            };

            return dropdown;
        }

        private Dropdown BuildStageStatusDropdown(StageEditState stage)
        {
            var options = BuildStageStatusOptions(_stageStatusOptions);
            var dropdown = new Dropdown
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var selectedStatusId = stage.Status.Id
                ?? options.FirstOrDefault(option => string.Equals(option.Label, stage.Status.Name, StringComparison.CurrentCultureIgnoreCase))?.Value;
            ConfigureStatusDropdown(dropdown, options, selectedStatusId, option =>
            {
                if (option is null)
                {
                    return;
                }

                stage.Status = new StatusEditState(option.Value, option.Label);
                ApplyStageStatusBusinessLogic(stage);
            });
            return dropdown;
        }

        private void ApplyStageStatusBusinessLogic(StageEditState stage)
        {
            if (stage.Status.Id == WorkflowStatusIds.Closed)
            {
                stage.ClosedAt ??= DateTimeOffset.Now;
            }
            else
            {
                stage.ClosedAt = null;
            }

            ApplyContractClosePreview(stage);
        }

        private void ApplyContractClosePreview(StageEditState stage)
        {
            var contract = _workflowStore.SelectedContractEditState
                ?? throw new InvalidOperationException("ContractCommerEditDialog.ApplyContractClosePreview: SelectedContractEditState is not set.");

            if (_workflowStore.ShouldCloseContractAfterStageClosed(stage))
            {
                contract.ApplyClosedStatusPreview(stage.ClosedAt);
                SelectContractStatus(WorkflowStatusIds.Closed);
                _closedAtEditor.Date = stage.ClosedAt;
                AppendContractCloseCommentIfNeeded();
                _contractClosePreviewApplied = true;
                return;
            }

            if (!_contractClosePreviewApplied)
            {
                return;
            }

            contract.RestoreStatusPreview();
            SelectContractStatus(contract.Status.Id);
            _closedAtEditor.Date = contract.ClosedAt;
            _contractClosePreviewApplied = false;
        }

        private void SelectContractStatus(long? statusId)
        {
            var option = _statusBox.ItemsSource
                ?.OfType<EnumSelectOption>()
                .FirstOrDefault(item => item.Value == statusId);
            if (option is null)
            {
                throw new InvalidOperationException($"ContractCommerEditDialog.SelectContractStatus: contract status options must contain status id {statusId}.");
            }

            _statusBox.SelectedItem = option;
        }

        private void AppendContractCloseCommentIfNeeded()
        {
            if (_contractCloseCommentApplied)
            {
                return;
            }

            AppendAutomaticContractComment("Статус контракта был изменен автоматически на \"Закрыт\"");
            _contractCloseCommentApplied = true;
        }

        private void AppendAutomaticContractComment(string text)
        {
            _commentBox.Text = string.IsNullOrWhiteSpace(_commentBox.Text)
                ? text
                : $"{_commentBox.Text.TrimEnd()}; {text}";
            _commentBox.Select(_commentBox.Text.Length, 0);
        }

        private List<TaskKindSelectOption> BuildTaskKindOptions()
        {
            return _taskKindOptions
                .Select(option =>
                {
                    var code = option.Value?.ToString() ?? string.Empty;
                    var id = _stageTaskKindItems
                        .Where(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase))
                        .Select(static item => AppFormatters.TryGetLong(item.Id))
                        .FirstOrDefault(static value => value is not null);
                    return new TaskKindSelectOption(id, code, option.Label);
                })
                .ToList();
        }

        private void SyncSingleStageTaskKindFromContract()
        {
            foreach (var stage in StageEditors.Where(static stage => stage.Priority == 0))
            {
                SyncStageTaskKindFromContract(stage);
            }
        }

        private void SyncStageTaskKindFromContract(StageEditState stage)
        {
            if (_taskKindBox.SelectedItem is not TaskKindSelectOption option)
            {
                stage.TaskKind = new TaskKindEditState(null, null, null);
                return;
            }

            stage.TaskKind = new TaskKindEditState(option.Id, ExtractTaskKindName(option), option.Code);
        }

        private Dropdown BuildStageDeadlineKindDropdown(StageEditState stage)
        {
            var dropdown = BuildEnumDropdown(DeadlineKindOptions(), stage.DeadlineKind);
            _firstStageDeadlineKindBox ??= dropdown;
            dropdown.SelectionChanged += (_, _) =>
            {
                stage.DeadlineKind = GetSelectedDropdownKey(dropdown);
            };

            return dropdown;
        }

        private void StageDeadlineKind_OnTab(
            Dropdown dropdown,
            StageEditState stage,
            CalendarInput startEditor,
            TextBox durationEditor,
            CalendarInput deadlineEditor,
            KeyRoutedEventArgs args)
        {
            dropdown.CloseDropDown();
            dropdown.CommitText();
            stage.DeadlineKind = GetSelectedDropdownKey(dropdown);
            ApplyStageStartMode(stage, startEditor);
            ApplyStageDeadlineMode(stage, durationEditor, deadlineEditor);
            SyncStageDeadlineEditorsFromBusinessRules(stage, startEditor, deadlineEditor, applyInitialStart: true);
            if (IsStageCalendarPlanMode(stage.DeadlineKind))
            {
                DispatcherQueue.TryEnqueue(() => deadlineEditor.FocusInput());
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    durationEditor.Focus(FocusState.Programmatic);
                    durationEditor.SelectAll();
                });
            }

            args.Handled = true;
        }

        private void ConfigureTabTo(TextBox source, TextBox target)
        {
            source.PreviewKeyDown += (_, args) =>
            {
                if (args.Key != VirtualKey.Tab)
                {
                    return;
                }

                DispatcherQueue.TryEnqueue(() =>
                {
                    target.Focus(FocusState.Programmatic);
                    target.SelectAll();
                });
                args.Handled = true;
            };
        }

        private void FocusStageCostEditor(TextBox costEditor, KeyRoutedEventArgs args)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                costEditor.Focus(FocusState.Programmatic);
                costEditor.SelectAll();
            });
            args.Handled = true;
        }

        private static void ApplyStageDeadlineMode(
            StageEditState stage,
            TextBox durationEditor,
            CalendarInput deadlineEditor)
        {
            var isCalendarPlan = IsStageCalendarPlanMode(stage.DeadlineKind);
            durationEditor.IsReadOnly = isCalendarPlan;
            durationEditor.IsTabStop = !isCalendarPlan;
            deadlineEditor.IsReadOnly = !isCalendarPlan;
            deadlineEditor.IsTabStop = isCalendarPlan;
        }

        private static void ApplyStageStartMode(StageEditState stage, CalendarInput startEditor)
        {
            var isPaymentBased = StageDeadlineBusinessRules.IsPaymentBasedDeadlineMode(stage.DeadlineKind);
            startEditor.IsReadOnly = isPaymentBased;
            startEditor.IsTabStop = !isPaymentBased;
        }

        private static bool IsStageCalendarPlanMode(string? deadlineKind)
        {
            return StageDeadlineBusinessRules.IsDeadlineManualMode(deadlineKind);
        }

        private static Dropdown BuildStagePaymentDeadlineKindDropdown(StageEditState stage)
        {
            var dropdown = BuildEnumDropdown(PaymentDeadlineKindOptions(), stage.PaymentDeadlineKind);
            dropdown.SelectionChanged += (_, _) =>
            {
                stage.PaymentDeadlineKind = GetSelectedDropdownKey(dropdown);
            };

            return dropdown;
        }

        private void StagePaymentDeadlineKind_OnTab(
            Dropdown dropdown,
            StageEditState stage,
            TextBox durationEditor,
            CalendarInput deadlineEditor,
            KeyRoutedEventArgs args)
        {
            dropdown.CloseDropDown();
            dropdown.CommitText();
            stage.PaymentDeadlineKind = GetSelectedDropdownKey(dropdown);
            ApplyStagePaymentDeadlineMode(stage, durationEditor, deadlineEditor);
            SyncStagePaymentDeadlineEditorsFromBusinessRules(stage, durationEditor, deadlineEditor);
            if (IsStagePaymentCalendarPlanMode(stage.PaymentDeadlineKind))
            {
                DispatcherQueue.TryEnqueue(() => deadlineEditor.FocusInput());
            }
            else
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    durationEditor.Focus(FocusState.Programmatic);
                    durationEditor.SelectAll();
                });
            }

            args.Handled = true;
        }

        private static void ApplyStagePaymentDeadlineMode(
            StageEditState stage,
            TextBox durationEditor,
            CalendarInput deadlineEditor)
        {
            var isCalendarPlan = IsStagePaymentCalendarPlanMode(stage.PaymentDeadlineKind);
            durationEditor.IsReadOnly = isCalendarPlan;
            durationEditor.IsTabStop = !isCalendarPlan;
            deadlineEditor.IsReadOnly = !isCalendarPlan;
            deadlineEditor.IsTabStop = isCalendarPlan;
        }

        private static bool IsStagePaymentCalendarPlanMode(string? paymentDeadlineKind)
        {
            return StageDeadlineBusinessRules.IsPaymentDeadlineManualMode(paymentDeadlineKind);
        }

        private void ApplyStageDeadlineBusinessLogicToAll(bool applyInitialStart)
        {
            foreach (var stage in StageEditors)
            {
                ApplyStageDeadlineBusinessLogic(stage, applyInitialStart);
                ApplyStagePaymentDeadlineBusinessLogic(stage);
            }

            RefreshStagesStack();
        }

        private void SyncStageDeadlineEditorsFromBusinessRules(
            StageEditState stage,
            CalendarInput startEditor,
            CalendarInput deadlineEditor,
            bool applyInitialStart)
        {
            ApplyStageDeadlineBusinessLogic(stage, applyInitialStart);
            startEditor.Date = stage.StartAt;
            deadlineEditor.Date = stage.DeadlineAt;
        }

        private void SyncStagePaymentDeadlineEditorsFromBusinessRules(
            StageEditState stage,
            TextBox durationEditor,
            CalendarInput deadlineEditor)
        {
            ApplyStagePaymentDeadlineBusinessLogic(stage);
            SyncNumberText(durationEditor, stage.PaymentDuration);
            deadlineEditor.Date = stage.PaymentDeadlineAt;
        }

        private void ApplyStageDeadlineBusinessLogic(StageEditState stage, bool applyInitialStart)
        {
            if (applyInitialStart)
            {
                ApplyStageInitialStartBusinessLogic(stage);
            }

            if (StageDeadlineBusinessRules.IsDeadlineManualMode(stage.DeadlineKind))
            {
                return;
            }

            stage.DeadlineAt = StageDeadlineBusinessRules.CalculateDeadline(
                stage.DeadlineKind,
                stage.StartAt,
                stage.Duration,
                _holidays);
        }

        private void ApplyStageInitialStartBusinessLogic(StageEditState stage)
        {
            if (StageDeadlineBusinessRules.IsPaymentBasedDeadlineMode(stage.DeadlineKind))
            {
                stage.StartAt = stage.PaymentBaseDate;
                return;
            }

            var nextStart = StageDeadlineBusinessRules.ResolveInitialStart(
                IsMultiStageContract(),
                stage.StartAt,
                stage.DeadlineKind,
                _signedAtEditor.Date,
                stage.PaymentBaseDate);

            if (nextStart is not null)
            {
                stage.StartAt = nextStart;
            }
        }

        private void ApplyStagePaymentDeadlineBusinessLogic(StageEditState stage)
        {
            var paymentDeadline = StageDeadlineBusinessRules.CalculatePaymentDeadline(
                stage.PaymentDeadlineKind,
                stage.FundedAt,
                stage.PaymentDuration,
                _holidays);

            if (paymentDeadline is not null)
            {
                stage.PaymentDeadlineAt = paymentDeadline;
                return;
            }

            if (StageDeadlineBusinessRules.ShouldClearPaymentDuration(stage.PaymentDeadlineKind, stage.PaymentDuration))
            {
                stage.PaymentDuration = null;
                return;
            }

            if (stage.PaymentDuration is null
                && stage.FundedAt is not null
                && stage.Original.PaymentDeadlineAt is null
                && !StageDeadlineBusinessRules.IsPaymentDeadlineManualMode(stage.PaymentDeadlineKind))
            {
                stage.PaymentDeadlineAt = null;
            }
        }

        private static void SyncNumberText(TextBox textBox, int? value)
        {
            var nextText = FormatNullableNumber(value);
            if (!string.Equals(textBox.Text, nextText, StringComparison.Ordinal))
            {
                textBox.Text = nextText;
            }
        }

        private static Dropdown BuildEnumDropdown(IReadOnlyList<EnumSelectOption> options, string? key)
        {
            var dropdown = new Dropdown
            {
                DisplayMemberPath = nameof(EnumSelectOption.Label),
                TextMemberPath = nameof(EnumSelectOption.Label),
                MatchMemberPath = nameof(EnumSelectOption.Key),
                IsClearButtonEnabled = false,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            dropdown.ItemsSource = options;
            dropdown.SelectedItem = options.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                ?? options.FirstOrDefault();
            return dropdown;
        }

        private static string? GetSelectedDropdownKey(Dropdown dropdown)
        {
            return (dropdown.SelectedItem as EnumSelectOption)?.Key;
        }

        private FrameworkElement BuildStageCommentEditor(StageEditState stage)
        {
            var grid = new Grid
            {
                ColumnSpacing = 6
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var commentBox = new TextBox
            {
                Text = stage.Comment,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            commentBox.TextChanged += (_, _) => stage.Comment = commentBox.Text ?? string.Empty;
            commentBox.KeyDown += (_, args) => StageCommentBox_KeyDown(stage, commentBox, args);
            grid.Children.Add(commentBox);

            var addButton = BuildRevisionActionButton(
                "\ue710",
                "Добавить этап",
                Microsoft.UI.ColorHelper.FromArgb(255, 34, 197, 94),
                Microsoft.UI.ColorHelper.FromArgb(255, 22, 163, 74),
                Microsoft.UI.ColorHelper.FromArgb(255, 21, 128, 61));
            addButton.Click += (_, _) => AddStage((stage.Priority ?? 0) + 1);
            Grid.SetColumn(addButton, 1);
            grid.Children.Add(addButton);

            if (StageEditors.Count == 1 && stage.Priority == 0)
            {
                return grid;
            }

            var deleteButton = BuildRevisionActionButton(
                "\ue74d",
                "Удалить этап",
                Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68),
                Microsoft.UI.ColorHelper.FromArgb(255, 220, 38, 38),
                Microsoft.UI.ColorHelper.FromArgb(255, 185, 28, 28));
            deleteButton.Click += (_, _) => DeleteStage(stage);
            Grid.SetColumn(deleteButton, 2);
            grid.Children.Add(deleteButton);

            return grid;
        }

        private async void StageCommentBox_KeyDown(
            StageEditState stage,
            TextBox editor,
            KeyRoutedEventArgs args)
        {
            if (args.Key != VirtualKey.Enter || stage.Id <= 0)
            {
                return;
            }

            args.Handled = true;
            if (string.IsNullOrWhiteSpace(editor.Text))
            {
                return;
            }

            editor.IsEnabled = false;
            try
            {
                var contractId = TryGetLong(_contract.GetValue("id"))
                    ?? throw new InvalidOperationException("Persisted stage comment requires contract id.");
                var result = await _commentWorkflow.SaveStageCommentAsync(
                    contractId,
                    stage.Id,
                    stage.ListKey,
                    editor.Text);
                _contract = result.Contract;
                editor.Text = string.Empty;
                if (_stageCommentBoxes.TryGetValue(stage.Id, out var commentsBox))
                {
                    commentsBox.Comments = result.Comments;
                }

                ShowErrorInfo(string.Empty);
            }
            catch (Exception ex)
            {
                ShowErrorInfo(ex.Message);
            }
            finally
            {
                editor.IsEnabled = true;
            }
        }

        private static TextBox BuildStageDurationEditor(int? duration, Action<int?> updateDuration)
        {
            var textBox = BuildNumberTextBox();
            textBox.Text = FormatNullableNumber(duration);
            textBox.TextAlignment = TextAlignment.Right;
            textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            textBox.TextChanged += (_, _) => updateDuration(TryGetInt(textBox.Text));
            return textBox;
        }

        private TextBox BuildStageCostEditor(StageEditState stage)
        {
            var textBox = BuildMoneyInputTextBox(FormatMoneyInput(stage.Cost));
            textBox.TextChanged += (_, _) =>
            {
                stage.Cost = TryParseMoney(textBox.Text);
                RefreshContractCostBox();
            };
            return textBox;
        }

        private MultiSelect BuildStageTasksMultiSelectEditor(StageEditState stage)
        {
            var multiSelect = new MultiSelect();
            var taskRecords = stage.Tasks
                .Select(static task => new StageTaskRecord(task.Id, task.ListKey, task.TaskKindId, task.Name ?? string.Empty))
                .ToList();
            var selectedTaskKindIds = taskRecords
                .Select(static task => task.TaskKindId)
                .Where(static id => id is not null)
                .Select(static id => id!.Value)
                .ToHashSet();
            var taskOptions = StageContractTaskDialogControls.CreateTaskOptions(_stageTaskKindItems, taskRecords);
            return StageContractTaskDialogControls.ConfigureTasksMultiSelect(
                multiSelect,
                taskOptions,
                selectedTaskKindIds,
                (_, args) =>
                {
                    StageContractTaskDialogControls.UpdateSelectedTaskKindIds(selectedTaskKindIds, args);
                    stage.Tasks = taskOptions
                        .Where(option => selectedTaskKindIds.Contains(option.TaskKindId))
                        .Select(option =>
                        {
                            var existing = taskRecords.FirstOrDefault(task => task.TaskKindId == option.TaskKindId);
                            return existing is null
                                ? new StageTaskEditState(null, null, option.TaskKindId, option.Name)
                                : new StageTaskEditState(existing.Id, existing.ListKey, option.TaskKindId, option.Name);
                        })
                        .ToList();
                });
        }

        private void RefreshContractCostBox()
        {
            _costBox.Text = SumStageCosts() is decimal stagesCost
                ? FormatMoneyInput(stagesCost)
                : string.Empty;
        }

        private static void ConfigureFileTextBox(TextBox textBox)
        {
            textBox.MinWidth = 260;
            textBox.IsReadOnly = true;
            textBox.IsTabStop = false;
            textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        private static TextBox BuildRevisionFileTextBox(string? value, Action<string?> updateValue)
        {
            var textBox = new TextBox
            {
                Text = value ?? string.Empty
            };
            textBox.TextChanged += (_, _) => updateValue(string.IsNullOrWhiteSpace(textBox.Text) ? null : textBox.Text);
            ConfigureFileTextBox(textBox);
            return textBox;
        }

        private static string FormatRevisionNumber(long number)
        {
            return number > 0
                ? number.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string FormatNullableNumber(int? value)
        {
            return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string? NormalizeEditorText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

            var normalized = text
                .Replace("руб.", string.Empty, StringComparison.CurrentCultureIgnoreCase)
                .Replace("руб", string.Empty, StringComparison.CurrentCultureIgnoreCase)
                .Replace("₽", string.Empty, StringComparison.CurrentCultureIgnoreCase)
                .Trim();

            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out var currentCultureValue)
                ? currentCultureValue
                : decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantCultureValue)
                    ? invariantCultureValue
                    : null;
        }

        private static string FormatTaskKind(StageEditState stage)
        {
            if (string.IsNullOrWhiteSpace(stage.TaskKind.Code))
            {
                return stage.TaskKind.Name ?? string.Empty;
            }

            return string.IsNullOrWhiteSpace(stage.TaskKind.Name)
                ? stage.TaskKind.Code
                : $"{stage.TaskKind.Code} - {stage.TaskKind.Name}";
        }

        private static string ExtractTaskKindName(TaskKindSelectOption option)
        {
            if (string.IsNullOrWhiteSpace(option.Code))
            {
                return option.Label;
            }

            var prefix = option.Code + " - ";
            return option.Label.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase)
                ? option.Label[prefix.Length..]
                : option.Label;
        }

        private static string FormatStageFlagText(bool? isSet, DateTimeOffset? date)
        {
            if (date is not null)
            {
                return AppFormatters.FormatDisplayDate(date);
            }

            return isSet == true ? "ДА" : "НЕТ";
        }

        private static FrameworkElement BuildInputLineCheckBox(CheckBox checkBox, string label)
        {
            var element = BuildInlineCheckBox(checkBox, label);
            element.Margin = new Thickness(0, 18, 0, 0);
            return element;
        }

        private static FrameworkElement BuildRevisionDescriptionEditor(
            TextBox descriptionBox,
            Action addRevision,
            Action deleteRevision)
        {
            descriptionBox.MinWidth = 285;
            descriptionBox.HorizontalAlignment = HorizontalAlignment.Stretch;

            var grid = new Grid
            {
                ColumnSpacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(285) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            grid.Children.Add(descriptionBox);

            var addButton = BuildRevisionActionButton(
                "\ue710",
                "Добавить ревизию",
                Microsoft.UI.ColorHelper.FromArgb(255, 34, 197, 94),
                Microsoft.UI.ColorHelper.FromArgb(255, 22, 163, 74),
                Microsoft.UI.ColorHelper.FromArgb(255, 21, 128, 61));
            addButton.Click += (_, _) => addRevision();
            Grid.SetColumn(addButton, 1);
            grid.Children.Add(addButton);

            var deleteButton = BuildRevisionActionButton(
                "\ue74d",
                "Удалить ревизию",
                Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68),
                Microsoft.UI.ColorHelper.FromArgb(255, 220, 38, 38),
                Microsoft.UI.ColorHelper.FromArgb(255, 185, 28, 28));
            deleteButton.Click += (_, _) => deleteRevision();
            Grid.SetColumn(deleteButton, 2);
            grid.Children.Add(deleteButton);

            return grid;
        }

        private static Button BuildRevisionActionButton(
            string iconGlyph,
            string tooltip,
            Windows.UI.Color backgroundColor,
            Windows.UI.Color hoverColor,
            Windows.UI.Color pressedColor)
        {
            var background = new Microsoft.UI.Xaml.Media.SolidColorBrush(backgroundColor);
            var hoverBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(hoverColor);
            var pressedBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(pressedColor);
            var foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);

            var button = new Button
            {
                Width = 24,
                Height = 24,
                MinWidth = 24,
                Padding = new Thickness(0),
                Background = background,
                Foreground = foreground,
                Content = new TextBlock
                {
                    Text = iconGlyph,
                    FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = foreground
                }
            };
            button.Resources["ButtonBackground"] = background;
            button.Resources["ButtonBackgroundPointerOver"] = hoverBackground;
            button.Resources["ButtonBackgroundPressed"] = pressedBackground;
            button.Resources["ButtonBorderBrush"] = background;
            button.Resources["ButtonBorderBrushPointerOver"] = hoverBackground;
            button.Resources["ButtonBorderBrushPressed"] = pressedBackground;
            button.Resources["ButtonForeground"] = foreground;
            button.Resources["ButtonForegroundPointerOver"] = foreground;
            button.Resources["ButtonForegroundPressed"] = foreground;
            ToolTipService.SetToolTip(button, tooltip);
            return button;
        }

        private static FrameworkElement BuildSectionSeparator(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return new Grid
                {
                    Margin = new Thickness(0, 4, 0, 0),
                    Children =
                    {
                        BuildSeparatorLine()
                    }
                };
            }

            var leftLine = BuildSeparatorLine();

            var titleBlock = new TextBlock
            {
                Text = title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 1);

            var rightLine = BuildSeparatorLine();
            Grid.SetColumn(rightLine, 2);

            return new Grid
            {
                Margin = new Thickness(0, 4, 0, 0),
                ColumnSpacing = 8,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                Children =
                {
                    leftLine,
                    titleBlock,
                    rightLine
                }
            };
        }

        private static Border BuildSeparatorLine()
        {
            var line = new Border
            {
                Height = 1,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 222, 226, 230)),
                VerticalAlignment = VerticalAlignment.Center
            };
            return line;
        }

        private FrameworkElement BuildFileRow(
            string label,
            string iconGlyph,
            TextBox editor,
            Action<Button>? configureAttachButton = null)
        {
            var grid = new Grid
            {
                ColumnSpacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center
            });

            var icon = new TextBlock
            {
                Text = iconGlyph,
                FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 1);
            grid.Children.Add(icon);

            Grid.SetColumn(editor, 2);
            grid.Children.Add(editor);

            var attachButton = BuildFileActionButton("\ue723", "Прикрепить");
            attachButton.Click += async (_, _) => await PickFilePathAsync(editor);
            configureAttachButton?.Invoke(attachButton);
            Grid.SetColumn(attachButton, 3);
            grid.Children.Add(attachButton);

            var openButton = BuildFileActionButton("\ue8a7", "Открыть");
            openButton.IsTabStop = false;
            openButton.Click += (_, _) => OpenFilePath(editor.Text);
            Grid.SetColumn(openButton, 4);
            grid.Children.Add(openButton);

            return grid;
        }

        private static Button BuildFileActionButton(string iconGlyph, string tooltip)
        {
            var button = new Button
            {
                Width = 24,
                Height = 24,
                MinWidth = 24,
                Padding = new Thickness(0),
                Content = new TextBlock
                {
                    Text = iconGlyph,
                    FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            ToolTipService.SetToolTip(button, tooltip);
            return button;
        }

        private async Task PickFilePathAsync(TextBox target)
        {
            try
            {
                var picker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };
                picker.FileTypeFilter.Add("*");

                if (App.CurrentWindow is not null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }

                var file = await picker.PickSingleFileAsync();
                if (file is not null)
                {
                    target.Text = file.Path;
                }
            }
            catch (Exception ex)
            {
                ShowErrorInfo($"Не удалось выбрать файл: {ex.Message}");
            }
        }

        private void OpenFilePath(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ShowErrorInfo("Путь к файлу не заполнен.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath.Trim(),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowErrorInfo($"Не удалось открыть файл: {ex.Message}");
            }
        }

        private static FrameworkElement BuildInlineCheckBox(CheckBox checkBox, string label)
        {
            checkBox.Content = null;
            checkBox.HorizontalAlignment = HorizontalAlignment.Left;
            checkBox.VerticalAlignment = VerticalAlignment.Center;
            checkBox.MinHeight = 0;
            checkBox.MinWidth = 0;
            checkBox.Padding = new Thickness(0);
            checkBox.Margin = new Thickness(0);

            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 3,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    checkBox,
                    new TextBlock
                    {
                        Text = label,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };
        }

        private static UIElement BuildPlaceholder(string text)
        {
            return new Border
            {
                Padding = new Thickness(8),
                Child = new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }

        private string BuildDialogTitle()
        {
            if (_isCreateMode)
            {
                return "Создание контракта";
            }

            var name = GetText(_contract, "name");
            return string.IsNullOrWhiteSpace(name)
                ? "Редактирование контракта"
                : $"Редактирование контракта {name}";
        }

        private sealed record TaskKindSelectOption(long? Id, string Code, string Label);

    }
}
