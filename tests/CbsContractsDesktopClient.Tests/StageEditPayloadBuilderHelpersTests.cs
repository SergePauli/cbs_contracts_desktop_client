using CbsContractsDesktopClient.ViewModels.Workflow;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageEditPayloadBuilderHelpersTests
{
    [Fact]
    public void BuildCommentUpdate_ContainsOnlyCommentMutationFields()
    {
        var payload = StageEditPayloadBuilderHelpers.BuildCommentUpdate(
            15L,
            "stage-list-key",
            "  новый комментарий  ",
            7);

        Assert.Equal(3, payload.Count);
        Assert.Equal(15L, payload["id"]);
        Assert.Equal("stage-list-key", payload["list_key"]);
        var comments = Assert.IsType<Dictionary<string, object?>[]>(payload["comments_attributes"]);
        var comment = Assert.Single(comments);
        Assert.Equal("новый комментарий", comment["content"]);
        Assert.Equal(7, comment["profile_id"]);
    }

    [Fact]
    public void AppendCommentAttributes_UsesRailsNestedAttributesShape()
    {
        var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = 15L
        };

        StageEditPayloadBuilderHelpers.AppendCommentAttributes(request, " comment ", 7);

        var comments = Assert.IsType<Dictionary<string, object?>[]>(request["comments_attributes"]);
        Assert.Single(comments);
        Assert.Equal("comment", comments[0]["content"]);
        Assert.Equal(7, comments[0]["profile_id"]);
    }

    [Fact]
    public void AppendCommentAttributes_SkipsEmptyCommentOrMissingProfile()
    {
        var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = 15L
        };

        StageEditPayloadBuilderHelpers.AppendCommentAttributes(request, " ", 7);
        StageEditPayloadBuilderHelpers.AppendCommentAttributes(request, "comment", null);

        Assert.False(request.ContainsKey("comments_attributes"));
    }
}
