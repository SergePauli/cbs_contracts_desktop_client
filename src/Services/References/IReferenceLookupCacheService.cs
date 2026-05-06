using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.References
{
    public interface IReferenceLookupCacheService
    {
        Task<IReadOnlyList<ReferenceLookupItem>> GetItemsAsync(
            string model,
            string? preset = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<CbsTableFilterOptionDefinition>> GetOptionsAsync(
            string model,
            string? preset = null,
            CancellationToken cancellationToken = default);

        Task<ReferenceLookupItem?> FindByIdAsync(
            string model,
            object? id,
            string? preset = null,
            CancellationToken cancellationToken = default);

        Task<ReferenceLookupItem?> FindOwnershipAsync(
            object? id,
            string? code,
            CancellationToken cancellationToken = default);

        void Invalidate(string model, string? preset = null);
    }
}
