namespace CbsContractsDesktopClient.Views.Functional;

public sealed class ContractStageTreeItem
{
    public ContractStageTreeItem(
        object content,
        IReadOnlyList<ContractStageTreeItem>? children = null,
        bool isExpanded = false)
    {
        Content = content;
        Children = children ?? [];
        IsExpanded = isExpanded;
    }

    public object Content { get; }

    public IReadOnlyList<ContractStageTreeItem> Children { get; }

    public bool IsExpanded { get; set; }
}
