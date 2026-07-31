using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed partial class StageOziEditView : UserControl
{
    public StageOziEditView()
    {
        InitializeComponent();
    }

    public ContentControl ContractTitleSlot => ContractTitleHost;
    public TextBlock ExternalNumberValue => ExternalNumberText;
    public TextBlock ContragentValue => ContragentText;
    public TextBlock SignedAtValue => SignedAtText;
    public ContentControl ContractStatusSlot => ContractStatusHost;
    public TextBlock ContractClosedAtValue => ContractClosedAtText;
    public TextBlock ContractCostValue => ContractCostText;
    public TextBlock ContractKindValue => ContractKindText;

    public Button PreviousButton => PreviousStageButton;
    public Button NextButton => NextStageButton;
    public TextBlock StageTitleValue => StageTitleText;
    public TextBlock StartAtValue => StartAtText;
    public TextBlock PrepaymentValue => PrepaymentText;
    public TextBlock DeadlineAtValue => DeadlineAtText;
    public TextBlock TasksValue => TasksText;

    public ContentControl PerformersSlot => PerformersHost;
    public ContentControl RideOutCheckSlot => RideOutCheckHost;
    public ContentControl RideOutAtSlot => RideOutAtHost;
    public ContentControl SendedCheckSlot => SendedCheckHost;
    public ContentControl SendedAtSlot => SendedAtHost;
    public ContentControl RegistryCheckSlot => RegistryCheckHost;
    public ContentControl RegistryQuarterSlot => RegistryQuarterHost;
    public ContentControl RegistryYearSlot => RegistryYearHost;
    public ContentControl StatusSlot => StatusHost;
    public ContentControl CompletedAtSlot => CompletedAtHost;
    public ContentControl ClosedAtSlot => ClosedAtHost;
    public ContentControl BranchesSlot => BranchesHost;
}
