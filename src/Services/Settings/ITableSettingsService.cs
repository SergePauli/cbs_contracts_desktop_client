// Defines shared persistence for table UI settings across references and functional tables.
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Settings;

namespace CbsContractsDesktopClient.Services.Settings
{
    public interface ITableSettingsService
    {
        LocalTableSettings? GetTableSettings(string route);

        Task SaveColumnWidthAsync(
            TableColumnWidthSettings settings,
            CancellationToken cancellationToken = default);

        Task SaveSortAsync(
            TableSortSettings settings,
            CancellationToken cancellationToken = default);

        Task SaveColumnLayoutAsync(
            TableColumnLayoutSettings settings,
            CancellationToken cancellationToken = default);

        Task SaveFiltersAsync(
            string route,
            IReadOnlyList<DataFilterCriterion> filters,
            CancellationToken cancellationToken = default);
    }
}
