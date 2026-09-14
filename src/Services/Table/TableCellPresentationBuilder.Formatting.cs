using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.Table;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.Services.Table;

public static partial class TableCellPresentationBuilder
{
        public static string GetCellText(
            CbsTableColumnDefinition column,
            TableDataRow row,
            bool showStageCostFraction)
        {
            var valueKey = column.DisplayField ?? column.ApiField ?? column.FieldKey;
            var value = row.GetValue(valueKey);
            if (string.Equals(column.BodyTemplateKey, "StageOrderSeverity", StringComparison.OrdinalIgnoreCase))
            {
                var severity = StageOrderSeverityText.Parse(checked((int)(TryGetLong(value)
                    ?? throw new InvalidOperationException("StageOrderSeverity должен содержать целочисленное значение."))));
                return StageOrderSeverityText.GetLabel(severity);
            }

            if (!string.IsNullOrWhiteSpace(column.BodyTemplateKey))
            {
                var formatted = FormatTemplateValue(column.BodyTemplateKey, row, value, showStageCostFraction);
                if (formatted is not null)
                {
                    return formatted;
                }
            }

            if (column.BodyMode == CbsTableBodyMode.BooleanIcon)
            {
                return value switch
                {
                    true => "\u2713",
                    false => string.Empty,
                    null => "?",
                    string textValue when bool.TryParse(textValue, out var parsedBoolean)
                        => parsedBoolean ? "\u2713" : string.Empty,
                    _ => "?"
                };
            }

            return FormatCellValue(column, value);
        }

        private static string FormatCellValue(CbsTableColumnDefinition column, object? value)
        {
            if (column.Filter.Mode == DataFilterMode.Date)
            {
                return FormatDateValue(value);
            }

            return value switch
            {
                null => string.Empty,
                DateTime dateTime => dateTime.ToLocalTime().ToString(CultureInfo.CurrentCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.LocalDateTime.ToString(CultureInfo.CurrentCulture),
                string text when TryFormatDateTimeText(text, out var formattedDateTime) => formattedDateTime,
                _ => value.ToString() ?? string.Empty
            };
        }

        private static string? FormatTemplateValue(
            string templateKey,
            TableDataRow? row,
            object? value,
            bool showStageCostFraction)
        {
            if (row is null)
            {
                return null;
            }

            return templateKey switch
            {
                "StageRegion" => FirstText(
                    row.GetValue("contract.contragent.region.name"),
                    row.GetValue("contract.region.name"),
                    value),
                "StageRegister" => FormatStageRegister(row),
                "StageCost" => FormatStageCost(value, showStageCostFraction),
                "StageDuration" => FormatStageDuration(row, value),
                "StageSzi" => HasStageTaskKind(row, 10) ? "\u2713" : string.Empty,
                "IsecurityToolKind" => FormatIsecurityToolKind(value),
                "ContractDsp" => FormatContractDsp(row),
                "ContractRegion" => FirstText(
                    row.GetValue("contragent.region.name"),
                    row.GetValue("region.name"),
                    value),
                "ContractCost" => FormatStageCost(value, showStageCostFraction),
                "ContractFunded" => FormatContractFunded(value),
                "ActivityReportDeletedAmount" => value?.ToString() ?? string.Empty,
                _ => null
            };
        }

        private static string FormatIsecurityToolKind(object? value)
        {
            var numericValue = value switch
            {
                long longValue => longValue,
                decimal decimalValue when decimal.Truncate(decimalValue) == decimalValue => (long)decimalValue,
                _ => throw new InvalidOperationException("IsecurityTool.kind должен содержать целочисленное значение.")
            };

            return IsecurityToolKindText.GetLabel(IsecurityToolKindText.Parse(numericValue));
        }

        private static string FormatContractDsp(TableDataRow row)
        {
            return $"{FormatPresence(row.GetValue("revision.doc_link"))}{FormatPresence(row.GetValue("revision.scan_link"))}{FormatPresence(row.GetValue("revision.protocol_link"))}";
        }

        private static string FormatPresence(object? value)
        {
            return string.IsNullOrWhiteSpace(value?.ToString()) ? "-" : "+";
        }

        private static string FormatContractFunded(object? value)
        {
            return value switch
            {
                true => "\u2713",
                string text when bool.TryParse(text, out var parsed) && parsed => "\u2713",
                string text when string.Equals(text, "null", StringComparison.OrdinalIgnoreCase) => "\u231B",
                null => "\u231B",
                _ => string.Empty
            };
        }

        private static string FormatStageCost(object? value, bool showFraction)
        {
            return ReadCost(value) is decimal amount
                ? amount.ToString(showFraction ? "N2" : "N0", CultureInfo.CurrentCulture)
                : string.Empty;
        }

        private static decimal? ReadCost(object? value)
        {
            return value switch
            {
                decimal decimalValue => decimalValue,
                double doubleValue => Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture),
                float floatValue => Convert.ToDecimal(floatValue, CultureInfo.InvariantCulture),
                long longValue => longValue,
                int intValue => intValue,
                string text when decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantValue) => invariantValue,
                string text when decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var currentValue) => currentValue,
                _ => (decimal?)null
            };

        }

        public static bool IsBadgeTemplate(CbsTableColumnDefinition column)
        {
            return string.Equals(column.BodyTemplateKey, "StatusBadge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(column.BodyTemplateKey, "StageOrderSeverity", StringComparison.OrdinalIgnoreCase)
                || string.Equals(column.BodyTemplateKey, "OrderDeliveryStatus", StringComparison.OrdinalIgnoreCase)
                || string.Equals(column.BodyTemplateKey, "OrderStatusBadge", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatStageRegister(TableDataRow row)
        {
            var quarter = row.GetValue("registry_quarter")?.ToString();
            var year = row.GetValue("registry_year")?.ToString();
            return string.IsNullOrWhiteSpace(quarter)
                ? string.Empty
                : string.IsNullOrWhiteSpace(year)
                    ? quarter
                    : $"{quarter}.{year}";
        }

        private static string FormatStageDuration(TableDataRow row, object? value)
        {
            var duration = value?.ToString();
            if (string.IsNullOrWhiteSpace(duration))
            {
                return string.Empty;
            }

            return $"{duration}{FormatDeadlineKind(row.GetValue("deadline_kind")?.ToString())}";
        }

        private static string FormatDeadlineKind(string? kind)
        {
            return kind switch
            {
                "calendar_plan" => "KП",
                "calendar_days" => "КД",
                "calendar_prepayment" => "KДП",
                "working_days" => "РД",
                "working_prepayment" => "РДП",
                _ => string.Empty
            };
        }

        private static bool HasStageTaskKind(TableDataRow row, long taskKindId)
        {
            if (!row.Values.TryGetValue("tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var task in tasks.EnumerateArray())
            {
                if (task.ValueKind == JsonValueKind.Object
                    && task.TryGetProperty("task_kind_id", out var taskKind)
                    && taskKind.TryGetInt64(out var id)
                    && id == taskKindId)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatDateValue(object? value)
        {
            return value switch
            {
                null => string.Empty,
                DateTime dateTime => dateTime.ToLocalTime().ToString("d", CultureInfo.CurrentCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.LocalDateTime.ToString("d", CultureInfo.CurrentCulture),
                string text when TryFormatDateText(text, out var formattedDate) => formattedDate,
                _ => value.ToString() ?? string.Empty
            };
        }

        private static bool TryFormatDateTimeText(string text, out string formattedValue)
        {
            formattedValue = string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!text.Contains('-') || (!text.Contains('T') && !text.Contains(':')))
            {
                return false;
            }

            if (DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out var dateTimeOffset))
            {
                formattedValue = dateTimeOffset.LocalDateTime.ToString(CultureInfo.CurrentCulture);
                return true;
            }

            if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                out var dateTime))
            {
                formattedValue = dateTime.ToString(CultureInfo.CurrentCulture);
                return true;
            }

            return false;
        }

        private static bool TryFormatDateText(string text, out string formattedValue)
        {
            formattedValue = string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out var dateTimeOffset))
            {
                formattedValue = dateTimeOffset.LocalDateTime.ToString("d", CultureInfo.CurrentCulture);
                return true;
            }

            if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                out var dateTime))
            {
                formattedValue = dateTime.ToString("d", CultureInfo.CurrentCulture);
                return true;
            }

            return false;
        }

        private static ConditionalRowStyle ResolveStageDeadlineStyle(TableDataRow row)
        {
            var deadline = TryGetDateTime(row.GetValue("deadline_at"));
            var statusId = TryGetLong(row.GetValue("status.id")) ?? TryGetLong(row.GetValue("status_id")) ?? 2;
            var governmental = TryGetBoolean(row.GetValue("contract.governmental"));
            return ResolveDeadlineStyle(deadline, statusId, governmental);
        }

        private static ConditionalRowStyle ResolveContractDeadlineStyle(TableDataRow row)
        {
            var deadline =
                TryGetDateTime(row.GetValue("stage.deadline_at"))
                ?? TryGetDateTime(row.GetValue("expired_at"))
                ?? TryGetDateTime(row.GetValue("deadline_at"))
                ?? TryGetDateTime(row.GetValue("expire_at"));
            var statusId =
                TryGetLong(row.GetValue("stage.status.id"))
                ?? TryGetLong(row.GetValue("stage.status_id"))
                ?? TryGetLong(row.GetValue("status.id"))
                ?? TryGetLong(row.GetValue("status_id"))
                ?? 2;
            var governmental = TryGetBoolean(row.GetValue("governmental"));
            return ResolveDeadlineStyle(deadline, statusId, governmental);
        }

        private static ConditionalRowStyle ResolveDeadlineStyle(DateTimeOffset? deadline, long statusId, bool governmental)
        {
            var isNotDone = statusId is not (5 or 4 or 7 or 6);

            if (deadline is null)
            {
                return governmental
                    ? new ConditionalRowStyle("StageGovernmentForegroundBrush", null, null, true)
                    : ConditionalRowStyle.Empty;
            }

            var now = DateTimeOffset.Now;
            var isDeadline = deadline.Value <= now;
            var isCloseDeadline = deadline.Value.AddDays(-14) <= now;

            if (isNotDone && isDeadline && governmental)
            {
                return new ConditionalRowStyle(
                    "StageGovernmentForegroundBrush",
                    "StageDeadlineAlertBackgroundBrush",
                    "StageDeadlineAlertBorderBrush",
                    true);
            }

            if (isNotDone && isCloseDeadline && !isDeadline)
            {
                return new ConditionalRowStyle(
                    governmental ? "StageGovernmentForegroundBrush" : null,
                    "StageDeadlineWarningBackgroundBrush",
                    "StageDeadlineWarningBorderBrush",
                    governmental);
            }

            if (isNotDone && isDeadline)
            {
                return new ConditionalRowStyle("StageDeadlineTextBrush", null, "StageDeadlineAlertBorderBrush", false);
            }

            return governmental
                ? new ConditionalRowStyle("StageGovernmentForegroundBrush", null, null, true)
                : ConditionalRowStyle.Empty;
        }

        private static DateTimeOffset? TryGetDateTime(object? value)
        {
            return value switch
            {
                DateTimeOffset dateTimeOffset => dateTimeOffset,
                DateTime dateTime => dateTime,
                string text when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed) => parsed,
                string text when DateTimeOffset.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsed) => parsed,
                _ => null
            };
        }

        private static long? TryGetLong(object? value)
        {
            return value switch
            {
                long longValue => longValue,
                int intValue => intValue,
                decimal decimalValue => (long)decimalValue,
                string text when long.TryParse(text, out var parsed) => parsed,
                _ => null
            };
        }

        private static bool TryGetBoolean(object? value)
        {
            return value switch
            {
                bool booleanValue => booleanValue,
                string text when bool.TryParse(text, out var parsed) => parsed,
                _ => false
            };
        }

}

public sealed record ConditionalRowStyle(string? ForegroundBrushKey, string? BackgroundBrushKey,
    string? BorderBrushKey, bool IsSemibold)
{
    public static ConditionalRowStyle Empty { get; } = new(null, null, null, false);
}
