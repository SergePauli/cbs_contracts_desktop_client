using System.Collections.Generic;
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

        Assert.Equal("proj", payload["name__cnt"]);
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

    [Fact]
    public void BuildFilters_AcceptsTypedEnumerableForInCriteria()
    {
        var filters = new[]
        {
            new DataFilterCriterion
            {
                FieldKey = "department",
                MatchMode = DataFilterMatchMode.In,
                Value = new List<int> { 2, 5, 7 }
            }
        };

        var payload = Assert.IsType<Dictionary<string, object?>>(DataQueryStateBuilder.BuildFilters(
            filters,
            new Dictionary<string, string>
            {
                ["department"] = "department_id"
            }));

        Assert.Equal(new object?[] { 2, 5, 7 }, payload["department_id__in"]);
    }

    [Fact]
    public void BuildFilters_MapsNumericCriteriaToApiPayload()
    {
        var filters = new[]
        {
            new DataFilterCriterion
            {
                FieldKey = "cost",
                FilterMode = DataFilterMode.Numeric,
                MatchMode = DataFilterMatchMode.LessThanOrEqual,
                Value = "125.50"
            },
            new DataFilterCriterion
            {
                FieldKey = "priority",
                FilterMode = DataFilterMode.Numeric,
                MatchMode = DataFilterMatchMode.GreaterThan,
                Value = 2
            }
        };

        var payload = Assert.IsType<Dictionary<string, object?>>(DataQueryStateBuilder.BuildFilters(
            filters,
            new Dictionary<string, string>
            {
                ["cost"] = "cost",
                ["priority"] = "priority"
            }));

        Assert.Equal(125.50m, payload["cost__lte"]);
        Assert.Equal(2, payload["priority__gt"]);
    }
}
