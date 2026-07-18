using Microsoft.UI.Xaml.Controls;

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

    public ContentControl ExternalNumberSlot => ExternalNumberHost;
    public ContentControl InvoiceAtSlot => InvoiceAtHost;
    public ContentControl PaymentAtSlot => PaymentAtHost;
    public ContentControl PrepaymentAtSlot => PrepaymentAtHost;
    public ContentControl FundedAtSlot => FundedAtHost;
    public ContentControl CommentSlot => CommentHost;
    public ContentControl CommentListSlot => CommentListHost;
}
