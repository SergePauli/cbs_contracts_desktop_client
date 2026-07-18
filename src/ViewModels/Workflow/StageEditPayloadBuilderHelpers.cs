namespace CbsContractsDesktopClient.ViewModels.Workflow;

public static class StageEditPayloadBuilderHelpers
{
    public static IReadOnlyDictionary<string, object?> BuildCommentUpdate(
        long stageId,
        string? listKey,
        string comment,
        int profileId)
    {
        var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = stageId
        };
        if (!string.IsNullOrWhiteSpace(listKey))
        {
            request["list_key"] = listKey;
        }

        AppendCommentAttributes(request, comment, profileId);
        return request;
    }

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
