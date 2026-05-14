using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class CbsTableRowViewTests
{
    private static readonly string CbsTableRowViewPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Controls",
        "CbsTableRowView.xaml.cs");

    [Fact]
    public void CbsTableRowView_FormatsDateTimeValuesUsingCurrentCulture()
    {
        var code = File.ReadAllText(CbsTableRowViewPath);

        Assert.Contains("FormatCellValue", code);
        Assert.Contains("column.Filter.Mode == DataFilterMode.Date", code);
        Assert.Contains("FormatDateValue", code);
        Assert.Contains("CultureInfo.CurrentCulture", code);
        Assert.Contains("DateTimeOffset.TryParse", code);
        Assert.Contains("DateTime.TryParse", code);
        Assert.Contains("dateTimeOffset.LocalDateTime.ToString(CultureInfo.CurrentCulture)", code);
        Assert.Contains("dateTimeOffset.LocalDateTime.ToString(\"d\", CultureInfo.CurrentCulture)", code);
    }

    [Fact]
    public void CbsTableRowView_RendersBooleanIconAsCheckQuestionOrEmpty()
    {
        var code = File.ReadAllText(CbsTableRowViewPath);

        Assert.Contains("column.BodyMode == CbsTableBodyMode.BooleanIcon", code);
        Assert.Contains("(\"\\u2713\", \"SystemFillColorSuccessBrush\")", code);
        Assert.Contains("(\"?\", \"ShellSecondaryTextBrush\")", code);
        Assert.Contains("false => (string.Empty, \"ShellPrimaryTextBrush\")", code);
    }

    [Fact]
    public void CbsTableRowView_BatchesConfigureRefreshAndUsesSkeletonForPlaceholders()
    {
        var code = File.ReadAllText(CbsTableRowViewPath);

        Assert.Contains("private bool _isConfiguring;", code);
        Assert.Contains("if (rowView._isConfiguring)", code);
        Assert.Contains("_textCells[index].Visibility = isPlaceholder ? Visibility.Collapsed : Visibility.Visible;", code);
        Assert.Contains("_skeletonCells[index].Visibility = isPlaceholder ? Visibility.Visible : Visibility.Collapsed;", code);
        Assert.Contains("_textCells[index].Text = string.Empty;", code);
    }

    [Fact]
    public void CbsTableRowView_RendersStatusBadgeTemplateWithPrimeSeverityColors()
    {
        var code = File.ReadAllText(CbsTableRowViewPath);

        Assert.Contains("IsStatusBadgeTemplate", code);
        Assert.Contains("ApplyStatusBadgeContent", code);
        Assert.Contains("StageContractStatusDialogControls.ResolveStatusBadgeColors(statusId)", code);
    }
}
