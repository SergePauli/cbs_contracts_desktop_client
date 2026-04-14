using System.ComponentModel;

namespace CbsContractsDesktopClient.Models.Table
{
    public interface ICbsTableRows<out TItem> : INotifyPropertyChanged
        where TItem : class
    {
        IReadOnlyList<TItem> Items { get; }

        bool SupportsVirtualScrolling { get; }

        bool IsLoading { get; }

        string ErrorMessage { get; }

        int TotalCount { get; }

        int LoadedCount { get; }

        int ResidentCount { get; }

        bool HasMoreItems { get; }

        string LastCountRequestJson { get; }

        string LastPageRequestJson { get; }

        string TraceLog { get; }

        Task InitializeAsync(CancellationToken cancellationToken = default);

        Task RefreshAsync(CancellationToken cancellationToken = default);

        Task<uint> LoadMoreAsync(uint requestedCount = 0, CancellationToken cancellationToken = default);
    }
}
