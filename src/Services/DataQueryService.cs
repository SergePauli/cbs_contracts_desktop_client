using System.Text.Json;
using CbsContractsDesktopClient.Models.Data;

namespace CbsContractsDesktopClient.Services
{
    public sealed class DataQueryService : ApiServiceBase, IDataQueryService
    {
        public DataQueryService(HttpClient httpClient, IUserService userService)
            : base(httpClient, userService)
        {
        }

        public async Task<IReadOnlyList<TItem>> GetDataAsync<TItem>(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var items = await PostAsync<DataQueryRequest, List<TItem>>("api/index", request, cancellationToken);
            return items;
        }

        public async Task<int> GetCountAsync(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var payload = await PostForJsonAsync("api/count", request, cancellationToken);
            return NormalizeCount(payload);
        }

        public async Task<DataQueryPage<TItem>> GetPageAsync<TItem>(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var dataTask = GetDataAsync<TItem>(request, cancellationToken);
            var countTask = GetCountAsync(request, cancellationToken);

            await Task.WhenAll(dataTask, countTask);

            return new DataQueryPage<TItem>
            {
                Items = dataTask.Result,
                TotalCount = countTask.Result
            };
        }

        private static int NormalizeCount(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt32(out var count) => count,
                JsonValueKind.String when int.TryParse(value.GetString(), out var count) => count,
                JsonValueKind.Object when value.TryGetProperty("count", out var nestedCount) => NormalizeCount(nestedCount),
                _ => 0
            };
        }
    }
}
