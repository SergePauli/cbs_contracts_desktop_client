using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Shared.Dates;
using static CbsContractsDesktopClient.Shared.Formatting.AppFormatters;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class HolidayRecalculationService : ApiServiceBase, IHolidayRecalculationService
    {
        private readonly SemaphoreSlim _holidayCalendarGate = new(1, 1);
        private IReadOnlyList<ReferenceDataRow>? _holidayCalendarCache;
        private IReadOnlyList<HolidayCalendarDay>? _holidayCalendarDaysCache;

        public HolidayRecalculationService(HttpClient httpClient, IUserService userService)
            : base(httpClient, userService)
        {
        }

        public async Task<IReadOnlyList<ReferenceDataRow>> GetHolidayCalendarAsync(CancellationToken cancellationToken = default)
        {
            await EnsureHolidayCalendarCacheAsync(cancellationToken);
            return _holidayCalendarCache ?? [];
        }

        public async Task<IReadOnlyList<HolidayCalendarDay>> GetHolidayCalendarDaysAsync(CancellationToken cancellationToken = default)
        {
            await EnsureHolidayCalendarCacheAsync(cancellationToken);
            return _holidayCalendarDaysCache ?? [];
        }

        private async Task EnsureHolidayCalendarCacheAsync(CancellationToken cancellationToken)
        {
            if (_holidayCalendarCache is not null && _holidayCalendarDaysCache is not null)
            {
                return;
            }

            await _holidayCalendarGate.WaitAsync(cancellationToken);
            try
            {
                if (_holidayCalendarCache is not null && _holidayCalendarDaysCache is not null)
                {
                    return;
                }

                var rows = await LoadHolidayCalendarAsync(cancellationToken);
                var days = rows
                    .Where(static row => !row.IsPlaceholder)
                    .Select(TryCreateHolidayCalendarDay)
                    .Where(static item => item is not null)
                    .Cast<HolidayCalendarDay>()
                    .ToList();

                _holidayCalendarCache = rows;
                _holidayCalendarDaysCache = days;
            }
            finally
            {
                _holidayCalendarGate.Release();
            }
        }

        private Task<IReadOnlyList<ReferenceDataRow>> LoadHolidayCalendarAsync(CancellationToken cancellationToken)
        {
            var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["limit"] = 500,
                ["data_set"] = "card",
                ["q"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sorts"] = new[] { "begin_at desc" }
                }
            };

            return PostAsync<Dictionary<string, object?>, IReadOnlyList<ReferenceDataRow>>(
                "model/Holiday",
                request,
                cancellationToken);
        }

        public Task<IReadOnlyList<ReferenceDataRow>> GetAffectedStagesAsync(
            string intervalStart,
            string intervalEnd,
            CancellationToken cancellationToken = default)
        {
            var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["data_set"] = "card",
                ["q"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["g"] = new object?[]
                    {
                        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["m"] = "or",
                            ["status_id_in"] = new object?[] { 2, 4 },
                            ["status_id_null"] = true
                        },
                        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["m"] = "or",
                            ["g"] = new object?[]
                            {
                                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["m"] = "and",
                                    ["start_at_lt"] = intervalEnd,
                                    ["deadline_at_gt"] = intervalStart,
                                    ["deadline_kind_in"] = new object?[] { 3, 4 }
                                },
                                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["m"] = "and",
                                    ["payment_deadline_kind_eq"] = 2,
                                    ["funded_at_lt"] = intervalEnd,
                                    ["payment_deadline_at_gt"] = intervalStart
                                }
                            }
                        }
                    }
                },
                ["sorts"] = new[] { "id desc" }
            };

            return PostAsync<Dictionary<string, object?>, IReadOnlyList<ReferenceDataRow>>(
                "model/Stage",
                request,
                cancellationToken);
        }

        private static HolidayCalendarDay? TryCreateHolidayCalendarDay(ReferenceDataRow row)
        {
            var beginAt = ParseDate(row.GetValue("begin_at"));
            if (beginAt is null)
            {
                return null;
            }

            var endAt = ParseDate(row.GetValue("end_at"));
            return new HolidayCalendarDay(
                beginAt.Value.Date,
                endAt?.Date,
                TryGetBool(row.GetValue("work")) ?? false);
        }
    }
}
