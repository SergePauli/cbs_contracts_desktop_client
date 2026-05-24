namespace CbsContractsDesktopClient.ViewModels.Workflow;

public static class StageEditPayloadBuilderHelpers
{
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
