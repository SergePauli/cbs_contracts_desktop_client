using CbsContractsDesktopClient.Shared.Dates;
using CbsContractsDesktopClient.ViewModels.Workflow;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageDeadlineBusinessRulesTests
{
    [Fact]
    public void ResolveInitialStart_ForSingleStageNonPaymentDeadlineUsesContractSignedDate()
    {
        var signedAt = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);

        var startAt = StageDeadlineBusinessRules.ResolveInitialStart(
            isMultiStageContract: false,
            currentStartAt: null,
            deadlineKind: StageDeadlineBusinessRules.DeadlineCalendarDays,
            contractSignedAt: signedAt,
            paymentBaseDate: null);

        Assert.Equal(signedAt, startAt);
    }

    [Fact]
    public void ResolveInitialStart_ForPaymentBasedDeadlineUsesPaymentBaseDate()
    {
        var signedAt = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var paymentAt = new DateTimeOffset(2026, 6, 18, 0, 0, 0, TimeSpan.Zero);

        var startAt = StageDeadlineBusinessRules.ResolveInitialStart(
            isMultiStageContract: false,
            currentStartAt: signedAt,
            deadlineKind: StageDeadlineBusinessRules.DeadlineCalendarPrepayment,
            contractSignedAt: signedAt,
            paymentBaseDate: paymentAt);

        Assert.Equal(paymentAt, startAt);
    }

    [Fact]
    public void ResolveInitialStart_ForMultiStageNonPaymentDeadlineDoesNotAutoFillStart()
    {
        var startAt = StageDeadlineBusinessRules.ResolveInitialStart(
            isMultiStageContract: true,
            currentStartAt: null,
            deadlineKind: StageDeadlineBusinessRules.DeadlineWorkingDays,
            contractSignedAt: new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero),
            paymentBaseDate: null);

        Assert.Null(startAt);
    }

    [Fact]
    public void CalculateDeadline_ForCalendarDaysAddsDurationToStartDate()
    {
        var deadline = StageDeadlineBusinessRules.CalculateDeadline(
            StageDeadlineBusinessRules.DeadlineCalendarDays,
            new DateTimeOffset(2026, 6, 14, 0, 0, 0, TimeSpan.Zero),
            13,
            []);

        Assert.Equal(new DateOnly(2026, 6, 27), DateOnly.FromDateTime(deadline!.Value.Date));
    }

    [Fact]
    public void CalculatePaymentDeadline_ForCalendarPlanLeavesManualDateUntouched()
    {
        var deadline = StageDeadlineBusinessRules.CalculatePaymentDeadline(
            StageDeadlineBusinessRules.PaymentCalendarPlan,
            new DateTimeOffset(2026, 6, 14, 0, 0, 0, TimeSpan.Zero),
            13,
            Array.Empty<HolidayCalendarDay>());

        Assert.Null(deadline);
    }
}
