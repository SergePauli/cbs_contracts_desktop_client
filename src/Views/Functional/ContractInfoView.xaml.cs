using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed partial class ContractInfoView : UserControl
{
    public ContractInfoView()
    {
        InitializeComponent();
    }

    public ContentControl ContractTitleSlot => ContractTitleHost;
    public TextBlock ExternalNumberValue => ExternalNumberText;
    public TextBlock ContragentValue => ContragentText;
    public TextBlock ContractCostValue => ContractCostText;
    public ContentControl ContractStatusSlot => ContractStatusHost;
    public TextBlock SignedAtValue => SignedAtText;
    public TextBlock ContractClosedAtValue => ContractClosedAtText;
    public TextBlock GovernmentalValue => GovernmentalText;
    public TextBlock MultiStageValue => MultiStageText;
    public TextBlock ContractRevisionPresentValue => ContractRevisionPresentText;
    public Grid ContractCreatedAtRow => ContractCreatedAtGrid;
    public TextBlock ContractCreatedAtLabel => ContractCreatedAtLabelText;
    public TextBlock ContractCreatedAtValue => ContractCreatedAtText;
    public TextBlock ContractCreatedByLabel => ContractCreatedByLabelText;
    public TextBlock ContractCreatedByValue => ContractCreatedByText;
    public TextBlock ContractClosedByValue => ContractClosedByText;

    public Button PreviousButton => PreviousStageButton;
    public Button NextButton => NextStageButton;
    public TextBlock StageTitleValue => StageTitleText;
    public TextBlock PrepaymentValue => PrepaymentText;
    public TextBlock PaymentValue => PaymentText;
    public TextBlock InvoiceValue => InvoiceText;
    public TextBlock FundedValue => FundedText;
    public TextBlock CompletedValue => CompletedText;
    public TextBlock RideOutValue => RideOutText;
    public TextBlock SendedValue => SendedText;

    public ContentControl StageStatusSlot => StageStatusHost;
    public TextBlock StageTaskKindValue => StageTaskKindText;
    public TextBlock StageCostValue => StageCostText;
    public TextBlock StartAtValue => StartAtText;
    public TextBlock DeadlineKindValue => DeadlineKindText;
    public TextBlock DurationValue => DurationText;
    public TextBlock DeadlineAtValue => DeadlineAtText;
    public TextBlock PaymentDeadlineKindValue => PaymentDeadlineKindText;
    public TextBlock PaymentDurationValue => PaymentDurationText;
    public TextBlock PaymentDeadlineAtValue => PaymentDeadlineAtText;
    public TextBlock ClosedAtValue => ClosedAtText;
    public TextBlock PerformersValue => PerformersText;
    public TextBlock RegistryValue => RegistryText;
    public TextBlock TasksValue => TasksText;
    public TextBlock ErrorValue => ErrorText;
}
