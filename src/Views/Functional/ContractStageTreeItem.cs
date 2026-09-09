using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed class ContractStageTreeItem : INotifyPropertyChanged
{
    public ContractStageTreeItem(
        object content,
        IReadOnlyList<ContractStageTreeItem>? children = null,
        bool isExpanded = false,
        Thickness? contentMargin = null)
        : this(() => content, children, isExpanded, contentMargin)
    {
    }

    public ContractStageTreeItem(
        Func<object> contentFactory,
        IReadOnlyList<ContractStageTreeItem>? children = null,
        bool isExpanded = false,
        Thickness? contentMargin = null)
    {
        _contentFactory = contentFactory;
        Children = children ?? [];
        _isExpanded = isExpanded;
        ContentMargin = contentMargin ?? new Thickness(0);
    }

    private readonly Func<object> _contentFactory;

    public object Content => _contentFactory();

    public IReadOnlyList<ContractStageTreeItem> Children { get; }

    private bool _isExpanded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            ExpansionChanged?.Invoke(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    public event Action<bool>? ExpansionChanged;

    public Thickness ContentMargin { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshContent()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
    }
}
