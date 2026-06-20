using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Shared.Dates;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pauli.WinUiKit.Controls;
using Windows.System;
using static CbsContractsDesktopClient.Shared.Dates.BusinessCalendar;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;
using static CbsContractsDesktopClient.Shared.Dialogs.StageContractStatusDialogControls;
using static CbsContractsDesktopClient.Shared.Formatting.AppFormatters;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed class StageFinEditDialog : AppEditDialog
{
    private StageEditState _stage;
    private ContractEditState? _contract;
    private readonly IHolidayRecalculationService _holidayRecalculationService;
    private StageEditDialogNavigationState? _navigationState;
    private readonly Func<StageEditDialogNavigationDirection, Task<StageEditDialogNavigationResult?>>? _navigateAsync;
    private readonly int? _profileId;
    private readonly CalendarInput _invoiceAtEditor = new();
    private readonly CalendarInput _prepaymentAtEditor = new();
    private readonly CalendarInput _paymentAtEditor = new();
    private readonly CalendarInput _fundedAtEditor = new();
    private DateTimeOffset? _startAt;
    private DateTimeOffset? _deadlineAt;
    private DateTimeOffset? _paymentDeadlineAt;
    private readonly TextBox _externalNumberBox = new();
    private readonly TextBox _commentBox = new();
    private readonly StageFinEditView _view = new();
    private IReadOnlyList<HolidayCalendarDay> _holidays = [];
    private bool _isApplyingBusinessLogic;
    private bool _businessLogicHandlersAttached;
    private bool _isFunded;

    public StageFinEditDialog(
        StageEditState stage,
        ContractEditState? contract,
        IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions,
        int? profileId,
        StageEditDialogNavigationState? navigationState = null,
        Func<StageEditDialogNavigationDirection, Task<StageEditDialogNavigationResult?>>? navigateAsync = null)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(statusOptions);

        _stage = stage;
        _contract = contract;
        _profileId = profileId;
        _navigationState = navigationState;
        _navigateAsync = navigateAsync;
        _holidayRecalculationService = App.Services.GetRequiredService<IHolidayRecalculationService>();
        _isFunded = stage.IsFunded;
        _view.PreviousButton.Click += StageNavigationButton_Click;
        _view.NextButton.Click += StageNavigationButton_Click;
        Resources["ContentDialogMinWidth"] = 670d;
        Resources["ContentDialogMaxWidth"] = 770d;
        Content = BuildContent();
        DialogChrome.Apply(this, _stage.GetEditDialogTitle());
        Loaded += StageFinEditDialog_Loaded;
    }

    public long Id => _stage.Id;

    public bool HasContractExternalNumberChanges()
    {
        return _contract?.IsExternalNumberChanged(_externalNumberBox.Text) == true;
    }

    public IReadOnlyDictionary<string, object?> BuildContractExternalNumberPayload()
    {
        if (_contract is null)
        {
            throw new InvalidOperationException("Не удалось определить контракт для сохранения внешнего номера.");
        }

        return _contract.BuildExternalNumberPayload(_externalNumberBox.Text);
    }

    public IReadOnlyDictionary<string, object?> BuildPayload()
    {
        SyncStageStateFromEditors();
        return StageFinEditPayloadBuilder.BuildForUpdate(_stage, _commentBox.Text, _profileId);
    }

    public override bool Validate()
    {
        ShowErrorInfo(string.Empty);
        return true;
    }

    private async void StageFinEditDialog_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= StageFinEditDialog_Loaded;

        try
        {
            _holidays = await _holidayRecalculationService.GetHolidayCalendarDaysAsync();
            ApplyBusinessLogicAfterPaymentChange();
            ApplyBusinessLogicAfterFundedAtChange();
            UpdateCalculatedSummary();
            FocusExternalNumberBox();
        }
        catch
        {
            _holidays = [];
            UpdateCalculatedSummary();
            FocusExternalNumberBox();
        }
    }

    private UIElement BuildContent()
    {
        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 620,
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
        _view.ContragentValue.Text = FormatSummaryValue(_contract?.ContragentName ?? string.Empty);
        _view.ContractCostValue.Text = FormatSummaryValue(FormatMoney(_contract?.Cost));
        _view.ContractStatusSlot.Content = BuildStatusBadge(
            RequireContract().Status.Name!,
            RequireContract().Status.Id,
            horizontalAlignment: HorizontalAlignment.Left);
        _view.SignedAtValue.Text = FormatSummaryValue(FormatDisplayDate(_contract?.SignedAt));
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
        _view.ExternalNumberSlot.Content = _externalNumberBox;
        _view.InvoiceAtSlot.Content = _invoiceAtEditor;
        _view.PaymentAtSlot.Content = _paymentAtEditor;
        _view.PrepaymentAtSlot.Content = _prepaymentAtEditor;
        _view.FundedAtSlot.Content = _fundedAtEditor;
        _view.CommentSlot.Content = _commentBox;
        _commentBox.PlaceholderText = _profileId is null
            ? "Комментарий недоступен: не получен profile_id пользователя"
            : "Комментарий";
        _commentBox.IsEnabled = _profileId is not null;
        AttachBusinessLogicHandlers();
        ConfigureTabChain();
    }

    private void ConfigureTabChain()
    {
        _externalNumberBox.TabIndex = 0;
        _invoiceAtEditor.TabIndex = 1;
        _paymentAtEditor.TabIndex = 2;
        _prepaymentAtEditor.TabIndex = 3;
        _fundedAtEditor.TabIndex = 4;
        _commentBox.TabIndex = 5;

        _externalNumberBox.PreviewKeyDown += ExternalNumberBox_PreviewKeyDown;
        _invoiceAtEditor.OnTab = (_, args) => FocusDateEditor(_paymentAtEditor, args);
        _paymentAtEditor.OnTab = (_, args) => FocusDateEditor(_prepaymentAtEditor, args);
        _prepaymentAtEditor.OnTab = (_, args) => FocusDateEditor(_fundedAtEditor, args);
        _fundedAtEditor.OnTab = (_, args) => FocusTextBox(_commentBox, args);
    }

    private void FocusExternalNumberBox()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _externalNumberBox.Focus(FocusState.Programmatic);
            _externalNumberBox.Select(0, _externalNumberBox.Text.Length);
        });
    }

    private void ExternalNumberBox_PreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Tab)
        {
            return;
        }

        FocusDateEditor(_invoiceAtEditor, args);
    }

    private void FocusDateEditor(CalendarInput editor, KeyRoutedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() => editor.FocusInput(FocusState.Programmatic));
        args.Handled = true;
    }

    private void FocusTextBox(TextBox textBox, KeyRoutedEventArgs args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            textBox.Focus(FocusState.Programmatic);
            textBox.Select(textBox.Text.Length, 0);
        });
        args.Handled = true;
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
        _view.StageStatusSlot.Content = BuildStatusBadge(
            _stage.Status.Name ?? string.Empty,
            _stage.Status.Id,
            horizontalAlignment: HorizontalAlignment.Left);
        _view.PaymentKindValue.Text = FormatSummaryValue(ResolvePaymentDeadlineKindLabel());
        _view.CompletedAtValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.CompletedAt));
        _view.RideOutValue.Text = FormatSummaryValue(FormatFlagDate(_stage.IsRideOut, _stage.RideOutAt));
        _view.SendedValue.Text = FormatSummaryValue(FormatFlagDate(_stage.IsSended, _stage.SendedAt));
        UpdateCalculatedSummary();
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
            _externalNumberBox.Text = _contract?.ExternalNumber ?? string.Empty;
            _invoiceAtEditor.Date = _stage.InvoiceAt;
            _prepaymentAtEditor.Date = _stage.PrepaymentAt;
            _paymentAtEditor.Date = _stage.PaymentAt;
            _fundedAtEditor.Date = _stage.FundedAt;
            _startAt = _stage.StartAt;
            _deadlineAt = _stage.DeadlineAt;
            _paymentDeadlineAt = _stage.PaymentDeadlineAt;
        }
        finally
        {
            _isApplyingBusinessLogic = false;
        }

        UpdateCalculatedSummary();
        ApplyBusinessLogicAfterPaymentChange();
        ApplyBusinessLogicAfterFundedAtChange();
    }

    private static string FormatSummaryValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
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
        var oldIsFunded = _isFunded;

        try
        {
            _stage = result.Stage;
            _contract = result.Contract;
            _navigationState = result.NavigationState;
            _isFunded = _stage.IsFunded;
            RenderStageContent();
        }
        catch
        {
            _stage = oldStage;
            _contract = oldContract;
            _navigationState = oldNavigationState;
            _isFunded = oldIsFunded;
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

    private void AttachBusinessLogicHandlers()
    {
        if (_businessLogicHandlersAttached)
        {
            return;
        }

        _businessLogicHandlersAttached = true;
        _prepaymentAtEditor.DateChanged += (_, _) => ApplyBusinessLogicAfterPaymentChange();
        _paymentAtEditor.DateChanged += (_, _) => ApplyBusinessLogicAfterPaymentChange();
        _fundedAtEditor.DateChanged += (_, _) => ApplyBusinessLogicAfterFundedAtChange();
    }

    private void ApplyBusinessLogicAfterPaymentChange()
    {
        if (_isApplyingBusinessLogic)
        {
            return;
        }

        _isApplyingBusinessLogic = true;
        try
        {
            ApplyDeadlineFromPaymentBusinessLogic();
            UpdateCalculatedSummary();
        }
        finally
        {
            _isApplyingBusinessLogic = false;
        }
    }

    private void ApplyBusinessLogicAfterFundedAtChange()
    {
        if (_isApplyingBusinessLogic)
        {
            return;
        }

        _isApplyingBusinessLogic = true;
        try
        {
            _isFunded = _fundedAtEditor.Date is not null;
            ApplyPaymentDeadlineBusinessLogic();
            UpdateCalculatedSummary();
        }
        finally
        {
            _isApplyingBusinessLogic = false;
        }
    }

    private void ApplyDeadlineFromPaymentBusinessLogic()
    {
        var payment = _prepaymentAtEditor.Date ?? _paymentAtEditor.Date;
        var deadlineKind = _stage.DeadlineKind;
        var duration = _stage.Duration;
        if (payment is null || duration is null)
        {
            return;
        }

        if (string.Equals(deadlineKind, "calendar_prepayment", StringComparison.OrdinalIgnoreCase))
        {
            _deadlineAt = payment.Value.Date.AddDays(duration.Value);
        }
        else if (string.Equals(deadlineKind, "working_prepayment", StringComparison.OrdinalIgnoreCase))
        {
            _deadlineAt = AddWorkingDaysToDate(payment.Value, duration.Value, _holidays);
        }
        else
        {
            return;
        }

        _startAt = payment;
    }

    private void ApplyPaymentDeadlineBusinessLogic()
    {
        var paymentKind = _stage.PaymentDeadlineKind;
        var duration = _stage.PaymentDuration;
        var fundedAt = _fundedAtEditor.Date;
        if (fundedAt is null || duration is null)
        {
            return;
        }

        if (string.Equals(paymentKind, "c_days", StringComparison.OrdinalIgnoreCase))
        {
            _paymentDeadlineAt = fundedAt.Value.Date.AddDays(duration.Value);
        }
        else if (string.Equals(paymentKind, "w_days", StringComparison.OrdinalIgnoreCase))
        {
            _paymentDeadlineAt = AddWorkingDaysToDate(fundedAt.Value, duration.Value, _holidays);
        }
    }

    private void UpdateCalculatedSummary()
    {
        _view.StartAtValue.Text = FormatSummaryValue(FormatDisplayDate(_startAt));
        _view.DeadlineAtValue.Text = FormatSummaryValue(FormatDisplayDate(_deadlineAt));
        _view.PaymentDeadlineAtValue.Text = FormatSummaryValue(FormatDisplayDate(_paymentDeadlineAt));
    }

    private void SyncStageStateFromEditors()
    {
        _stage.PaymentAt = _paymentAtEditor.Date;
        _stage.PrepaymentAt = _prepaymentAtEditor.Date;
        _stage.InvoiceAt = _invoiceAtEditor.Date;
        _stage.FundedAt = _fundedAtEditor.Date;
        _stage.IsFunded = _isFunded;
        _stage.StartAt = _startAt;
        _stage.DeadlineAt = _deadlineAt;
        _stage.PaymentDeadlineAt = _paymentDeadlineAt;
    }

    private string ResolvePaymentDeadlineKindLabel()
    {
        var key = _stage.PaymentDeadlineKind;
        var label = StageContractDeadlineDialogOptions.PaymentDeadlineKindOptions()
            .FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
            ?.Label
            ?? "-";
        var duration = _stage.PaymentDuration;
        if (duration is int days
            && !string.IsNullOrWhiteSpace(key)
            && !string.Equals(key, "c_plan", StringComparison.OrdinalIgnoreCase))
        {
            return $"{label}, {days} дн.";
        }

        return label;
    }

}
