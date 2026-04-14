using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Services;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public class DataQueryStateBuilderTests
{
    [Fact]
    public void BuildFilters_MapsContainsAndInCriteriaToApiPayload()
    {
        var filters = new[]
        {
            new DataFilterCriterion
            {
                FieldKey = "name",
                MatchMode = DataFilterMatchMode.Contains,
                Value = "proj"
            },
            new DataFilterCriterion
            {
                FieldKey = "status",
                MatchMode = DataFilterMatchMode.In,
                Value = new object?[] { 1, 2, null }
            }
        };

        var payload = Assert.IsType<Dictionary<string, object?>>(DataQueryStateBuilder.BuildFilters(
            filters,
            new Dictionary<string, string>
            {
                ["name"] = "name",
                ["status"] = "status"
            }));

        Assert.Equal("proj", payload["name__cont"]);
        var groups = Assert.IsType<Dictionary<string, object?>[]>(payload["g"]);
        Assert.Single(groups);
        Assert.Equal("or", groups[0]["m"]);
        Assert.Equal(true, groups[0]["status__null"]);
        Assert.Equal(new object?[] { 1, 2 }, groups[0]["status__in"]);
    }

    [Fact]
    public void BuildSorts_MapsDirectionAndFieldNames()
    {
        var sorts = new[]
        {
            new DataSortCriterion
            {
                FieldKey = "createdAt",
                Direction = DataSortDirection.Descending
            }
        };

        var payload = DataQueryStateBuilder.BuildSorts(
            sorts,
            new Dictionary<string, string>
            {
                ["createdAt"] = "created_at"
            });

        Assert.Equal(["created_at desc"], payload);
    }
}
