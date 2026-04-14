using System.Collections.Generic;

namespace CbsContractsDesktopClient.Models.Data
{
    public sealed class LazyDataQuery
    {
        public required string Model { get; init; }

        public string? Preset { get; init; }

        public object? Filters { get; init; }

        public IReadOnlyList<string>? Sorts { get; init; }

        public int PageSize { get; init; } = 50;

        public DataQueryRequest CreatePageRequest(int offset, int limit)
        {
            return new DataQueryRequest
            {
                Model = Model,
                Preset = Preset,
                Filters = Filters,
                Sorts = Sorts,
                Offset = offset,
                Limit = limit
            };
        }

        public DataQueryRequest CreateCountRequest()
        {
            return new DataQueryRequest
            {
                Model = Model,
                Preset = Preset,
                Filters = Filters,
                Sorts = Sorts
            };
        }
    }
}
