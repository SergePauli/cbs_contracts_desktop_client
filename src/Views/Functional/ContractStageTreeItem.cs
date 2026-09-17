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
        Content = content;
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

    public object? Content { get; private set; }

    public void EnsureContent()
    {
        if (Content is not null) return;
        Content = _contentFactory();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
    }

    public void PrepareVisibleContent()
    {
        EnsureContent();
        if (IsExpanded)
        {
            PrepareChildren();
        }
    }

    public void PrepareChildren()
    {
        foreach (var child in Children)
        {
            child.PrepareVisibleContent();
        }
    }

    public IReadOnlyList<ContractStageTreeItem> Children { get; }

    private Visibility _visibility = Visibility.Visible;

    public Visibility Visibility
    {
        get => _visibility;
        set
        {
            if (_visibility == value)
            {
                return;
            }

            _visibility = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visibility)));
        }
    }

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    public Thickness ContentMargin { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateHeader(string text)
    {
        EnsureContent();
        ((Microsoft.UI.Xaml.Controls.TextBlock)Content!).Text = text;
    }
}
