// Defines the shared model mutation boundary used by reference and functional table workflows.
using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.Mutations
{
    public interface IModelMutationService
    {
        Task<ReferenceDataRow> CreateAsync(
            string model,
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken = default);

        Task<ReferenceDataRow> UpdateAsync(
            string model,
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken = default);

        Task<ReferenceDataRow> DeleteAsync(
            string model,
            long id,
            CancellationToken cancellationToken = default);
    }
}
