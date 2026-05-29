using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using static CbsContractsDesktopClient.Shared.Formatting.AppFormatters;

namespace CbsContractsDesktopClient.ViewModels.Workflow;

public sealed record StageFinEditPayloadInput(
    long Id,
    string? ListKey,
    DateTimeOffset? PaymentAt,
    DateTimeOffset? PrepaymentAt,
    DateTimeOffset? InvoiceAt,
    DateTimeOffset? FundedAt,
    bool IsFunded,
    DateTimeOffset? StartAt,
    DateTimeOffset? DeadlineAt,
    DateTimeOffset? PaymentDeadlineAt,
    string? Comment,
    int? ProfileId);

public static class StageFinEditPayloadBuilder
{
    public static IReadOnlyDictionary<string, object?> BuildForUpdate(
        StageEditState state,
        string? comment,
        int? profileId)
    {
        ArgumentNullException.ThrowIfNull(state);

        var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = state.Id
        };

        if (!string.IsNullOrWhiteSpace(state.ListKey))
        {
            request["list_key"] = state.ListKey;
        }

        AppendChangedDate(request, "payment_at", state.Original.PaymentAt, state.PaymentAt);
        AppendChangedDate(request, "prepayment_at", state.Original.PrepaymentAt, state.PrepaymentAt);
        AppendChangedDate(request, "invoice_at", state.Original.InvoiceAt, state.InvoiceAt);
        AppendChangedDate(request, "funded_at", state.Original.FundedAt, state.FundedAt);
        AppendChangedBool(request, "is_funded", state.Original.IsFunded, state.IsFunded);
        AppendChangedDate(request, "start_at", state.Original.StartAt, state.StartAt);
        AppendChangedDate(request, "deadline_at", state.Original.DeadlineAt, state.DeadlineAt);
        AppendChangedDate(request, "payment_deadline_at", state.Original.PaymentDeadlineAt, state.PaymentDeadlineAt);

        StageEditPayloadBuilderHelpers.AppendCommentAttributes(request, comment, profileId);

        return request;
    }

    public static IReadOnlyDictionary<string, object?> BuildForUpdate(
        TableDataRow sourceRow,
        StageFinEditPayloadInput input)
    {
        ArgumentNullException.ThrowIfNull(sourceRow);
        ArgumentNullException.ThrowIfNull(input);

        var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = input.Id
        };

        if (!string.IsNullOrWhiteSpace(input.ListKey))
        {
            request["list_key"] = input.ListKey;
        }

        AppendChangedDate(request, sourceRow, "payment_at", input.PaymentAt);
        AppendChangedDate(request, sourceRow, "prepayment_at", input.PrepaymentAt);
        AppendChangedDate(request, sourceRow, "invoice_at", input.InvoiceAt);
        AppendChangedDate(request, sourceRow, "funded_at", input.FundedAt);
        AppendChangedBool(request, sourceRow, "is_funded", input.IsFunded);
        AppendChangedDate(request, sourceRow, "start_at", input.StartAt);
        AppendChangedDate(request, sourceRow, "deadline_at", input.DeadlineAt);
        AppendChangedDate(request, sourceRow, "payment_deadline_at", input.PaymentDeadlineAt);

        StageEditPayloadBuilderHelpers.AppendCommentAttributes(
            request,
            input.Comment,
            input.ProfileId);

        return request;
    }

    private static void AppendChangedBool(
        IDictionary<string, object?> request,
        TableDataRow sourceRow,
        string key,
        bool value)
    {
        var originalValue = JsonDataReader.TryGetBool(sourceRow.GetValue(key)) ?? false;
        if (originalValue != value)
        {
            request[key] = value;
        }
    }

    private static void AppendChangedBool(
        IDictionary<string, object?> request,
        string key,
        bool originalValue,
        bool value)
    {
        if (originalValue != value)
        {
            request[key] = value;
        }
    }

    private static void AppendChangedDate(
        IDictionary<string, object?> request,
        TableDataRow sourceRow,
        string key,
        DateTimeOffset? value)
    {
        var originalValue = ToDateOnly(ParseDate(sourceRow.GetValue(key)));
        var currentValue = ToDateOnly(value);
        if (originalValue != currentValue)
        {
            request[key] = FormatDate(value);
        }
    }

    private static void AppendChangedDate(
        IDictionary<string, object?> request,
        string key,
        DateTimeOffset? originalValue,
        DateTimeOffset? value)
    {
        var originalDate = ToDateOnly(originalValue);
        var currentDate = ToDateOnly(value);
        if (originalDate != currentDate)
        {
            request[key] = FormatDate(value);
        }
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
    {
        return value is null
            ? null
            : DateOnly.FromDateTime(value.Value.Date);
    }
}
