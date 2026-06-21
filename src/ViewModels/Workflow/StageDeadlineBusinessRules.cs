using CbsContractsDesktopClient.Shared.Dates;
using static CbsContractsDesktopClient.Shared.Dates.BusinessCalendar;

namespace CbsContractsDesktopClient.ViewModels.Workflow;

public sealed record StagePaymentBasedDeadlineCalculation(DateTimeOffset StartAt, DateTimeOffset DeadlineAt);

public static class StageDeadlineBusinessRules
{
    public const string DeadlineCalendarPlan = "calendar_plan";
    public const string DeadlineCalendarDays = "calendar_days";
    public const string DeadlineCalendarPrepayment = "calendar_prepayment";
    public const string DeadlineWorkingDays = "working_days";
    public const string DeadlineWorkingPrepayment = "working_prepayment";
    public const string PaymentCalendarPlan = "c_plan";
    public const string PaymentCalendarDays = "c_days";
    public const string PaymentWorkingDays = "w_days";

    public static bool IsDeadlineManualMode(string? deadlineKind)
    {
        return string.Equals(deadlineKind, DeadlineCalendarPlan, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPaymentBasedDeadlineMode(string? deadlineKind)
    {
        return string.Equals(deadlineKind, DeadlineCalendarPrepayment, StringComparison.OrdinalIgnoreCase)
            || string.Equals(deadlineKind, DeadlineWorkingPrepayment, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPaymentDeadlineManualMode(string? paymentDeadlineKind)
    {
        return string.Equals(paymentDeadlineKind, PaymentCalendarPlan, StringComparison.OrdinalIgnoreCase);
    }

    public static DateTimeOffset? ResolveInitialStart(
        bool isMultiStageContract,
        DateTimeOffset? currentStartAt,
        string? deadlineKind,
        DateTimeOffset? contractSignedAt,
        DateTimeOffset? paymentBaseDate)
    {
        if (IsPaymentBasedDeadlineMode(deadlineKind))
        {
            return paymentBaseDate;
        }

        if (isMultiStageContract || currentStartAt is not null)
        {
            return null;
        }

        return deadlineKind switch
        {
            DeadlineCalendarPlan or DeadlineCalendarDays or DeadlineWorkingDays => contractSignedAt,
            _ => null
        };
    }

    public static DateTimeOffset? CalculateDeadline(
        string? deadlineKind,
        DateTimeOffset? startAt,
        int? duration,
        IReadOnlyList<HolidayCalendarDay> holidays)
    {
        if (deadlineKind == DeadlineCalendarDays && duration is int calendarDuration && startAt is not null)
        {
            return startAt.Value.Date.AddDays(calendarDuration);
        }

        if (deadlineKind == DeadlineWorkingDays && duration is int workingDuration && startAt is not null)
        {
            return AddWorkingDaysToDate(startAt.Value, workingDuration, holidays);
        }

        if (deadlineKind == DeadlineCalendarPrepayment && duration is int calendarPrepaymentDuration && startAt is not null)
        {
            return startAt.Value.Date.AddDays(calendarPrepaymentDuration);
        }

        if (deadlineKind == DeadlineWorkingPrepayment && duration is int workingPrepaymentDuration && startAt is not null)
        {
            return AddWorkingDaysToDate(startAt.Value, workingPrepaymentDuration, holidays);
        }

        if ((duration is null || startAt is null) && !IsDeadlineManualMode(deadlineKind))
        {
            return null;
        }

        return null;
    }

    public static DateTimeOffset? CalculatePaymentDeadline(
        string? paymentDeadlineKind,
        DateTimeOffset? fundedAt,
        int? paymentDuration,
        IReadOnlyList<HolidayCalendarDay> holidays)
    {
        if (paymentDeadlineKind == PaymentCalendarDays && paymentDuration is int calendarDuration && fundedAt is not null)
        {
            return fundedAt.Value.Date.AddDays(calendarDuration);
        }

        if (paymentDeadlineKind == PaymentWorkingDays && paymentDuration is int workingDuration && fundedAt is not null)
        {
            return AddWorkingDaysToDate(fundedAt.Value, workingDuration, holidays);
        }

        return null;
    }

    public static StagePaymentBasedDeadlineCalculation? CalculatePaymentBasedStageDeadline(
        string? deadlineKind,
        DateTimeOffset? paymentAt,
        DateTimeOffset? prepaymentAt,
        int? duration,
        IReadOnlyList<HolidayCalendarDay> holidays)
    {
        if (!IsPaymentBasedDeadlineMode(deadlineKind))
        {
            return null;
        }

        var startAt = prepaymentAt ?? paymentAt;
        var deadlineAt = CalculateDeadline(deadlineKind, startAt, duration, holidays);
        if (startAt is null || deadlineAt is null)
        {
            return null;
        }

        return new StagePaymentBasedDeadlineCalculation(startAt.Value, deadlineAt.Value);
    }

    public static bool ShouldClearPaymentDuration(string? paymentDeadlineKind, int? paymentDuration)
    {
        return (string.IsNullOrWhiteSpace(paymentDeadlineKind) || IsPaymentDeadlineManualMode(paymentDeadlineKind))
            && paymentDuration is not null;
    }
}
