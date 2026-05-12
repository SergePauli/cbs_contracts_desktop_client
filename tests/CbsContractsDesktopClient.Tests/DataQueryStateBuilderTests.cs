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
        var group = Assert.IsType<Dictionary<string, object?>>(payload["or"]);
        Assert.Equal(true, group["status__null"]);
        Assert.Equal(new object?[] { 1, 2 }, group["status__in"]);
        Assert.DoesNotContain("g", payload.Keys);
    }

    [Fact]
    public void BuildFilters_MapsShortStageRegisterFilterToRegistryQuarter()
    {
        var filters = new[]
        {
            new DataFilterCriterion
            {
                FieldKey = "register",
                MatchMode = DataFilterMatchMode.Contains,
                Value = "1"
            }
        };

        var payload = Assert.IsType<Dictionary<string, object?>>(DataQueryStateBuilder.BuildFilters(
            filters,
            new Dictionary<string, string>
            {
                ["register"] = "registry_quarter_or_registry_year"
            }));

        Assert.Equal(1, payload["registry_quarter__eq"]);
        Assert.DoesNotContain("registry_year__eq", payload.Keys);
        Assert.DoesNotContain("registry_quarter_or_registry_year__cnt", payload.Keys);
        Assert.DoesNotContain("register__cnt", payload.Keys);
    }

    [Fact]
    public void BuildFilters_MapsLongStageRegisterFilterToRegistryYear()
    {
        var filters = new[]
        {
            new DataFilterCriterion
            {
                FieldKey = "register",
                MatchMode = DataFilterMatchMode.Contains,
                Value = "2025"
            }
        };

        var payload = Assert.IsType<Dictionary<string, object?>>(DataQueryStateBuilder.BuildFilters(
            filters,
            new Dictionary<string, string>
            {
                ["register"] = "registry_quarter_or_registry_year"
            }));

        Assert.Equal(2025, payload["registry_year__eq"]);
        Assert.DoesNotContain("registry_quarter__eq", payload.Keys);
        Assert.DoesNotContain("registry_quarter_or_registry_year__cnt", payload.Keys);
        Assert.DoesNotContain("register__cnt", payload.Keys);
    }

    [Fact]
    public void BuildFilters_MapsDottedStageRegisterFilterToRegistryQuarterAndYear()
    {
        var filters = new[]
        {
            new DataFilterCriterion
            {
                FieldKey = "register",
                MatchMode = DataFilterMatchMode.Contains,
                Value = "1.2025"
            }
        };

        var payload = Assert.IsType<Dictionary<string, object?>>(DataQueryStateBuilder.BuildFilters(
            filters,
            new Dictionary<string, string>
            {
                ["register"] = "registry_quarter_or_registry_year"
            }));

        Assert.Equal(1, payload["registry_quarter__eq"]);
        Assert.Equal(2025, payload["registry_year__eq"]);
        Assert.DoesNotContain("registry_quarter_or_registry_year__cnt", payload.Keys);
        Assert.DoesNotContain("register__cnt", payload.Keys);
    }

    [Fact]
    public void BuildFilters_MapsPositiveStageSziFilterToTaskKindId()
    {
        var filters = new[]
        {
            new DataFilterCriterion
            {
                FieldKey = "szi",
                MatchMode = DataFilterMatchMode.Equals,
                Value = true
            }
        };

        var payload = Assert.IsType<Dictionary<string, object?>>(DataQueryStateBuilder.BuildFilters(
            filters,
            new Dictionary<string, string>
            {
                ["szi"] = "tasks.task_kind_id"
            }));

        Assert.Equal(10, payload["tasks.task_kind_id__eq"]);
        Assert.DoesNotContain("szi__eq", payload.Keys);
    }

    [Fact]
    public void BuildFilters_MapsNegativeStageSziFilterToNotTaskKindIdGroup()
    {
        var filters = new[]
        {
            new DataFilterCriterion
            {
                FieldKey = "szi",
                MatchMode = DataFilterMatchMode.Equals,
                Value = false
            }
        };

        var payload = Assert.IsType<Dictionary<string, object?>>(DataQueryStateBuilder.BuildFilters(
            filters,
            new Dictionary<string, string>
            {
                ["szi"] = "tasks.task_kind_id"
            }));

        var notGroup = Assert.IsType<Dictionary<string, object?>>(payload["not"]);
        Assert.Equal(10, notGroup["tasks.task_kind_id__eq"]);
        Assert.DoesNotContain("tasks.task_kind_id__not_eq", payload.Keys);
        Assert.DoesNotContain("szi__eq", payload.Keys);
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
    public void BuildFilters_MapsNullOnlyInCriteriaToNullFilter()
    {
        var filters = new[]
        {
            new DataFilterCriterion
            {
                FieldKey = "status",
                MatchMode = DataFilterMatchMode.In,
                Value = new object?[] { null }
            }
        };

        var payload = Assert.IsType<Dictionary<string, object?>>(DataQueryStateBuilder.BuildFilters(
            filters,
            new Dictionary<string, string>
            {
                ["status"] = "status_id"
            }));

        Assert.Equal(true, payload["status_id__null"]);
        Assert.DoesNotContain("g", payload.Keys);
        Assert.DoesNotContain("or", payload.Keys);
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

    [Fact]
    public void BuildFilters_MapsDateTimeCriteriaToApiPayload()
    {
        var filters = new[]
        {
            new DataFilterCriterion
            {
                FieldKey = "lastLogin",
                FilterMode = DataFilterMode.DateTime,
                MatchMode = DataFilterMatchMode.GreaterThanOrEqual,
                Value = "20.04.2026 14:30"
            }
        };

        var payload = Assert.IsType<Dictionary<string, object?>>(DataQueryStateBuilder.BuildFilters(
            filters,
            new Dictionary<string, string>
            {
                ["lastLogin"] = "user.last_login"
            }));

        Assert.Equal("2026-04-20T14:30:00", payload["user.last_login__gte"]);
    }

    [Fact]
    public void BuildFilters_MapsDateCriteriaToApiPayload()
    {
        var filters = new[]
        {
            new DataFilterCriterion
            {
                FieldKey = "beginAt",
                FilterMode = DataFilterMode.Date,
                MatchMode = DataFilterMatchMode.GreaterThanOrEqual,
                Value = "20.04.2026"
            }
        };

        var payload = Assert.IsType<Dictionary<string, object?>>(DataQueryStateBuilder.BuildFilters(
            filters,
            new Dictionary<string, string>
            {
                ["beginAt"] = "begin_at"
            }));

        Assert.Equal("2026-04-20", payload["begin_at__gte"]);
    }

    [Fact]
    public void BuildFilters_MapsNotContainsCriteriaToApiPayload()
    {
        var filters = new[]
        {
            new DataFilterCriterion
            {
                FieldKey = "name",
                MatchMode = DataFilterMatchMode.NotContains,
                Value = "test"
            }
        };

        var payload = Assert.IsType<Dictionary<string, object?>>(DataQueryStateBuilder.BuildFilters(
            filters,
            new Dictionary<string, string>
            {
                ["name"] = "name"
            }));

        Assert.Equal("test", payload["name__not_cnt"]);
        Assert.DoesNotContain("name__not_cont", payload.Keys);
    }
}
