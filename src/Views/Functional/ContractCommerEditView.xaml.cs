using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed partial class ContractCommerEditView : UserControl
{
    private readonly Dictionary<ContractStageTreeItem, ContentControl> _stageContentHosts = [];

    public ContractCommerEditView()
    {
        InitializeComponent();
    }

    private void StageContent_Loaded(object sender, RoutedEventArgs args)
        => AttachStageContent((ContentControl)sender);

    private void StageContent_Unloaded(object sender, RoutedEventArgs args)
        => DetachStageContent((ContentControl)sender);

    private void StageContent_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (sender.IsLoaded) AttachStageContent((ContentControl)sender);
    }

    private void AttachStageContent(ContentControl host)
    {
        DetachStageContent(host);
        // A recycled container may temporarily have no node.
        if (host.DataContext is not TreeViewNode node) return;
        var item = (ContractStageTreeItem)node.Content;
        if (_stageContentHosts.Remove(item, out var previousHost))
        {
            previousHost.Tag = null;
            previousHost.Content = null;
        }

        _stageContentHosts.Add(item, host);
        host.Tag = item;
        // Content is prepared before expansion; loading only transfers visual ownership.
        host.Content = item.Content;
    }

    private void DetachStageContent(ContentControl host)
    {
        if (host.Tag is ContractStageTreeItem item)
        {
            _stageContentHosts.Remove(item);
            host.Tag = null;
        }
        host.Content = null;
    }

    public ContentControl TaskKindSlot => TaskKindHost;
    public ContentControl YearSlot => YearHost;
    public ContentControl OrderSlot => OrderHost;
    public ContentControl ContragentSlot => ContragentHost;
    public ContentControl StatusSlot => StatusHost;
    public ContentControl SignedAtSlot => SignedAtHost;
    public ContentControl CostSlot => CostHost;
    public ContentControl CommentSlot => CommentHost;
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
    public ContentControl ContractResponsiblesSlot => ContractResponsiblesHost;
    public ContentControl DocLinkRowSlot => DocLinkRowHost;
    public ContentControl ScanLinkRowSlot => ScanLinkRowHost;
    public ContentControl ProtocolLinkRowSlot => ProtocolLinkRowHost;
    public TreeView StagesTree => StagesTabContentHost;

    public Button ToggleStagesExpansion => ToggleStagesExpansionButton;
    public FontIcon StagesExpansionIcon => ToggleStagesExpansionIcon;
    public ContentControl RevisionsTabSlot => RevisionsTabContentHost;
}
