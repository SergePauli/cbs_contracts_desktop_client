using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Collections
{
    public sealed class CbsCollectionTableRows<TItem> : ICbsTableRows<TItem>
        where TItem : class
    {
        private readonly ObservableCollection<TItem> _items;

        public CbsCollectionTableRows(IEnumerable<TItem>? items = null)
        {
            _items = items is null ? [] : new ObservableCollection<TItem>(items);
            _items.CollectionChanged += OnCollectionChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public IReadOnlyList<TItem> Items => _items;

        public bool SupportsVirtualScrolling => false;

        public bool IsLoading => false;

        public string ErrorMessage => string.Empty;

        public int TotalCount => _items.Count;

        public int LoadedCount => _items.Count;

        public int ResidentCount => _items.Count;

        public bool HasMoreItems => false;

        public string LastCountRequestJson => string.Empty;

        public string LastPageRequestJson => string.Empty;

        public string TraceLog => string.Empty;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<uint> LoadMoreAsync(uint requestedCount = 0, CancellationToken cancellationToken = default)
            => Task.FromResult(0u);

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoadedCount)));
        }
    }
}
