using System.Collections.Generic;

namespace CbsContractsDesktopClient.Models.Data
{
    public sealed class DataQueryPage<TItem>
    {
        public required IReadOnlyList<TItem> Items { get; init; }

        public required int TotalCount { get; init; }
    }
}
