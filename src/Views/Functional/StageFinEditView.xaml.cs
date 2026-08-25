using Microsoft.UI.Xaml.Controls;
using Pauli.WinUiKit.Controls;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed partial class StageFinEditView : UserControl
{
    public StageFinEditView()
    {
        InitializeComponent();
    }

    public ContentControl ContractTitleSlot => ContractTitleHost;
    public TextBlock ContragentValue => ContragentText;
    public TextBlock ContractCostValue => ContractCostText;
    public ContentControl ContractStatusSlot => ContractStatusHost;
    public TextBlock SignedAtValue => SignedAtText;

    public Button PreviousButton => PreviousStageButton;
    public Button NextButton => NextStageButton;
    public TextBlock StageTitleValue => StageTitleText;
    public ContentControl StageStatusSlot => StageStatusHost;
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
