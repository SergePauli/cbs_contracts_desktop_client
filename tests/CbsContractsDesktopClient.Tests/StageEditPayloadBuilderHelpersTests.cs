using CbsContractsDesktopClient.ViewModels.Workflow;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageEditPayloadBuilderHelpersTests
{
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
