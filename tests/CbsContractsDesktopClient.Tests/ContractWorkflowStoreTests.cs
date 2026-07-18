using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContractWorkflowStoreTests
{
    [Fact]
    public void BeginContractEdit_ForNewContractCreatesZeroStageAndContractRevision()
    {
        var store = new ContractWorkflowStore();
        var contract = CreateRow(
            ("status", Status(0, "В проекте")),
            ("task_kind", TaskKind("Договор")));

        store.BeginContractEdit(contract);

        var stage = Assert.Single(store.ContractStageEditStates);
        Assert.Equal(0, stage.Priority);
        Assert.True(stage.Used);
        Assert.Equal("calendar_days", stage.DeadlineKind);

        var revision = Assert.Single(store.ContractRevisionEditStates);
        Assert.Equal(0, revision.Priority);
        Assert.Equal("Договор", revision.Description);
        Assert.True(revision.Used);
    }

    [Fact]
    public void BeginStageEdit_SelectsStageEditStateMatchingSelectedStageId()
    {
        var store = new ContractWorkflowStore();
        var selectedStage = CreateRow(("id", 200L));
        var contract = CreateRow(
            ("id", 10L),
            ("status", Status(1, "Подписан")),
            ("stages", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 100L,
                    ["priority"] = 1,
                    ["used"] = false
                },
                new Dictionary<string, object?>
                {
                    ["id"] = 200L,
                    ["priority"] = 2,
                    ["used"] = true
                }
            }));

        store.BeginStageEdit(contract, selectedStage);

        Assert.Equal(200L, store.SelectedStageEditState?.Id);
        Assert.Equal(2, store.SelectedStageEditState?.Priority);
        Assert.Equal([1, 2], store.ContractStageEditStates.Select(static stage => stage.Priority));
    }

    [Fact]
    public void BeginStageEdit_ThrowsWhenContractDoesNotContainSelectedStage()
    {
        var store = new ContractWorkflowStore();
        var selectedStage = CreateRow(("id", 300L));
        var contract = CreateRow(
            ("id", 10L),
            ("status", Status(1, "Подписан")),
            ("stages", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 100L,
                    ["priority"] = 1
                }
            }));

        var exception = Assert.Throws<InvalidOperationException>(() => store.BeginStageEdit(contract, selectedStage));

        Assert.Equal("Contract edit graph does not contain selected stage.", exception.Message);
    }

    [Fact]
    public void AddStageAfter_ConvertsSingleZeroStageToMultistageNumbering()
    {
        var store = new ContractWorkflowStore();
        var stage = StageEditState.CreateNew(0, used: true);
        store.SetContractStageEditStates([stage]);

        store.AddStageAfter(stage);

        Assert.Equal([1, 2], store.ContractStageEditStates.Select(static item => item.Priority));
        Assert.True(store.ContractStageEditStates.Single(static item => item.Priority == 1).Used);
        Assert.False(store.ContractStageEditStates.Single(static item => item.Priority == 2).Used);
    }

    [Fact]
    public void DeleteStage_WhenOnlyOneStageRemainsResetsItToZeroAndActive()
    {
        var store = new ContractWorkflowStore();
        var first = StageEditState.CreateNew(0, used: true);
        store.SetContractStageEditStates([first]);
        store.AddStageAfter(first);

        store.DeleteStage(store.ContractStageEditStates.Single(static item => item.Priority == 2));

        var remaining = Assert.Single(store.ContractStageEditStates, static item => !item.IsDestroyed);
        Assert.Equal(0, remaining.Priority);
        Assert.True(remaining.Used);
    }

    [Fact]
    public void DeleteStage_RejectsDeletingLastStage()
    {
        var store = new ContractWorkflowStore();
        var stage = StageEditState.CreateNew(0, used: true);
        store.SetContractStageEditStates([stage]);

        var exception = Assert.Throws<InvalidOperationException>(() => store.DeleteStage(stage));

        Assert.Equal("Нельзя удалить последний этап.", exception.Message);
    }

    [Fact]
    public void SetActiveStage_KeepsExactlyOneActiveStage()
    {
        var store = new ContractWorkflowStore();
        var first = StageEditState.CreateNew(1, used: true);
        var second = StageEditState.CreateNew(2);
        store.SetContractStageEditStates([first, second]);

        store.SetActiveStage(second);

        Assert.False(first.Used);
        Assert.True(second.Used);
        Assert.Same(second, store.SelectedStageEditState);
    }

    [Fact]
    public void TrySelectAdjacentStageEditState_MovesSelectionByStagePriority()
    {
        var store = new ContractWorkflowStore();
        var first = StageEditState.CreateNew(1, used: true);
        var second = StageEditState.CreateNew(2);
        var third = StageEditState.CreateNew(3);
        store.SetContractStageEditStates([third, first, second]);
        store.SetActiveStage(second);

        Assert.True(store.TrySelectAdjacentStageEditState(-1));
        Assert.Same(first, store.SelectedStageEditState);
        Assert.False(store.TrySelectAdjacentStageEditState(-1));
        Assert.Same(first, store.SelectedStageEditState);

        Assert.True(store.TrySelectAdjacentStageEditState(1));
        Assert.Same(second, store.SelectedStageEditState);
        Assert.True(store.TrySelectAdjacentStageEditState(1));
        Assert.Same(third, store.SelectedStageEditState);
        Assert.False(store.TrySelectAdjacentStageEditState(1));
    }

    [Fact]
    public void AddRevisionAfter_CreatesAdditionalAgreementWithNextPriority()
    {
        var store = new ContractWorkflowStore();
        var contractRevision = RevisionEditState.CreateNew(0, "Договор");
        store.SetContractRevisionEditStates([contractRevision]);

        store.AddRevisionAfter(contractRevision);

        var revision = Assert.Single(store.ContractRevisionEditStates, static item => item.Priority == 1);
        Assert.Equal("Доп.соглашение", revision.Description);
        Assert.True(revision.Used);
    }

    [Fact]
    public void DeleteRevision_RejectsDeletingFirstAdditionalAgreementWhenLaterRevisionsExist()
    {
        var store = new ContractWorkflowStore();
        var contractRevision = RevisionEditState.CreateNew(0, "Договор");
        var firstAgreement = RevisionEditState.CreateNew(1, "Доп.соглашение");
        var secondAgreement = RevisionEditState.CreateNew(2, "Доп.соглашение");
        store.SetContractRevisionEditStates([contractRevision, firstAgreement, secondAgreement]);

        var exception = Assert.Throws<InvalidOperationException>(() => store.DeleteRevision(firstAgreement));

        Assert.Equal("Нельзя удалить ревизию № 1, пока существуют ревизии с большим номером.", exception.Message);
    }

    [Fact]
    public void SetContractSelection_CombinesContractCommentsWithEveryStageComment()
    {
        var store = new ContractWorkflowStore();
        var contract = CreateRow(
            ("id", 10),
            ("status", Status(1, "Подписан")),
            ("comments", new object[]
            {
                new Dictionary<string, object?> { ["id"] = 30, ["content"] = "contract" }
            }),
            ("stages", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 100,
                    ["comments"] = new object[]
                    {
                        new Dictionary<string, object?> { ["id"] = 20, ["content"] = "stage 100" }
                    }
                },
                new Dictionary<string, object?>
                {
                    ["id"] = 200,
                    ["comments"] = new object[]
                    {
                        new Dictionary<string, object?> { ["id"] = 10, ["content"] = "stage 200" }
                    }
                }
            }));

        store.SetContractSelection(contract, contract, contragent: null);

        Assert.Equal([10L, 20L, 30L], store.Comments.Select(GetCommentId));
        Assert.Equal(["stage 200", "stage 100", "contract"], store.Comments.Select(GetCommentText));
    }

    [Fact]
    public void SetStageSelection_CombinesContractCommentsWithSelectedStageCommentsOnly()
    {
        var store = new ContractWorkflowStore();
        var selectedStage = CreateRow(("id", 100));
        var contract = CreateRow(
            ("id", 10),
            ("status", Status(1, "Подписан")),
            ("comments", new object[]
            {
                new Dictionary<string, object?> { ["id"] = 30, ["content"] = "contract" }
            }),
            ("stages", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 100,
                    ["comments"] = new object[]
                    {
                        new Dictionary<string, object?> { ["id"] = 20, ["content"] = "selected stage" }
                    }
                },
                new Dictionary<string, object?>
                {
                    ["id"] = 200,
                    ["comments"] = new object[]
                    {
                        new Dictionary<string, object?> { ["id"] = 10, ["content"] = "other stage" }
                    }
                }
            }));

        store.SetStageSelection(selectedStage, contract, contragent: null);

        Assert.Equal([20L, 30L], store.Comments.Select(GetCommentId));
        Assert.Equal(["selected stage", "contract"], store.Comments.Select(GetCommentText));
    }

    [Fact]
    public void SetStageSelection_UsesSelectedRowCommentsWhenTheyArePresent()
    {
        var store = new ContractWorkflowStore();
        var selectedStage = CreateRow(
            ("id", 100),
            ("comments", new object[]
            {
                new Dictionary<string, object?> { ["id"] = 5, ["content"] = "list selected stage" }
            }));
        var contract = CreateRow(
            ("id", 10),
            ("status", Status(1, "Подписан")),
            ("comments", new object[]
            {
                new Dictionary<string, object?> { ["id"] = 30, ["content"] = "contract" }
            }),
            ("stages", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 100,
                    ["comments"] = new object[]
                    {
                        new Dictionary<string, object?> { ["id"] = 20, ["content"] = "edit selected stage" }
                    }
                }
            }));

        store.SetStageSelection(selectedStage, contract, contragent: null);

        Assert.Equal([5L, 30L], store.Comments.Select(GetCommentId));
        Assert.Equal(["list selected stage", "contract"], store.Comments.Select(GetCommentText));
    }

    [Fact]
    public void ApplyCommentReadModel_RefreshesCommentsWithoutResettingStageEditState()
    {
        var store = new ContractWorkflowStore();
        var selectedStage = CreateRow(("id", 100));
        var contract = CreateRow(
            ("id", 10),
            ("status", Status(1, "Подписан")),
            ("comments", Array.Empty<object>()),
            ("stages", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 100,
                    ["priority"] = 1,
                    ["comments"] = Array.Empty<object>()
                }
            }));
        store.SetStageSelection(selectedStage, contract, contragent: null);
        var editState = Assert.IsType<StageEditState>(store.SelectedStageEditState);
        editState.Duration = 42;

        var refreshedContract = CreateRow(
            ("id", 10),
            ("status", Status(1, "Подписан")),
            ("comments", new object[]
            {
                new Dictionary<string, object?> { ["id"] = 30, ["content"] = "contract comment" }
            }),
            ("stages", new object[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = 100,
                    ["priority"] = 1,
                    ["comments"] = new object[]
                    {
                        new Dictionary<string, object?> { ["id"] = 20, ["content"] = "stage comment" }
                    }
                }
            }));

        store.ApplyCommentReadModel(refreshedContract, commentedStageId: 100);

        Assert.Same(refreshedContract, store.Contract);
        Assert.Same(editState, store.SelectedStageEditState);
        Assert.Equal(42, store.SelectedStageEditState.Duration);
        Assert.Equal([20L, 30L], store.Comments.Select(GetCommentId));
        Assert.Equal(["stage comment", "contract comment"], store.Comments.Select(GetCommentText));
    }

    [Fact]
    public void SetContractSelection_SelectsUsedStageAndBuildsStandardFooter()
    {
        var store = new ContractWorkflowStore();
        var contract = CreateRow(
            ("id", 10),
            ("status", Status(1, "Подписан")),
            ("stages", new object[]
            {
                Stage(100, "Поставка", used: false, priority: 1, tasks: ["Монтаж"], performers: ["Иванов"]),
                Stage(200, "Пусконаладка", used: true, priority: 2, tasks: ["Обучение"], performers: ["Петров", "Сидоров"])
            }));

        store.SetContractSelection(contract, contract, contragent: null);

        Assert.Equal(200L, GetRowId(store.SelectedStage));
        Assert.Equal("(ID: 10) Э2 - Пусконаладка|Обучение|Петров, Сидоров", store.SelectedFooterText);
    }

    [Fact]
    public void SetRevisionSelection_SelectsFirstStageWhenNoStageIsUsed()
    {
        var store = new ContractWorkflowStore();
        var revision = CreateRow(("id", 30), ("contract", new Dictionary<string, object?> { ["id"] = 10 }));
        var contract = CreateRow(
            ("id", 10),
            ("status", Status(1, "Подписан")),
            ("stages", new object[]
            {
                Stage(100, "Поставка", used: false),
                Stage(200, "Пусконаладка", used: false)
            }));

        store.SetRevisionSelection(revision, contract, contragent: null);

        Assert.Equal(100L, GetRowId(store.SelectedStage));
        Assert.Equal("(ID: 30) Поставка|нет|нет", store.SelectedFooterText);
    }

    [Fact]
    public void SetStageSelection_UsesSelectedStageForStandardFooter()
    {
        var store = new ContractWorkflowStore();
        var selectedStage = CreateRow(
            ("id", 100),
            ("priority", 3),
            ("task_kind", TaskKind("Исполнение")),
            ("tasks", new object[] { Named("Согласование") }),
            ("performers", new object[] { Named("Иванов") }));
        var contract = CreateRow(
            ("id", 10),
            ("status", Status(1, "Подписан")),
            ("stages", new object[]
            {
                Stage(200, "Другой этап", used: true)
            }));

        store.SetStageSelection(selectedStage, contract, contragent: null);

        Assert.Equal(100L, GetRowId(store.SelectedStage));
        Assert.Equal("(ID: 100) Э3 - Исполнение|Согласование|Иванов", store.SelectedFooterText);
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

    private static Dictionary<string, object?> Status(long id, string name)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = id,
            ["name"] = name
        };
    }

    private static Dictionary<string, object?> Stage(
        long id,
        string taskKindName,
        bool used,
        int? priority = null,
        string[]? tasks = null,
        string[]? performers = null)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = id,
            ["used"] = used,
            ["priority"] = priority,
            ["task_kind"] = TaskKind(taskKindName),
            ["tasks"] = (tasks ?? Array.Empty<string>()).Select(Named).ToArray(),
            ["performers"] = (performers ?? Array.Empty<string>()).Select(Named).ToArray()
        };
    }

    private static Dictionary<string, object?> TaskKind(string name)
    {
        return new Dictionary<string, object?>
        {
            ["name"] = name
        };
    }

    private static Dictionary<string, object?> Named(string name)
    {
        return new Dictionary<string, object?>
        {
            ["name"] = name
        };
    }

    private static long? GetRowId(TableDataRow? row)
    {
        return row?.GetValue("id") switch
        {
            long longValue => longValue,
            int intValue => intValue,
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt64(out var id) => id,
            _ => null
        };
    }

    private static long? GetCommentId(TableDataRow comment)
    {
        return comment.GetValue("id") switch
        {
            long longValue => longValue,
            int intValue => intValue,
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt64(out var id) => id,
            _ => null
        };
    }

    private static string? GetCommentText(TableDataRow comment)
    {
        return comment.GetValue("content")?.ToString();
    }
}
