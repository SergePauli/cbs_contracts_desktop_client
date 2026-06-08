using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContractWorkflowStoreTests
{
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
