using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Shared.Dates;

namespace CbsContractsDesktopClient.Services.References
{
    public interface IHolidayRecalculationService
    {
        Task<IReadOnlyList<TableDataRow>> GetHolidayCalendarAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<HolidayCalendarDay>> GetHolidayCalendarDaysAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TableDataRow>> GetAffectedStagesAsync(
            string intervalStart,
            string intervalEnd,
            CancellationToken cancellationToken = default);
    }
}
