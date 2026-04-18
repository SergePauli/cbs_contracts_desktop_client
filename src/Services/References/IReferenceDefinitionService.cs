using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Data;
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

        Task SaveSortAsync(
            ReferenceTableSortSettings settings,
            CancellationToken cancellationToken = default);
    }
}
