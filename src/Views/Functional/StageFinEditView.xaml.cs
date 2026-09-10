using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Pauli.WinUiKit.Controls;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed partial class StageFinEditView : UserControl
{
    public StageFinEditView()
    {
        InitializeComponent();
    }

    public TextBlock ContractTitleValue => ContractTitleText;
    public TextBlock ContragentValue => ContragentText;
    public TextBlock ContractCostValue => ContractCostText;
    public Border ContractStatusBadgeValue => ContractStatusBadge;
    public TextBlock ContractStatusValue => ContractStatusText;
    public TextBlock SignedAtValue => SignedAtText;

    public Button PreviousButton => PreviousStageButton;
    public Button NextButton => NextStageButton;
    public Run StageTitleMain => StageTitleMainRun;
    public Run StageTitleAmount => StageTitleAmountRun;
    public Border StatusBadge => StageStatusBadge;
    public TextBlock StatusText => StageStatusText;
    public TextBlock StartAtValue => StartAtText;
    public TextBlock PaymentKindValue => PaymentKindText;
    public TextBlock PaymentDeadlineAtValue => PaymentDeadlineAtText;
    public TextBlock DeadlineAtValue => DeadlineAtText;
    public TextBlock CompletedAtValue => CompletedAtText;
    public TextBlock RideOutValue => RideOutText;
    public TextBlock SendedValue => SendedText;

    public TextBox ExternalNumberInput => ExternalNumberEditor;
    public CalendarInput InvoiceAtInput => InvoiceAtEditor;
    public CalendarInput PaymentAtInput => PaymentAtEditor;
    public CalendarInput PrepaymentAtInput => PrepaymentAtEditor;
    public CalendarInput FundedAtInput => FundedAtEditor;
    public TextBox CommentInput => CommentEditor;
    public CommentBox CommentsBox => CommentsList;
}
