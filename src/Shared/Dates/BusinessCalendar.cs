namespace CbsContractsDesktopClient.Shared.Dates;

public static class BusinessCalendar
{
    public static DateTimeOffset AddWorkingDaysToDate(
        DateTimeOffset current,
        int days,
        IReadOnlyList<HolidayCalendarDay>? holidays = null)
    {
        var result = AddWorkingDaysToDate(current.Date, days, holidays);
        return new DateTimeOffset(result, current.Offset);
    }

    public static DateTime AddWorkingDaysToDate(
        DateTime current,
        int days,
        IReadOnlyList<HolidayCalendarDay>? holidays = null)
    {
        var result = current.Date.AddDays(-1);
        var daysCount = 0;
        var calendar = holidays ?? [];

        while (daysCount < days)
        {
            result = result.AddDays(1);
            var calendarDay = calendar.FirstOrDefault(day => day.Contains(result));

            var isWeekend = result.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var isWorkingDay = (!isWeekend || calendarDay?.IsWorkingDay == true)
                && (calendarDay is null || calendarDay.IsWorkingDay);

            if (isWorkingDay)
            {
                daysCount++;
            }
        }

        return result;
    }
}
