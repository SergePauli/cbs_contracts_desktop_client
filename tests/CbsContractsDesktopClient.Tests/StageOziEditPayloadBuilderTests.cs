using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageOziEditPayloadBuilderTests
{
    [Fact]
    public void BuildForUpdate_SerializesChangedOziFieldsPerformersAndComment()
    {
        var stage = StageEditState.FromRow(CreateRow(
            ("id", 15L),
            ("list_key", "stage-key"),
            ("status_id", 2L),
            ("status.name", "В работе"),
            ("completed_at", null),
            ("ride_out_at", null),
            ("sended_at", "Mon May 04 2026"),
            ("closed_at", null),
            ("is_ride_out", false),
            ("is_sended", true),
            ("registry_quarter", null),
            ("registry_year", null),
            ("performers", new[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 101L,
                    ["list_key"] = "performer-101",
                    ["employee_id"] = 10L,
                    ["name"] = "Первый исполнитель",
                    ["priority"] = 0
                },
                new Dictionary<string, object?>
                {
                    ["id"] = 102L,
                    ["list_key"] = "performer-102",
                    ["employee_id"] = 11L,
                    ["name"] = "Второй исполнитель",
                    ["priority"] = 1
                }
            })));
        stage.Status = new StatusEditState(5L, "Закрыт");
        stage.CompletedAt = new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero);
        stage.RideOutAt = new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero);
        stage.SendedAt = null;
        stage.ClosedAt = new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero);
        stage.IsRideOut = true;
        stage.IsSended = false;
        stage.RegistryQuarter = 2;
        stage.RegistryYear = 2026;

        var payload = StageOziEditPayloadBuilder.BuildForUpdate(
            stage,
            [
                new StagePerformerEditState(101L, "performer-101", 10L, "Первый исполнитель", 0),
                new StagePerformerEditState(null, null, 12L, "Новый исполнитель", 1)
            ],
            " comment ",
            7);

        Assert.Equal(15L, payload["id"]);
        Assert.Equal("stage-key", payload["list_key"]);
        Assert.Equal(5L, payload["status_id"]);
        Assert.Equal("Tue May 12 2026", payload["completed_at"]);
        Assert.Equal("Sun May 10 2026", payload["ride_out_at"]);
        Assert.Null(payload["sended_at"]);
        Assert.Equal("Thu May 14 2026", payload["closed_at"]);
        Assert.Equal(true, payload["is_ride_out"]);
        Assert.Equal(false, payload["is_sended"]);
        Assert.Equal(2, payload["registry_quarter"]);
        Assert.Equal(2026, payload["registry_year"]);

        var performers = Assert.IsAssignableFrom<IReadOnlyList<Dictionary<string, object?>>>(payload["performers_attributes"]);
        Assert.Equal(2, performers.Count);
        Assert.Equal(12L, performers[0]["employee_id"]);
        Assert.Equal("Новый исполнитель", performers[0]["name"]);
        Assert.Equal(1, performers[0]["priority"]);
        Assert.True(Guid.TryParse(Assert.IsType<string>(performers[0]["list_key"]), out _));
        Assert.Equal("1", performers[1]["_destroy"]);
        Assert.Equal(102L, performers[1]["id"]);
        Assert.Equal("performer-102", performers[1]["list_key"]);
        Assert.Equal(11L, performers[1]["employee_id"]);

        var comments = Assert.IsType<Dictionary<string, object?>[]>(payload["comments_attributes"]);
        Assert.Equal("comment", comments[0]["content"]);
        Assert.Equal(7, comments[0]["profile_id"]);
    }

    [Fact]
    public void BuildForUpdate_ReturnsOnlyIdentity_WhenNothingChanged()
    {
        var stage = StageEditState.FromRow(CreateRow(
            ("id", 15L),
            ("list_key", "stage-key"),
            ("status_id", 2L),
            ("status.name", "В работе"),
            ("completed_at", "Tue May 12 2026"),
            ("ride_out_at", "Sun May 10 2026"),
            ("sended_at", null),
            ("closed_at", null),
            ("is_ride_out", true),
            ("is_sended", false),
            ("registry_quarter", 2),
            ("registry_year", 2026),
            ("performers", new[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 101L,
                    ["list_key"] = "performer-101",
                    ["employee_id"] = 10L,
                    ["name"] = "Первый исполнитель",
                    ["priority"] = 0
                }
            })));

        var payload = StageOziEditPayloadBuilder.BuildForUpdate(
            stage,
            stage.Performers,
            null,
            7);

        Assert.Equal(2, payload.Count);
        Assert.Equal(15L, payload["id"]);
        Assert.Equal("stage-key", payload["list_key"]);
    }

    private static ReferenceDataRow CreateRow(params (string Key, object? Value)[] values)
    {
        return new ReferenceDataRow
        {
            Values = values.ToDictionary(
                static value => value.Key,
                static value => JsonSerializer.SerializeToElement(value.Value))
        };
    }
}
