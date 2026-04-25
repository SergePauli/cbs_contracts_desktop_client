using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class HolidayRecalculationService : ApiServiceBase, IHolidayRecalculationService
    {
        public HolidayRecalculationService(HttpClient httpClient, IUserService userService)
            : base(httpClient, userService)
        {
        }

        public Task<IReadOnlyList<ReferenceDataRow>> GetHolidayCalendarAsync(CancellationToken cancellationToken = default)
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
    }
}
