using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.References
{
    public interface IFnsContragentService
    {
        Task<FnsResponse> GetByReqAsync(
            string req,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FnsContragentLookupResult>> SearchByReqAsync(
            string req,
            string? kpp = null,
            string? apiKey = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FnsContragentLookupResult>> ReadDataAsync(
            FnsResponse response,
            string? kpp = null,
            CancellationToken cancellationToken = default);
    }
}
