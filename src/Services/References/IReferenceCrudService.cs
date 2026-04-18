using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.References
{
    public interface IReferenceCrudService
    {
        Task<ReferenceDataRow> CreateAsync(
            ReferenceDefinition definition,
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken = default);

        Task<ReferenceDataRow> UpdateAsync(
            ReferenceDefinition definition,
            IReadOnlyDictionary<string, object?> payload,
            CancellationToken cancellationToken = default);

        Task<ReferenceDataRow> DeleteAsync(
            ReferenceDefinition definition,
            long id,
            CancellationToken cancellationToken = default);
    }
}
