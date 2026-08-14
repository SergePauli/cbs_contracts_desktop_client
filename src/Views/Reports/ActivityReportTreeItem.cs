namespace CbsContractsDesktopClient.Views.Reports;

public sealed class ActivityReportTreeItem
{
    private bool _isExpanded;

    public ActivityReportTreeItem(object content, IReadOnlyList<ActivityReportTreeItem>? children = null, bool isExpanded = false)
    {
        Content = content;
        Children = children ?? [];
        _isExpanded = isExpanded;
    }

    public object Content { get; }

    public IReadOnlyList<ActivityReportTreeItem> Children { get; }

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
            ExpansionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? ExpansionChanged;

}
