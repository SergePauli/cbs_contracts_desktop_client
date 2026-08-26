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

        var contract = ContractCommerEditPayloadBuilder.BuildContractPayload(
            CreateRow(),
            ContractEditState.CreateNew(),
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

    [Fact]
    public void Build_DestroyedPersistedStage_EmitsNestedDestroyAttributes()
    {
        var stage = StageEditState.FromRow(CreateRow(
            ("id", 6393L),
            ("list_key", "stage-list-key")));
        stage.IsDestroyed = true;

        var payload = ContractCommerEditPayloadBuilder.BuildContractPayload(
            CreateRow(),
            CreateContractState(),
            new ContractCommerEditPayloadInput(
                IsCreateMode: false,
                Id: 6156L,
                ListKey: null,
                TaskKindId: null,
                Code: null,
                Year: null,
                Order: null,
                ContragentId: null,
                StatusId: null,
                SignedAt: null,
                Comment: null,
                Governmental: false,
                ExternalNumber: null,
                DeadlineAt: null,
                ClosedAt: null,
                ProfileId: null),
            [stage],
            []);

        var stages = Assert.IsType<List<Dictionary<string, object?>>>(payload["stages_attributes"]);
        var destroyedStage = Assert.Single(stages);
        Assert.Equal(6393L, destroyedStage["id"]);
        Assert.Equal("stage-list-key", destroyedStage["list_key"]);
        Assert.Equal("1", destroyedStage["_destroy"]);
        Assert.Equal(3, destroyedStage.Count);
    }

    [Fact]
    public void BuildSavePlan_UpdatesPersistedStageAndRevisionOutsideContractPayload()
    {
        var sourceRow = CreateRow(
            ("id", 6156L),
            ("status_id", 1L),
            ("status.name", "Подписан"),
            ("governmental", false),
            ("contract_responsibles", Array.Empty<object>()));
        var stage = StageEditState.FromRow(CreateRow(
            ("id", 6393L),
            ("list_key", "stage-list-key"),
            ("start_at", null)));
        stage.StartAt = new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);
        var revision = RevisionEditState.FromRow(CreateRow(
            ("id", 701L),
            ("list_key", "revision-list-key"),
            ("priority", 1L),
            ("description", "Старая редакция")));
        revision.Description = "Новая редакция";

        var plan = ContractCommerEditPayloadBuilder.BuildSavePlan(
            sourceRow,
            ContractEditState.FromRow(sourceRow)!,
            new ContractCommerEditPayloadInput(
                false, 6156L, null, null, null, null, null, null, 1L, null, null,
                false, null, null, null, null),
            [stage],
            [revision]);

        Assert.DoesNotContain("stages_attributes", plan.ContractPayload.Keys);
        Assert.DoesNotContain("revisions_attributes", plan.ContractPayload.Keys);
        var stagePayload = Assert.Single(plan.StageUpdatePayloads);
        Assert.Equal(6393L, stagePayload["id"]);
        Assert.Equal("Mon Aug 24 2026", stagePayload["start_at"]);
        var revisionPayload = Assert.Single(plan.RevisionUpdatePayloads);
        Assert.Equal(701L, revisionPayload["id"]);
        Assert.Equal("Новая редакция", revisionPayload["description"]);
        Assert.True(plan.HasChanges);
        Assert.False(plan.HasContractChanges);
    }

    [Fact]
    public void Build_ChangedContractResponsibles_EmitsOnlyNestedDelta()
    {
        var sourceRow = CreateRow(
            ("id", 6156L),
            ("status_id", 1L),
            ("status.name", "Подписан"),
            ("governmental", false),
            ("contract_responsibles", new object[]
            {
                new
                {
                    id = 41L,
                    list_key = "responsible-41",
                    employee_id = 101L,
                    employee = new { full_name = "Иванов Иван Иванович" }
                },
                new
                {
                    id = 42L,
                    list_key = "responsible-42",
                    employee_id = 102L,
                    employee = new { full_name = "Петров Пётр Петрович" }
                }
            }));
        var contractState = ContractEditState.FromRow(sourceRow)!;
        contractState.SetContractResponsibles(
        [
            contractState.ContractResponsibles[0],
            new ContractResponsibleEditState(null, null, 103L, "Сидоров Сидор Сидорович")
        ]);

        var payload = ContractCommerEditPayloadBuilder.BuildContractPayload(
            sourceRow,
            contractState,
            new ContractCommerEditPayloadInput(
                IsCreateMode: false,
                Id: 6156L,
                ListKey: null,
                TaskKindId: null,
                Code: null,
                Year: null,
                Order: null,
                ContragentId: null,
                StatusId: 1L,
                SignedAt: null,
                Comment: null,
                Governmental: false,
                ExternalNumber: null,
                DeadlineAt: null,
                ClosedAt: null,
                ProfileId: null),
            [],
            []);

        var attributes = Assert.IsType<List<Dictionary<string, object?>>>(
            payload["contract_responsibles_attributes"]);
        Assert.Equal(2, attributes.Count);

        var added = Assert.Single(
            attributes,
            static item => item.TryGetValue("employee_id", out var employeeId) && Equals(employeeId, 103L));
        Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<string>(added["list_key"])));
        Assert.Equal(2, added.Count);

        var destroyed = Assert.Single(
            attributes,
            static item => item.TryGetValue("id", out var id) && Equals(id, 42L));
        Assert.Equal("responsible-42", destroyed["list_key"]);
        Assert.Equal("1", destroyed["_destroy"]);
        Assert.Equal(3, destroyed.Count);
    }

    [Fact]
    public void Build_UnchangedContractResponsibles_DoesNotEmitNestedAttributes()
    {
        var sourceRow = CreateRow(
            ("id", 6156L),
            ("status_id", 1L),
            ("status.name", "Подписан"),
            ("governmental", false),
            ("contract_responsibles", new object[]
            {
                new
                {
                    id = 41L,
                    employee_id = 101L,
                    employee = new { full_name = "Иванов Иван Иванович" }
                }
            }));
        var contractState = ContractEditState.FromRow(sourceRow)!;

        var payload = ContractCommerEditPayloadBuilder.BuildContractPayload(
            sourceRow,
            contractState,
            new ContractCommerEditPayloadInput(
                false, 6156L, null, null, null, null, null, null, 1L, null, null,
                false, null, null, null, null),
            [],
            []);

        Assert.DoesNotContain("contract_responsibles_attributes", payload.Keys);
    }

    private static ContractEditState CreateContractState()
    {
        return ContractEditState.FromRow(CreateRow(
            ("id", 6156L),
            ("status_id", 1L),
            ("status.name", "Подписан"),
            ("contract_responsibles", Array.Empty<object>())))!;
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
