using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.ViewModels.Data;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public class LazyDataViewStateTests
{
    [Fact]
    public async Task SetFilterAsync_RebuildsQueryAndRefreshesCollection()
    {
        var service = new RecordingDataQueryService();
        var state = new LazyDataViewState<TestItem>(
            service,
            model: "Status",
            preset: "item",
            pageSize: 5,
            fieldMap: new Dictionary<string, string>
            {
                ["name"] = "name"
            },
            placeholderFactory: static () => new TestItem());

        await state.SetFilterAsync("name", DataFilterMode.Text, DataFilterMatchMode.Contains, "проект");

        Assert.NotNull(service.LastCountRequest);
        var filters = Assert.IsType<Dictionary<string, object?>>(service.LastCountRequest!.Filters);
        Assert.Equal("проект", filters["name__cnt"]);
    }

    [Fact]
    public async Task SetFilterAsync_UsesNumericModeForNumericFilters()
    {
        var service = new RecordingDataQueryService();
        var state = new LazyDataViewState<TestItem>(
            service,
            model: "Status",
            preset: "item",
            pageSize: 5,
            fieldMap: new Dictionary<string, string>
            {
                ["id"] = "id"
            },
            placeholderFactory: static () => new TestItem());

        await state.SetFilterAsync("id", DataFilterMode.Numeric, DataFilterMatchMode.GreaterThanOrEqual, "10");

        Assert.NotNull(service.LastCountRequest);
        var filters = Assert.IsType<Dictionary<string, object?>>(service.LastCountRequest!.Filters);
        Assert.Equal(10m, filters["id__gte"]);
    }

    [Fact]
    public async Task SetSortAsync_RebuildsSortsAndRefreshesCollection()
    {
        var service = new RecordingDataQueryService(totalCount: 1);
        var state = new LazyDataViewState<TestItem>(
            service,
            model: "Status",
            preset: "item",
            pageSize: 5,
            fieldMap: new Dictionary<string, string>
            {
                ["id"] = "id"
            },
            placeholderFactory: static () => new TestItem());

        await state.SetSortAsync("id", DataSortDirection.Ascending);

        Assert.NotNull(service.LastDataRequest);
        Assert.Equal(["id asc"], service.LastDataRequest!.Sorts);
    }

    private sealed class RecordingDataQueryService : IDataQueryService
    {
        private readonly int _totalCount;

        public RecordingDataQueryService(int totalCount = 0)
        {
            _totalCount = totalCount;
        }

        public DataQueryRequest? LastDataRequest { get; private set; }

        public DataQueryRequest? LastCountRequest { get; private set; }

        public Task<IReadOnlyList<TItem>> GetDataAsync<TItem>(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            LastDataRequest = request;
            return Task.FromResult<IReadOnlyList<TItem>>([]);
        }

        public Task<int> GetCountAsync(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            LastCountRequest = request;
            return Task.FromResult(_totalCount);
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
    }
}
