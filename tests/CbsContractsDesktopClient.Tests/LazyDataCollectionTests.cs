using CbsContractsDesktopClient.Collections;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Services;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public class LazyDataCollectionTests
{
    [Fact]
    public async Task InitializeAsync_LoadsCountAndFirstPage()
    {
        var service = new FakeDataQueryService(Enumerable.Range(0, 8).Select(i => new TestItem
        {
            Id = i,
            Name = $"Item {i}"
        }).ToList());

        var collection = new LazyDataCollection<TestItem>(
            service,
            new LazyDataQuery
            {
                Model = "Status",
                Preset = "item",
                PageSize = 3
            },
            static () => new TestItem());

        await collection.InitializeAsync();

        Assert.Equal(8, collection.TotalCount);
        Assert.Equal(8, collection.Count);
        Assert.Equal(3, collection.LoadedCount);
        Assert.True(collection.HasMoreItems);
        Assert.Equal(0, collection[0].Id);
        Assert.Equal(2, collection[2].Id);
        Assert.NotNull(collection[3]);
        Assert.True(collection[3].IsPlaceholder);
    }

    [Fact]
    public async Task LoadMoreItemsAsync_AppendsNextPageUntilCollectionIsExhausted()
    {
        var service = new FakeDataQueryService(Enumerable.Range(0, 5).Select(i => new TestItem
        {
            Id = i,
            Name = $"Item {i}"
        }).ToList());

        var collection = new LazyDataCollection<TestItem>(
            service,
            new LazyDataQuery
            {
                Model = "Status",
                Preset = "item",
                PageSize = 2
            },
            static () => new TestItem());

        await collection.InitializeAsync();
        await collection.LoadMoreItemsAsync(2);
        await collection.LoadMoreItemsAsync(2);

        Assert.Equal(5, collection.Count);
        Assert.Equal(5, collection.LoadedCount);
        Assert.False(collection.HasMoreItems);
        Assert.Equal(4, collection[^1].Id);
    }

    private sealed class FakeDataQueryService : IDataQueryService
    {
        private readonly List<TestItem> _items;

        public FakeDataQueryService(List<TestItem> items)
        {
            _items = items;
        }

        public Task<IReadOnlyList<TItem>> GetDataAsync<TItem>(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            var offset = request.Offset ?? 0;
            var limit = request.Limit ?? _items.Count;

            var page = _items
                .Skip(offset)
                .Take(limit)
                .Cast<TItem>()
                .ToList();

            return Task.FromResult<IReadOnlyList<TItem>>(page);
        }

        public Task<int> GetCountAsync(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Count);
        }

        public async Task<DataQueryPage<TItem>> GetPageAsync<TItem>(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            return new DataQueryPage<TItem>
            {
                Items = await GetDataAsync<TItem>(request, cancellationToken),
                TotalCount = await GetCountAsync(request, cancellationToken)
            };
        }
    }

    private sealed class TestItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public bool IsPlaceholder => Id == 0 && string.IsNullOrEmpty(Name);
    }
}
