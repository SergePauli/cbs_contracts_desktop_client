using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Services.Settings;

namespace CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions
{
    public interface ITablePageDefinitionService
    {
        bool TryGetByRoute(string? route, out TablePageDefinition definition);

        Task SaveColumnWidthAsync(
            TableColumnWidthSettings settings,
            CancellationToken cancellationToken = default);

        Task SaveSortAsync(
            TableSortSettings settings,
            CancellationToken cancellationToken = default);

        Task SaveFiltersAsync(
            string route,
            IReadOnlyList<DataFilterCriterion> filters,
            CancellationToken cancellationToken = default);

        Task SaveColumnLayoutAsync(
            TableColumnLayoutSettings settings,
            CancellationToken cancellationToken = default);
    }
}
