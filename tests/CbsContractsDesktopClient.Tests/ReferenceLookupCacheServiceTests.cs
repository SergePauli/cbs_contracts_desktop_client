using System.Text.Json;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ReferenceLookupCacheServiceTests
{
    [Fact]
    public async Task GetOptionsAsync_UsesOwnershipCardPresetAndCachesResult()
    {
        var dataQueryService = new FakeDataQueryService([
            CreateRow(("id", 10), ("name", "ООО"), ("full_name", "Общество с ограниченной ответственностью"), ("okopf", "12300"))
        ]);
        var service = new ReferenceLookupCacheService(dataQueryService);

        var first = await service.GetOptionsAsync("Ownership");
        var second = await service.GetOptionsAsync("Ownership");

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(10L, first[0].Value);
        Assert.Equal("ООО", first[0].Label);
        Assert.Single(dataQueryService.Requests);
        Assert.Equal("Ownership", dataQueryService.Requests[0].Model);
        Assert.Equal("card", dataQueryService.Requests[0].Preset);
    }

    [Fact]
    public async Task FindOwnershipAsync_MatchesByIdAndCodeFromCachedRows()
    {
        var dataQueryService = new FakeDataQueryService([
            CreateRow(("id", 10), ("name", "ООО"), ("full_name", "Общество с ограниченной ответственностью"), ("okopf", "12300")),
            CreateRow(("id", 11), ("name", "АО"), ("full_name", "Акционерное общество"), ("code", "12247"))
        ]);
        var service = new ReferenceLookupCacheService(dataQueryService);

        var byId = await service.FindOwnershipAsync(10L, null);
        var byCode = await service.FindOwnershipAsync(null, "12247");

        Assert.NotNull(byId);
        Assert.Equal("Общество с ограниченной ответственностью", byId.FullName);
        Assert.Equal("12300", byId.Code);
        Assert.NotNull(byCode);
        Assert.Equal("Акционерное общество", byCode.FullName);
        Assert.Equal("12247", byCode.Code);
        Assert.Single(dataQueryService.Requests);
    }

    [Fact]
    public async Task Invalidate_RemovesCachedModelPresets()
    {
        var dataQueryService = new FakeDataQueryService([
            CreateRow(("id", 1), ("name", "Департамент"))
        ]);
        var service = new ReferenceLookupCacheService(dataQueryService);

        await service.GetOptionsAsync("Department", "item");
        service.Invalidate("Department");
        await service.GetOptionsAsync("Department", "item");

        Assert.Equal(2, dataQueryService.Requests.Count);
    }

    [Fact]
    public async Task GetOptionsAsync_DoesNotCachePositionLookup()
    {
        var dataQueryService = new FakeDataQueryService([
            CreateRow(("id", 1), ("name", "Юрист"))
        ]);
        var service = new ReferenceLookupCacheService(dataQueryService);

        await service.GetOptionsAsync("Position", "item");
        await service.GetOptionsAsync("Position", "item");

        Assert.Equal(2, dataQueryService.Requests.Count);
    }

    private static ReferenceDataRow CreateRow(params (string Key, object? Value)[] values)
    {
        return new ReferenceDataRow
        {
            Values = values.ToDictionary(
                static value => value.Key,
                static value => JsonSerializer.SerializeToElement(value.Value))
        };
    }

    private sealed class FakeDataQueryService : IDataQueryService
    {
        private readonly IReadOnlyList<ReferenceDataRow> _rows;

        public FakeDataQueryService(IReadOnlyList<ReferenceDataRow> rows)
        {
            _rows = rows;
        }

        public List<DataQueryRequest> Requests { get; } = [];

        public Task<IReadOnlyList<TItem>> GetDataAsync<TItem>(
            DataQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult<IReadOnlyList<TItem>>(_rows.Cast<TItem>().ToList());
        }

        public Task<int> GetCountAsync(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_rows.Count);
        }

        public async Task<DataQueryPage<TItem>> GetPageAsync<TItem>(
            DataQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            return new DataQueryPage<TItem>
            {
                Items = await GetDataAsync<TItem>(request, cancellationToken),
                TotalCount = await GetCountAsync(request, cancellationToken)
            };
        }
    }
}
