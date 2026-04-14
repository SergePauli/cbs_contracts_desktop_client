using System.ComponentModel;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Collections
{
    public sealed class CbsVirtualTableRows<TItem> : ICbsTableRows<TItem>
        where TItem : class
    {
        private readonly LazyDataCollection<TItem> _items;

        public CbsVirtualTableRows(LazyDataCollection<TItem> items)
        {
            _items = items;
            ((INotifyPropertyChanged)_items).PropertyChanged += OnItemsPropertyChanged;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public IReadOnlyList<TItem> Items => _items;

        public bool SupportsVirtualScrolling => true;

        public bool IsLoading => _items.IsLoading;

        public string ErrorMessage => _items.ErrorMessage;

        public int TotalCount => _items.TotalCount;

        public int LoadedCount => _items.LoadedCount;

        public int ResidentCount => _items.ResidentCount;

        public bool HasMoreItems => _items.HasMoreItems;

        public string LastCountRequestJson => _items.LastCountRequestJson;

        public string LastPageRequestJson => _items.LastPageRequestJson;

        public string TraceLog => _items.TraceLog;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => _items.InitializeAsync(cancellationToken);

        public Task RefreshAsync(CancellationToken cancellationToken = default)
            => _items.RefreshAsync(cancellationToken);

        public async Task<uint> LoadMoreAsync(uint requestedCount = 0, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _items.LoadMoreItemsAsync(requestedCount);
            return result.Count;
        }

        private void OnItemsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e.PropertyName));
            if (e.PropertyName == nameof(LazyDataCollection<TItem>.LoadedCount)
                || e.PropertyName == nameof(LazyDataCollection<TItem>.ResidentCount)
                || e.PropertyName == nameof(LazyDataCollection<TItem>.TotalCount))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Items)));
            }
        }
    }
}
