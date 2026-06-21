namespace CbsContractsDesktopClient.ViewModels.Workflow;

public static class StageEditPayloadBuilderHelpers
{
    public static IReadOnlyDictionary<string, object?> BuildContractClosePayload(
        long contractId,
        DateTimeOffset? closedAt)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = contractId,
            ["status_id"] = WorkflowStatusIds.Closed,
            ["closed_at"] = Shared.Formatting.AppFormatters.FormatDate(closedAt)
        };
    }

    public static void AppendCommentAttributes(
        IDictionary<string, object?> request,
        string? comment,
        int? profileId)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedComment = comment?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedComment) || profileId is not int value)
        {
            return;
        }

        request["comments_attributes"] = new[]
        {
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["content"] = normalizedComment,
                ["profile_id"] = value
            }
        };
    }
}
