using CbsContractsDesktopClient.Shared.Dates;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class BusinessCalendarTests
{
    [Fact]
    public void AddWorkingDaysToDate_CountsCurrentDateLikeWebClient()
    {
        var result = BusinessCalendar.AddWorkingDaysToDate(new DateTime(2026, 5, 11), 1);

        Assert.Equal(new DateTime(2026, 5, 11), result);
    }

    [Fact]
    public void AddWorkingDaysToDate_SkipsRegularWeekend()
    {
        var result = BusinessCalendar.AddWorkingDaysToDate(new DateTime(2026, 5, 15), 2);

        Assert.Equal(new DateTime(2026, 5, 18), result);
    }

    [Fact]
    public void AddWorkingDaysToDate_SkipsHolidayInterval()
    {
        var holidays = new[]
        {
            new HolidayCalendarDay(new DateTime(2026, 5, 12), new DateTime(2026, 5, 13), IsWorkingDay: false)
        };

        var result = BusinessCalendar.AddWorkingDaysToDate(new DateTime(2026, 5, 11), 3, holidays);

        Assert.Equal(new DateTime(2026, 5, 15), result);
    }

    [Fact]
    public void AddWorkingDaysToDate_CountsTransferredWeekendWorkday()
    {
        var holidays = new[]
        {
            new HolidayCalendarDay(new DateTime(2026, 5, 16), null, IsWorkingDay: true)
        };

        var result = BusinessCalendar.AddWorkingDaysToDate(new DateTime(2026, 5, 15), 2, holidays);

        Assert.Equal(new DateTime(2026, 5, 16), result);
    }
}
