using Microsoft.UI.Xaml;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed class ContractStageTreeItem
{
    public ContractStageTreeItem(
        object content,
        IReadOnlyList<ContractStageTreeItem>? children = null,
        bool isExpanded = false,
        Thickness? contentMargin = null)
    {
        Content = content;
        Children = children ?? [];
        IsExpanded = isExpanded;
        ContentMargin = contentMargin ?? new Thickness(0);
    }

    public object Content { get; }

    public IReadOnlyList<ContractStageTreeItem> Children { get; }

    public bool IsExpanded { get; set; }

    public Thickness ContentMargin { get; }
}
