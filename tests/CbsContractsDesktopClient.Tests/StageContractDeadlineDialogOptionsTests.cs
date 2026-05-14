using CbsContractsDesktopClient.Shared.Dialogs;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageContractDeadlineDialogOptionsTests
{
    [Fact]
    public void DeadlineKindOptions_DefinesExecutionDeadlineModes()
    {
        var keys = StageContractDeadlineDialogOptions
            .DeadlineKindOptions()
            .Select(static option => option.Key)
            .ToList();

        Assert.Equal(
            ["calendar_plan", "calendar_days", "calendar_prepayment", "working_days", "working_prepayment"],
            keys);
    }

    [Fact]
    public void PaymentDeadlineKindOptions_DefinesPaymentDeadlineModes()
    {
        var keys = StageContractDeadlineDialogOptions
            .PaymentDeadlineKindOptions()
            .Select(static option => option.Key)
            .ToList();

        Assert.Equal([null, "c_plan", "c_days", "w_days"], keys);
    }
}
