using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.Services.Shell;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;
using static CbsContractsDesktopClient.Shared.Dialogs.StageContractDeadlineDialogOptions;
using static CbsContractsDesktopClient.Shared.Dialogs.StageContractStatusDialogControls;
using static CbsContractsDesktopClient.Shared.Formatting.AppFormatters;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed class ContractInfoDialog : ContentDialog
{
    private readonly ContractInfoView _view = new();
    private StageEditState _stage;
    private ContractEditState _contract;
    private readonly RevisionEditState _contractRevision;
    private readonly ContractInfoAuditSummary _auditSummary;
    private StageEditDialogNavigationState? _navigationState;
    private readonly Func<StageEditDialogNavigationDirection, Task<StageEditDialogNavigationResult?>>? _navigateAsync;

    public ContractInfoDialog(
        StageEditState stage,
        ContractEditState contract,
        RevisionEditState contractRevision,
        ContractInfoAuditSummary auditSummary,
        StageEditDialogNavigationState? navigationState = null,
        Func<StageEditDialogNavigationDirection, Task<StageEditDialogNavigationResult?>>? navigateAsync = null)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(contractRevision);
        ArgumentNullException.ThrowIfNull(auditSummary);

        _stage = stage;
        _contract = contract;
        _contractRevision = contractRevision;
        _auditSummary = auditSummary;
        _navigationState = navigationState;
        _navigateAsync = navigateAsync;
        _view.PreviousButton.Click += StageNavigationButton_Click;
        _view.NextButton.Click += StageNavigationButton_Click;

        PrimaryButtonText = string.Empty;
        SecondaryButtonText = string.Empty;
        CloseButtonText = string.Empty;
        DefaultButton = ContentDialogButton.None;
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("ms-appx:///Microsoft.UI.Xaml/DensityStyles/Compact.xaml")
        });
        Resources["ContentDialogMinWidth"] = 780d;
        Resources["ContentDialogMaxWidth"] = 860d;
        Content = BuildContent();
        DialogChrome.Apply(this, "Информация о контракте");
    }

    private UIElement BuildContent()
    {
        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 640,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _view
        };

        InitializeNavigationButton(_view.PreviousButton, "Предыдущий этап", StageEditDialogNavigationDirection.Previous);
        InitializeNavigationButton(_view.NextButton, "Следующий этап", StageEditDialogNavigationDirection.Next);
        RenderContent();
        return scrollViewer;
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

    private void RenderContent()
    {
        RenderContractSummary();
        RenderStageSummary();
        RenderStageDetails();
    }

    private void RenderContractSummary()
    {
        var contract = _contract;
        _view.ContractTitleSlot.Content = BuildDialogSectionTitle(contract.GetSectionTitle());
        _view.ExternalNumberValue.Text = FormatSummaryValue(contract.ExternalNumber);
        _view.ContragentValue.Text = FormatSummaryValue(contract.ContragentName);
        _view.ContractCostValue.Text = FormatSummaryValue(FormatMoney(contract.Cost));
        _view.ContractStatusSlot.Content = BuildStatusBadge(
            contract.Status.Name ?? string.Empty,
            contract.Status.Id,
            horizontalAlignment: HorizontalAlignment.Left);
        _view.SignedAtValue.Text = FormatSummaryValue(FormatDisplayDate(contract.SignedAt));
        _view.ContractClosedAtValue.Text = FormatSummaryValue(FormatDisplayDate(contract.ClosedAt));
        _view.GovernmentalValue.Text = FormatSummaryValue(FormatBoolean(contract.Governmental));
        _view.MultiStageValue.Text = contract.IsMultiStage ? "Да" : "Нет";
        _view.ContractRevisionPresentValue.Text = FormatSummaryValue(FormatBoolean(_contractRevision.IsPresent));
        _view.ContractCreatedAtRow.Visibility = _auditSummary.IsImported ? Visibility.Collapsed : Visibility.Visible;
        _view.ContractCreatedAtLabel.Text = _auditSummary.CreatedAtLabel;
        _view.ContractCreatedAtValue.Text = FormatSummaryValue(_auditSummary.CreatedAt);
        _view.ContractCreatedByLabel.Text = _auditSummary.CreatedByLabel;
        _view.ContractCreatedByValue.Text = FormatSummaryValue(_auditSummary.CreatedBy);
        _view.ContractClosedByValue.Text = FormatSummaryValue(_auditSummary.ClosedBy);
    }

    private void RenderStageSummary()
    {
        _view.StageTitleValue.Inlines.Clear();
        var title = _stage.GetSectionTitle(_contract);
        var accentText = _stage.GetSectionTitleAmount(_contract);
        if (string.IsNullOrWhiteSpace(accentText))
        {
            _view.StageTitleValue.Text = title;
        }
        else
        {
            _view.StageTitleValue.Text = string.Empty;
            _view.StageTitleValue.Inlines.Add(new Run { Text = title + " " });
            _view.StageTitleValue.Inlines.Add(new Run
            {
                Text = accentText,
                Foreground = Application.Current.Resources["ShellAccentBrush"] as Brush
            });
        }

        var state = _navigationState ?? new StageEditDialogNavigationState(false, false);
        var hasNavigation = state.CanPrevious || state.CanNext;
        _view.PreviousButton.Visibility = hasNavigation ? Visibility.Visible : Visibility.Collapsed;
        _view.NextButton.Visibility = hasNavigation ? Visibility.Visible : Visibility.Collapsed;
        _view.PreviousButton.IsEnabled = state.CanPrevious;
        _view.NextButton.IsEnabled = state.CanNext;

        _view.PrepaymentValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.PrepaymentAt));
        _view.PaymentValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.PaymentAt));
        _view.InvoiceValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.InvoiceAt));
        _view.FundedValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.FundedAt));
        _view.CompletedValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.CompletedAt));
        _view.RideOutValue.Text = FormatSummaryValue(FormatFlagDate(_stage.IsRideOut, _stage.RideOutAt));
        _view.SendedValue.Text = FormatSummaryValue(FormatFlagDate(_stage.IsSended, _stage.SendedAt));
    }

    private void RenderStageDetails()
    {
        _view.StageStatusSlot.Content = BuildStatusBadge(
            _stage.Status.Name ?? string.Empty,
            _stage.Status.Id,
            horizontalAlignment: HorizontalAlignment.Left);
        _view.StageTaskKindValue.Text = FormatSummaryValue(FormatTaskKind());
        _view.StageCostValue.Text = FormatSummaryValue(FormatMoney(_stage.Cost));
        _view.StartAtValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.StartAt));
        _view.DeadlineKindValue.Text = FormatSummaryValue(FindOptionLabel(DeadlineKindOptions(), _stage.DeadlineKind));
        _view.DurationValue.Text = FormatSummaryValue(FormatNumber(_stage.Duration));
        _view.DeadlineAtValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.DeadlineAt));
        _view.PaymentDeadlineKindValue.Text = FormatSummaryValue(FindOptionLabel(PaymentDeadlineKindOptions(), _stage.PaymentDeadlineKind));
        _view.PaymentDurationValue.Text = FormatSummaryValue(FormatNumber(_stage.PaymentDuration));
        _view.PaymentDeadlineAtValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.PaymentDeadlineAt));
        _view.ClosedAtValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.ClosedAt));
        _view.PerformersValue.Text = FormatSummaryValue(FormatPerformers());
        _view.RegistryValue.Text = FormatSummaryValue(FormatRegistry());
        _view.TasksValue.Text = FormatSummaryValue(FormatTasks());
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

            _stage = result.Stage;
            _contract = result.Contract
                ?? throw new InvalidOperationException("ContractInfoDialog.RequestNavigation: navigation result must contain contract edit state.");
            _navigationState = result.NavigationState;
            RenderContent();
        }
        catch (Exception ex)
        {
            _view.ErrorValue.Text = $"ContractInfoDialog.RequestNavigation: {ex.Message}";
            _view.ErrorValue.Visibility = Visibility.Visible;
        }
    }

    private void StageNavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: StageEditDialogNavigationDirection direction })
        {
            RequestNavigation(direction);
        }
    }

    private string FormatTaskKind()
    {
        var code = _stage.TaskKind.Code?.Trim();
        var name = _stage.TaskKind.Name?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return name ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(name) ? code : $"{code} - {name}";
    }

    private string FormatTasks()
    {
        return string.Join(", ", _stage.Tasks
            .Select(static task => task.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name)));
    }

    private string FormatPerformers()
    {
        return string.Join(", ", _stage.Performers
            .OrderBy(static performer => performer.Priority ?? int.MaxValue)
            .Select(static performer => performer.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name)));
    }

    private string FormatRegistry()
    {
        if (_stage.RegistryQuarter is null && _stage.RegistryYear is null)
        {
            return string.Empty;
        }

        return $"Квартал {_stage.RegistryQuarter?.ToString() ?? "-"}, год {_stage.RegistryYear?.ToString() ?? "-"}";
    }

    private static string FormatSummaryValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static string FormatNumber(int? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private static string FindOptionLabel(IReadOnlyList<EnumSelectOption> options, string? key)
    {
        return options.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))?.Label
            ?? string.Empty;
    }
}
