using System.Globalization;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.Table;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.Services.Table;

public sealed record TablePresentationContext(IReadOnlyDictionary<string, uint> Colors, bool ShowCostFraction, double FontSize);

public sealed record TableCellPresentation(object? Value, string Text, string NumberFormat,
    uint Foreground, uint Background, bool Bold, CbsTableColumnAlignment Alignment, double FontSize);

public static partial class TableCellPresentationBuilder
{
    public static ConditionalRowStyle GetRowStyle(TableDataRow row, CbsTableRowStyleKey key) => key switch
    {
        CbsTableRowStyleKey.ContractDeadline => ResolveContractDeadlineStyle(row),
        CbsTableRowStyleKey.StageDeadline => ResolveStageDeadlineStyle(row),
        _ => ConditionalRowStyle.Empty
    };

    public static (uint Background, uint Foreground) GetStatusColors(long? statusId) => statusId switch
    {
        4 or 5 => (0xFFC9E9D4, 0xFF404040),
        6 => (0xFFFFCDD2, 0xFF404040),
        1 or 2 => (0xFEC2EDF6, 0xFF404040),
        3 => (0xFEF6E3C2, 0xFF404040),
        _ => (0xFFDEE2E6, 0xFF404040)
    };

    public static TableCellPresentation Build(CbsTableColumnDefinition column, TableDataRow row,
        CbsTableRowStyleKey rowStyleKey, TablePresentationContext context)
    {
        var raw = row.GetValue(column.DisplayField ?? column.ApiField ?? column.FieldKey);
        var text = GetCellText(column, row, context.ShowCostFraction);
        var style = GetRowStyle(row, rowStyleKey);
        var foreground = context.Colors[style.ForegroundBrushKey ?? "ShellPrimaryTextBrush"];
        var background = context.Colors[style.BackgroundBrushKey ?? "ShellTableRowBackgroundBrush"];
        var bold = style.IsSemibold;
        object? value = raw;
        var format = "General";

        if (IsBadgeTemplate(column))
        {
            bold = true;
            if (column.BodyTemplateKey == "StageOrderSeverity")
            {
                var severity = StageOrderSeverityText.Parse(checked((int)(TryGetLong(raw)
                    ?? throw new InvalidOperationException("StageOrderSeverity должен содержать целочисленное значение."))));
                text = StageOrderSeverityText.GetLabel(severity);
                background = severity switch
                {
                    StageOrderSeverity.Need => 0xFFFFC107,
                    StageOrderSeverity.InStock => 0xFF00B050,
                    StageOrderSeverity.OnControl => 0xFFDC3545,
                    StageOrderSeverity.NotApproved => 0xFF89CFF0,
                    StageOrderSeverity.Delivered => 0xFFE0E0E0,
                    _ => throw new ArgumentOutOfRangeException(nameof(raw))
                };
                foreground = severity is StageOrderSeverity.Need or StageOrderSeverity.NotApproved or StageOrderSeverity.Delivered
                    ? 0xFF000000 : 0xFFFFFFFF;
            }
            else if (column.BodyTemplateKey is "OrderDeliveryStatus" or "OrderStatusBadge")
            {
                var statusField = column.BodyTemplateKey == "OrderDeliveryStatus" ? "order.status.id" : "status.id";
                background = TryGetLong(row.GetValue(statusField)) switch
                {
                    2 => 0xFFC2EDF6, 3 => 0xFFC9E9D4, 4 => 0xFFFFEB9C, _ => 0xFFE0E0E0
                };
                foreground = 0xFF404040;
            }
            else
            {
                text = FirstText(raw, row.GetValue("status.name"));
                (background, foreground) = GetStatusColors(TryGetLong(row.GetValue("status.id")) ?? TryGetLong(row.GetValue("status_id")));
            }
            value = text;
        }
        else if (column.BodyTemplateKey is "StageCost" or "ContractCost")
        {
            value = raw is null ? null : ReadCost(raw)
                ?? throw new InvalidOperationException($"Столбец {column.FieldKey} должен содержать сумму.");
            format = context.ShowCostFraction ? "#,##0.00" : "#,##0";
        }
        else if (column.BodyTemplateKey == "StageSzi")
        {
            value = HasStageTaskKind(row, 10);
        }
        else if (column.BodyMode == CbsTableBodyMode.BooleanIcon || column.BodyTemplateKey == "ContractFunded")
        {
            value = raw is null || raw is string nullText && nullText.Equals("null", StringComparison.OrdinalIgnoreCase)
                ? text : raw is bool boolean ? boolean : bool.Parse((string)raw);
        }
        else if (column.BodyTemplateKey is "ContractDsp" or "StageRegion" or "ContractRegion"
            or "StageRegister" or "StageDuration" or "IsecurityToolKind")
        {
            value = text;
        }
        else if (raw is not null && (column.Filter.Mode == DataFilterMode.Date || raw is DateTime or DateTimeOffset
            || raw is string dateText && TryFormatDateTimeText(dateText, out _)))
        {
            value = raw switch
            {
                DateTime date => date.ToLocalTime(),
                DateTimeOffset date => date.LocalDateTime,
                string date => DateTimeOffset.Parse(date, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces).LocalDateTime,
                _ => throw new InvalidOperationException($"Столбец {column.FieldKey} должен содержать дату.")
            };
            format = column.Filter.Mode == DataFilterMode.Date
                ? CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern
                : CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern + " " + CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern;
            format = format.Replace("tt", "AM/PM");
        }

        if (!IsBadgeTemplate(column) && text == "\u2713")
            foreground = context.Colors[column.BodyMode == CbsTableBodyMode.BooleanIcon
                ? "SystemFillColorSuccessBrush" : "ShellPrimaryTextBrush"];
        if (column.BodyTemplateKey == "ActivityReportDeletedAmount" && text == "удален")
            foreground = 0xFFFF0000;

        return new(value, text, format, foreground, background, bold, column.Alignment,
            IsBadgeTemplate(column) ? Math.Max(11, context.FontSize - 1) : context.FontSize);
    }
}
