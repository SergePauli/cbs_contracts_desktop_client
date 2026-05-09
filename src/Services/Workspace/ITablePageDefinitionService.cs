using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services.References;

namespace CbsContractsDesktopClient.Services.Workspace
{
    public interface ITablePageDefinitionService
    {
        bool TryGetByRoute(string? route, out TablePageDefinition definition);

        Task SaveColumnWidthAsync(
            ReferenceTableColumnWidthSettings settings,
            CancellationToken cancellationToken = default);

        Task SaveSortAsync(
            ReferenceTableSortSettings settings,
            CancellationToken cancellationToken = default);

        Task SaveColumnLayoutAsync(
            ReferenceTableColumnLayoutSettings settings,
            CancellationToken cancellationToken = default);
    }
}
