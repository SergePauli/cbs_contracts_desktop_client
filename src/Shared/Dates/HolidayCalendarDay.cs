namespace CbsContractsDesktopClient.Shared.Dates;

public sealed record HolidayCalendarDay(DateTime BeginDate, DateTime? EndDate, bool IsWorkingDay)
{
    public bool Contains(DateTime date)
    {
        var current = date.Date;
        var begin = BeginDate.Date;
        var end = EndDate?.Date;

        return begin == current
            || end == current
            || (end is DateTime endDate && endDate > current && begin < current);
    }
}
