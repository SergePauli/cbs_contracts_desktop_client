using CbsContractsDesktopClient.Models.Data;

namespace CbsContractsDesktopClient.Services
{
    public interface IDataQueryService
    {
        Task<IReadOnlyList<TItem>> GetDataAsync<TItem>(DataQueryRequest request, CancellationToken cancellationToken = default);

        Task<int> GetCountAsync(DataQueryRequest request, CancellationToken cancellationToken = default);

        Task<DataQueryPage<TItem>> GetPageAsync<TItem>(DataQueryRequest request, CancellationToken cancellationToken = default);
    }
}
