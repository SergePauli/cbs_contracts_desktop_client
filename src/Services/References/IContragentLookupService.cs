using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.References
{
    public interface IContragentLookupService
    {
        Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadOptionsAsync(
            string searchText,
            CancellationToken cancellationToken = default);
    }
}
