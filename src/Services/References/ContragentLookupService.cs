using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class ContragentLookupService : IContragentLookupService
    {
        private readonly IDataQueryService _dataQueryService;

        public ContragentLookupService(IDataQueryService dataQueryService)
        {
            _dataQueryService = dataQueryService;
        }

        public async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadOptionsAsync(
            string searchText,
            CancellationToken cancellationToken = default)
        {
            var normalizedSearchText = searchText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedSearchText))
            {
                return [];
            }

            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = "Contragent",
                    Preset = "item",
                    Filters = new Dictionary<string, object?>
                    {
                        ["org.name_or_org.full_name__cnt"] = normalizedSearchText
                    },
                    Sorts = ["org.name asc"],
                    Limit = 25
                },
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(static row => new CbsTableFilterOptionDefinition
                {
                    Value = row.GetValue("id"),
                    Label =
                        row.GetValue("full_name")?.ToString()
                        ?? row.GetValue("name")?.ToString()
                        ?? string.Empty
                })
                .Where(static option => option.Value is not null && !string.IsNullOrWhiteSpace(option.Label))
                .DistinctBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}
