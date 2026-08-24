using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContractClipboardFormatterTests
{
    [Fact]
    public void BuildForStage_UsesRevisionDescriptionDatesAndResponsibleFullNames()
    {
        var contract = CreateContractState(signedAt: "Sun May 03 2026");
        var revision = RevisionEditState.CreateNew(0, "Договор поставки");
        var stage = StageEditState.FromRow(CreateRow(
            ("id", 501L),
            ("start_at", "Mon May 04 2026"),
            ("deadline_at", "Thu May 14 2026")));

        var text = ContractClipboardFormatter.BuildForStage(contract, revision, stage);

        Assert.Equal(
            $"ООО Ромашка Договор поставки EXT-77 от 03.05.2026 | работы проводятся с 04.05.2026 до 14.05.2026{Environment.NewLine}"
            + "Иванов Иван Иванович, Петров Пётр Петрович",
            text);
    }

    [Fact]
    public void BuildForContract_WithoutSignedDate_OmitsDatePrefix()
    {
        var contract = CreateContractState(signedAt: null);
        var revision = RevisionEditState.CreateNew(0, "Дополнительное соглашение");

        var text = ContractClipboardFormatter.BuildForContract(contract, revision);

        Assert.Equal(
            $"ООО Ромашка Дополнительное соглашение EXT-77{Environment.NewLine}"
            + "Иванов Иван Иванович, Петров Пётр Петрович",
            text);
        Assert.DoesNotContain(" от ", text);
        Assert.DoesNotContain("Ошибочный тип", text);
    }

    private static ContractEditState CreateContractState(string? signedAt)
    {
        return ContractEditState.FromRow(CreateRow(
            ("id", 77L),
            ("status_id", 1L),
            ("status.name", "Подписан"),
            ("contragent.name", "ООО Ромашка"),
            ("task_kind.name", "Ошибочный тип"),
            ("external_number", "EXT-77"),
            ("signed_at", signedAt),
            ("contract_responsibles", new object[]
            {
                new
                {
                    id = 41L,
                    employee_id = 101L,
                    employee = new { full_name = "Иванов Иван Иванович" }
                },
                new
                {
                    id = 42L,
                    employee_id = 102L,
                    employee = new { full_name = "Петров Пётр Петрович" }
                }
            })))!;
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
