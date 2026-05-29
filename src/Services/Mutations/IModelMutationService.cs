// Defines the shared model mutation boundary used by reference and functional table workflows.
using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.Mutations
{
    public interface IModelMutationService
    {
        Task<TableDataRow> CreateAsync(
            string model,
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken = default);

        Task<TableDataRow> UpdateAsync(
            string model,
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken = default);

        Task<TableDataRow> DeleteAsync(
            string model,
            long id,
            CancellationToken cancellationToken = default);
    }
}
