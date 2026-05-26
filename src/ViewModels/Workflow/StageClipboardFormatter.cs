using System.Globalization;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Shared.Formatting;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow;

public static class StageClipboardFormatter
{
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    public static string BuildClipboardText(ReferenceDataRow? stageRow)
    {
        if (stageRow is null || stageRow.IsPlaceholder)
        {
            return string.Empty;
        }

        var startAt = FormatClipboardDate(AppFormatters.ParseDate(stageRow.GetValue("start_at")));
        var deadlineAt = FormatClipboardDate(AppFormatters.ParseDate(stageRow.GetValue("deadline_at")));
        var contragentName = TryGetText(
            stageRow,
            "contract.contragent.name",
            "contract.contragent.org.name",
            "contract.contragent.org.full_name",
            "contragent.name")
            ?? string.Empty;
        var contractTitle = TryGetText(
            stageRow,
            "contract.external_number",
            "contract.name",
            "external_number",
            "name")
            ?? string.Empty;

        return $"{startAt}-{deadlineAt} | {contragentName} | {contractTitle}";
    }

    private static string FormatClipboardDate(DateTimeOffset? value)
    {
        return value is null
            ? " "
            : value.Value.ToString("d", RuCulture);
    }
}
