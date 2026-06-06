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
using Microsoft.UI.Xaml.Media;
using Pauli.WinUiKit.Controls;
using static CbsContractsDesktopClient.Shared.Dates.BusinessCalendar;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;
using static CbsContractsDesktopClient.Shared.Dialogs.StageContractStatusDialogControls;
using static CbsContractsDesktopClient.Shared.Formatting.AppFormatters;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed class StageFinEditDialog : AppEditDialog
{
    private readonly StageEditState _stage;
    private readonly ContractEditState? _contract;
    private readonly IHolidayRecalculationService _holidayRecalculationService;
    private readonly IReadOnlyList<CbsTableFilterOptionDefinition> _statusOptions;
    private readonly int? _profileId;
    private readonly string? _listKey;
    private readonly CalendarInput _invoiceAtEditor = new();
    private readonly CalendarInput _prepaymentAtEditor = new();
    private readonly CalendarInput _paymentAtEditor = new();
    private readonly CalendarInput _fundedAtEditor = new();
    private readonly TextBlock _startAtText = BuildDynamicSummaryText();
    private readonly TextBlock _deadlineAtText = BuildDynamicSummaryText();
    private readonly TextBlock _paymentDeadlineAtText = BuildDynamicSummaryText();
    private DateTimeOffset? _startAt;
    private DateTimeOffset? _deadlineAt;
    private DateTimeOffset? _paymentDeadlineAt;
    private readonly TextBox _externalNumberBox = new();
    private readonly TextBox _commentBox = new();
    private IReadOnlyList<HolidayCalendarDay> _holidays = [];
    private bool _isApplyingBusinessLogic;
    private bool _businessLogicHandlersAttached;
    private bool _isFunded;

    public StageFinEditDialog(
        StageEditState stage,
        ContractEditState? contract,
        IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions,
        int? profileId)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(statusOptions);

        _stage = stage;
        _contract = contract;
        _statusOptions = statusOptions;
        _profileId = profileId;
        _holidayRecalculationService = App.Services.GetRequiredService<IHolidayRecalculationService>();
        Id = stage.Id;
        _listKey = stage.ListKey;
        _isFunded = stage.IsFunded;
        Resources["ContentDialogMinWidth"] = 720d;
        Resources["ContentDialogMaxWidth"] = 860d;
        Content = BuildContent();
        DialogChrome.Apply(this, _stage.GetEditDialogTitle());
        Loaded += StageFinEditDialog_Loaded;
    }

    public long Id { get; }

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
        }
        catch
        {
            _holidays = [];
            UpdateCalculatedSummary();
        }
    }

    private UIElement BuildContent()
    {
        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 620,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var stack = new StackPanel { Spacing = 14 };
        scrollViewer.Content = stack;
        stack.Children.Add(BuildSummaryPanel());
        stack.Children.Add(BuildEditorsGrid());

        var body = new Grid
        {
            MinWidth = 700,
            MaxWidth = 840,
            Children = { scrollViewer }
        };
        return BuildEditContent(body);
    }

    private UIElement BuildSummaryPanel()
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(BuildDialogSectionTitle(RequireContract().GetSectionTitle()));

        var contractGrid = new Grid { ColumnSpacing = 18, RowSpacing = 6 };
        contractGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contractGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel { Spacing = 6 };
        left.Children.Add(BuildSummaryLine("Контрагент", _contract?.ContragentName ?? string.Empty));
        left.Children.Add(BuildAccentSummaryLine("Стоимость", FormatMoney(_contract?.Cost)));

        var right = new StackPanel { Spacing = 6 };
        right.Children.Add(BuildSummaryElement("Статус", BuildStatusBadge(
            RequireContract().Status.Name!,
            RequireContract().Status.Id,
            horizontalAlignment: HorizontalAlignment.Left)));
        right.Children.Add(BuildSummaryLine("Дата подписания", FormatDisplayDate(_contract?.SignedAt)));

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
        stageLeft.Children.Add(BuildSummaryElement("Статус", BuildStatusBadge(
            _stage.Status.Name ?? string.Empty,
            _stage.Status.Id,
            horizontalAlignment: HorizontalAlignment.Left)));
        stageLeft.Children.Add(BuildSummaryElement("Дата начала", _startAtText));
        stageLeft.Children.Add(BuildSummaryLine("Режим оплаты", ResolvePaymentDeadlineKindLabel()));
        stageLeft.Children.Add(BuildSummaryElement("Срок оплаты", _paymentDeadlineAtText));

        var stageRight = new StackPanel { Spacing = 6 };
        stageRight.Children.Add(BuildSummaryElement("Срок завершения", _deadlineAtText));
        stageRight.Children.Add(BuildSummaryLine("Дата завершения", FormatDisplayDate(_stage.CompletedAt)));
        stageRight.Children.Add(BuildSummaryLine("Выезд", FormatFlagDate(_stage.IsRideOut, _stage.RideOutAt)));
        stageRight.Children.Add(BuildSummaryLine("Отправка", FormatFlagDate(_stage.IsSended, _stage.SendedAt)));

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

    private UIElement BuildEditorsGrid()
    {
        _externalNumberBox.Text = _contract?.ExternalNumber ?? string.Empty;
        _invoiceAtEditor.Date = _stage.InvoiceAt;
        _prepaymentAtEditor.Date = _stage.PrepaymentAt;
        _paymentAtEditor.Date = _stage.PaymentAt;
        _fundedAtEditor.Date = _stage.FundedAt;
        _startAt = _stage.StartAt;
        _deadlineAt = _stage.DeadlineAt;
        _paymentDeadlineAt = _stage.PaymentDeadlineAt;
        UpdateCalculatedSummary();
        AttachBusinessLogicHandlers();

        var grid = new Grid { ColumnSpacing = 18, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var left = new StackPanel { Spacing = 10 };
        var right = new StackPanel { Spacing = 10 };

        left.Children.Add(BuildLabeledControl("Внешний номер", _externalNumberBox));
        left.Children.Add(BuildLabeledControl("Дата предоплаты", _prepaymentAtEditor));
        left.Children.Add(BuildLabeledControl("Дата бух. закрытия", _fundedAtEditor));

        right.Children.Add(BuildLabeledControl("Дата счёта", _invoiceAtEditor));
        right.Children.Add(BuildLabeledControl("Дата оплаты", _paymentAtEditor));

        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);

        var root = new StackPanel { Spacing = 12 };
        root.Children.Add(grid);
        _commentBox.PlaceholderText = _profileId is null
            ? "Комментарий недоступен: не получен profile_id пользователя"
            : "Комментарий";
        _commentBox.IsEnabled = _profileId is not null;
        root.Children.Add(BuildLabeledControl("Комментарий", _commentBox));
        return root;
    }

    private ContractEditState RequireContract()
    {
        return _contract
            ?? throw new InvalidOperationException("Stage edit dialog requires selected contract edit state.");
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
        _startAtText.Text = FormatDisplayDate(_startAt);
        _deadlineAtText.Text = FormatDisplayDate(_deadlineAt);
        _paymentDeadlineAtText.Text = FormatDisplayDate(_paymentDeadlineAt);
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
