using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed partial class StageCommerEditView : UserControl
{
    public StageCommerEditView()
    {
        InitializeComponent();
    }

    public ContentControl ContractTitleSlot => ContractTitleHost;
    public TextBlock ExternalNumberValue => ExternalNumberText;
    public TextBlock ContragentValue => ContragentText;
    public TextBlock ContractCostValue => ContractCostText;
    public ContentControl ContractStatusSlot => ContractStatusHost;
    public TextBlock SignedAtValue => SignedAtText;
    public TextBlock GovernmentalValue => GovernmentalText;

    public Button PreviousButton => PreviousStageButton;
    public Button NextButton => NextStageButton;
    public TextBlock StageTitleValue => StageTitleText;
    public TextBlock PrepaymentValue => PrepaymentText;
    public TextBlock PaymentValue => PaymentText;
    public TextBlock FundedValue => FundedText;
    public TextBlock CompletedValue => CompletedText;
    public TextBlock RideOutValue => RideOutText;
    public TextBlock SendedValue => SendedText;

    public ContentControl DeadlineKindSlot => DeadlineKindHost;
    public ContentControl DurationSlot => DurationHost;
    public ContentControl DeadlineAtSlot => DeadlineAtHost;
    public ContentControl CostSlot => CostHost;
    public ContentControl PaymentDeadlineKindSlot => PaymentDeadlineKindHost;
    public ContentControl PaymentDurationSlot => PaymentDurationHost;
    public ContentControl PaymentDeadlineAtSlot => PaymentDeadlineAtHost;
    public ContentControl StatusSlot => StatusHost;
    public ContentControl StartAtSlot => StartAtHost;
    public ContentControl ClosedAtSlot => ClosedAtHost;
    public ContentControl TasksSlot => TasksHost;
    public ContentControl CommentSlot => CommentHost;
}
