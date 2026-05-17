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

            EmitTrace(FormatQueryTrace("DATA QUERY INDEX", request));
            EmitTrace($"Page payload:{Environment.NewLine}{SerializeForTrace(request)}");
            var items = await PostAsync<DataQueryRequest, List<TItem>>("api/index", request, cancellationToken);
            return items;
        }

        public async Task<int> GetCountAsync(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var countRequest = BuildCountRequest(request);
            EmitTrace(FormatQueryTrace("DATA QUERY COUNT", countRequest));
            EmitTrace($"Count payload:{Environment.NewLine}{SerializeForTrace(countRequest)}");
            var payload = await PostForJsonAsync("api/count", countRequest, cancellationToken);
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

        private static DataQueryRequest BuildCountRequest(DataQueryRequest request)
        {
            return new DataQueryRequest
            {
                Model = request.Model,
                Preset = request.Preset,
                Filters = request.Filters
            };
        }

        private string SerializeForTrace(DataQueryRequest request)
        {
            return JsonSerializer.Serialize(request, SerializerOptions);
        }

        private static string FormatQueryTrace(string title, DataQueryRequest request)
        {
            var sorts = request.Sorts is null || request.Sorts.Count == 0
                ? "<none>"
                : string.Join(", ", request.Sorts);
            var filters = request.Filters is null ? "<none>" : "set";
            return $"{title} model={request.Model} preset={request.Preset ?? "<null>"} offset={request.Offset?.ToString() ?? "<null>"} limit={request.Limit?.ToString() ?? "<null>"} filters={filters} sorts={sorts}";
        }
    }
}
