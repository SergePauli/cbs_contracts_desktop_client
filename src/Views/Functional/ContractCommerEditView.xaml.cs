using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed partial class ContractCommerEditView : UserControl
{
    public ContractCommerEditView()
    {
        InitializeComponent();
    }

    public ContentControl TaskKindSlot => TaskKindHost;
    public ContentControl YearSlot => YearHost;
    public ContentControl OrderSlot => OrderHost;
    public ContentControl ContragentSlot => ContragentHost;
    public ContentControl StatusSlot => StatusHost;
    public ContentControl SignedAtSlot => SignedAtHost;
    public ContentControl CostSlot => CostHost;
    public ContentControl CommentSlot => CommentHost;
    public ContentControl ExtAgreementSlot => ExtAgreementHost;
    public ContentControl MultiStageSlot => MultiStageHost;
    public ContentControl ResetChangesSlot => ResetChangesHost;

    public TabView Tabs => ContractTabs;
    public TabViewItem ContractTabItem => ContractTab;
    public TabViewItem StagesTabItem => StagesTab;
    public TabViewItem RevisionsTabItem => RevisionsTab;
    public ContentControl GovernmentalSlot => GovernmentalHost;
    public ContentControl RevisionPresentSlot => RevisionPresentHost;
    public ContentControl RevisionDescriptionSlot => RevisionDescriptionHost;
    public ContentControl ExternalNumberSlot => ExternalNumberHost;
    public ContentControl DeadlineAtSlot => DeadlineAtHost;
    public ContentControl ClosedAtSlot => ClosedAtHost;
    public ContentControl DocLinkRowSlot => DocLinkRowHost;
    public ContentControl ScanLinkRowSlot => ScanLinkRowHost;
    public ContentControl ProtocolLinkRowSlot => ProtocolLinkRowHost;
    public ContentControl StagesTabSlot => StagesTabContentHost;
    public ContentControl RevisionsTabSlot => RevisionsTabContentHost;
}
