using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.References
{
    public interface IHolidayRecalculationService
    {
        Task<IReadOnlyList<ReferenceDataRow>> GetHolidayCalendarAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ReferenceDataRow>> GetAffectedStagesAsync(
            string intervalStart,
            string intervalEnd,
            CancellationToken cancellationToken = default);
    }
}
