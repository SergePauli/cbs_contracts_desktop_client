using System.IO;
using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.Table;
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
        var date = new DateTimeOffset(2026, 9, 14, 12, 30, 0, TimeSpan.Zero);
        var column = new CbsTableColumnDefinition { FieldKey = "value", Header = "Дата" };
        var row = CreateRow(date);
        Assert.Equal(date.LocalDateTime.ToString(CultureInfo.CurrentCulture),
            TableCellPresentationBuilder.GetCellText(column, row, false));
        column = new() { FieldKey = "value", Header = "Дата", Filter = new() { Mode = DataFilterMode.Date } };
        Assert.Equal(date.LocalDateTime.ToString("d", CultureInfo.CurrentCulture),
            TableCellPresentationBuilder.GetCellText(column, row, false));
    }

    [Fact]
    public void CbsTableRowView_RendersBooleanIconAsCheckQuestionOrEmpty()
    {
        var column = new CbsTableColumnDefinition { FieldKey = "value", Header = "Флаг", BodyMode = CbsTableBodyMode.BooleanIcon };
        Assert.Equal("✓", TableCellPresentationBuilder.GetCellText(column, CreateRow(true), false));
        Assert.Equal("", TableCellPresentationBuilder.GetCellText(column, CreateRow(false), false));
        Assert.Equal("?", TableCellPresentationBuilder.GetCellText(column, CreateRow(null), false));
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
        Assert.True(TableCellPresentationBuilder.IsBadgeTemplate(new()
            { FieldKey = "status", Header = "Статус", BodyTemplateKey = "StatusBadge" }));
        Assert.Equal((0xFFC9E9D4u, 0xFF404040u), TableCellPresentationBuilder.GetStatusColors(4));
        Assert.Equal((0xFFFFCDD2u, 0xFF404040u), TableCellPresentationBuilder.GetStatusColors(6));
        Assert.Equal((0xFEC2EDF6u, 0xFF404040u), TableCellPresentationBuilder.GetStatusColors(1));
    }

    [Fact]
    public void CbsTableRowView_FormatsStageCostWithOptionalFraction()
    {
        var oldCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            var column = new CbsTableColumnDefinition { FieldKey = "value", Header = "Сумма", BodyTemplateKey = "StageCost" };
            Assert.Equal("1,251", TableCellPresentationBuilder.GetCellText(column, CreateRow(1250.75m), false));
            Assert.Equal("1,250.75", TableCellPresentationBuilder.GetCellText(column, CreateRow(1250.75m), true));
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
    }

    private static TableDataRow CreateRow(object? value) => new()
    { Values = new() { ["value"] = JsonSerializer.SerializeToElement(value) } };
}
