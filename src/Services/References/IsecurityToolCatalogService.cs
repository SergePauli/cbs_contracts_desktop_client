// Owns reusable IsecurityTool catalog values consumed by reference and order workflows.
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Data;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class IsecurityToolCatalogService
    {
        private readonly IDataQueryService _dataQueryService;

        public IsecurityToolCatalogService(IDataQueryService dataQueryService)
        {
            _dataQueryService = dataQueryService;
        }

        public async Task<IReadOnlyList<string>> LoadUnitOptionsAsync(CancellationToken cancellationToken = default)
        {
            var units = await _dataQueryService.GetDataAsync<string>(
                new DataQueryRequest
                {
                    Model = "IsecurityTool",
                    UniqueBy = "unit"
                },
                cancellationToken);

            if (units.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException("IsecurityTool.unit unique query вернул пустое значение.");
            }

            return units;
        }

        public async Task<IReadOnlyList<IsecurityToolCatalogItem>> LoadToolOptionsAsync(CancellationToken cancellationToken = default)
        {
            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest { Model = "IsecurityTool", Preset = "card", Sorts = ["name asc"], Limit = 1000 },
                cancellationToken);
            return rows.Select(row => new IsecurityToolCatalogItem(
                JsonDataReader.TryGetLong(row.GetValue("id")) ?? throw new InvalidOperationException("IsecurityTool.card не содержит id."),
                row.GetValue("name")?.ToString() ?? throw new InvalidOperationException("IsecurityTool.card не содержит name."),
                row.GetValue("default_cost") is { } cost ? Convert.ToDecimal(cost) : null)).ToList();
        }

        public async Task<IReadOnlyList<IsecurityToolCatalogItem>> SearchToolOptionsAsync(
            string searchText,
            CancellationToken cancellationToken = default)
        {
            var normalized = searchText.Trim();
            if (normalized.Length == 0)
            {
                return [];
            }

            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = "IsecurityTool",
                    Preset = "card",
                    Filters = new Dictionary<string, object?> { ["name__cnt"] = normalized },
                    Sorts = ["name asc"],
                    Limit = 25
                },
                cancellationToken);
            return rows.Select(row => new IsecurityToolCatalogItem(
                JsonDataReader.TryGetLong(row.GetValue("id"))
                    ?? throw new InvalidOperationException("IsecurityTool.card не содержит id."),
                JsonDataReader.TryGetText(row, "name")
                    ?? throw new InvalidOperationException("IsecurityTool.card не содержит name."),
                row.GetValue("default_cost") is { } cost ? Convert.ToDecimal(cost) : null)).ToList();
        }
    }
}
