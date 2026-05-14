using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Dialogs;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageContractStatusDialogControlsTests
{
    [Fact]
    public void BuildStageStatusOptions_KeepsOnlyAllowedStageStatuses()
    {
        var options = new[]
        {
            Option(1, "Signed"),
            Option(2, "In progress"),
            Option(4, "Ready"),
            Option(5, "Closed"),
            Option(6, "Rejected"),
            Option(7, "Archived")
        };

        var result = StageContractStatusDialogControls.BuildStageStatusOptions(options);

        Assert.Equal([null, 2L, 4L, 5L, 6L, 7L], result.Select(static item => item.Value).ToList());
    }

    [Fact]
    public void FindStatusLabel_ReturnsLabelByNumericOptionValue()
    {
        var options = new[]
        {
            Option("2", "In progress")
        };

        Assert.Equal("In progress", StageContractStatusDialogControls.FindStatusLabel(options, 2));
    }

    [Fact]
    public void ResolveStatusBadgeColors_KeepsSharedPrimeSeverityPalette()
    {
        var success = StageContractStatusDialogControls.ResolveStatusBadgeColors(5);
        var info = StageContractStatusDialogControls.ResolveStatusBadgeColors(2);
        var warning = StageContractStatusDialogControls.ResolveStatusBadgeColors(3);
        var danger = StageContractStatusDialogControls.ResolveStatusBadgeColors(6);

        Assert.Equal(201, success.Background.R);
        Assert.Equal(194, info.Background.R);
        Assert.Equal(246, warning.Background.R);
        Assert.Equal(255, danger.Background.R);
    }

    private static CbsTableFilterOptionDefinition Option(object value, string label)
    {
        return new CbsTableFilterOptionDefinition
        {
            Value = value,
            Label = label
        };
    }
}
