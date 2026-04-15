using CbsContractsDesktopClient.Models.References;
using System.Threading;
using System.Threading.Tasks;

namespace CbsContractsDesktopClient.Services.References
{
    public interface IReferenceDefinitionService
    {
        bool TryGetByRoute(string? route, out ReferenceDefinition definition);

        Task SaveColumnWidthAsync(
            ReferenceTableColumnWidthSettings settings,
            CancellationToken cancellationToken = default);
    }
}
