using System.Text.Json;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContractCommerEditPayloadBuilderTests
{
    [Fact]
    public void BuildCommentUpdate_ContainsOnlyCommentMutationFields()
    {
        var payload = ContractCommerEditPayloadBuilder.BuildCommentUpdate(
            15L,
            "contract-list-key",
            "  новый комментарий  ",
            7);

        Assert.Equal(3, payload.Count);
        Assert.Equal(15L, payload["id"]);
        Assert.Equal("contract-list-key", payload["list_key"]);
        var comments = Assert.IsType<Dictionary<string, object?>[]>(payload["comments_attributes"]);
        var comment = Assert.Single(comments);
        Assert.Equal("новый комментарий", comment["content"]);
        Assert.Equal(7, comment["profile_id"]);
    }

    [Fact]
    public void Build_CreatePayload_MatchesAddContractJsonContract()
    {
        var stage = StageEditState.FromRow(CreateRow(
            ("id", 0L),
            ("list_key", "757c3799-6a01-4187-bb30-7b6c17ad1e18")));
        stage.Priority = 0;
        stage.Used = true;
        stage.Cost = 124324m;
        stage.TaskKind = new TaskKindEditState(1L, "Договор", "02");
        stage.DeadlineKind = "calendar_days";
        stage.Duration = 13;
        stage.PaymentDeadlineKind = "c_plan";
        stage.PaymentDeadlineAt = new DateTimeOffset(2026, 6, 27, 0, 0, 0, TimeSpan.Zero);
        stage.Tasks =
        [
            new StageTaskEditState(null, "319fa49a-2a0b-44e3-8589-f9e897313e8d", 10L, "Поставка СЭИ"),
            new StageTaskEditState(null, "671e37c1-bb1d-4097-a516-b9e5ab49c3c7", 12L, "Поставка ПО")
        ];
        stage.Comment = "комментарий этапа";

        var revision = RevisionEditState.FromRow(CreateRow(
            ("list_key", "dbc8292c-5f9e-44f0-ba40-d29daab84493"),
            ("priority", 0L)));
        revision.IsPresent = true;
        revision.Description = "Договор";
        revision.DocLink = @"C:\Projects\cbs_contracts_webclient\README.md";

        var contract = ContractCommerEditPayloadBuilder.Build(
            CreateRow(),
            new ContractCommerEditPayloadInput(
                IsCreateMode: true,
                Id: null,
                ListKey: null,
                TaskKindId: 1L,
                Code: "02",
                Year: 2026,
                Order: null,
                ContragentId: 1007L,
                StatusId: 0L,
                SignedAt: null,
                Comment: "комментарий контракта",
                Governmental: true,
                ExternalNumber: "1234567",
                DeadlineAt: null,
                ClosedAt: null,
                ProfileId: 1),
            [stage],
            [revision]);

        var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["data_set"] = "item",
            ["Contract"] = contract
        };

        Assert.Equal(
            """{"data_set":"item","Contract":{"task_kind_id":1,"code":"02","year":2026,"order":null,"contragent_id":1007,"status_id":0,"governmental":true,"external_number":"1234567","stages_attributes":[{"list_key":"757c3799-6a01-4187-bb30-7b6c17ad1e18","priority":0,"used":true,"cost":124324,"task_kind_id":1,"deadline_kind":"calendar_days","duration":13,"payment_deadline_kind":"c_plan","payment_deadline_at":"Sat Jun 27 2026","tasks_attributes":[{"list_key":"319fa49a-2a0b-44e3-8589-f9e897313e8d","task_kind_id":10},{"list_key":"671e37c1-bb1d-4097-a516-b9e5ab49c3c7","task_kind_id":12}],"comments_attributes":[{"content":"\u043A\u043E\u043C\u043C\u0435\u043D\u0442\u0430\u0440\u0438\u0439 \u044D\u0442\u0430\u043F\u0430","profile_id":1}]}],"revisions_attributes":[{"list_key":"dbc8292c-5f9e-44f0-ba40-d29daab84493","priority":0,"is_signed":false,"is_present":true,"used":true,"description":"\u0414\u043E\u0433\u043E\u0432\u043E\u0440","doc_link":"C:\\Projects\\cbs_contracts_webclient\\README.md"}],"comments_attributes":[{"content":"\u043A\u043E\u043C\u043C\u0435\u043D\u0442\u0430\u0440\u0438\u0439 \u043A\u043E\u043D\u0442\u0440\u0430\u043A\u0442\u0430","profile_id":1}]}}""",
            JsonSerializer.Serialize(request));
    }

    private static TableDataRow CreateRow(params (string Key, object? Value)[] values)
    {
        return new TableDataRow
        {
            Values = values.ToDictionary(
                static value => value.Key,
                static value => JsonSerializer.SerializeToElement(value.Value))
        };
    }
}
