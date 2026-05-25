using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageOziEditDialogTests
{
    private static readonly string DialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "StageOziEditDialog.cs");

    [Fact]
    public void StageOziEditDialog_KeepsSummaryFreeOfInputDuplicates()
    {
        var code = File.ReadAllText(DialogPath);
        var summaryCode = ExtractMethodBody(code, "private UIElement BuildSummaryPanel()");

        Assert.Contains("public sealed class StageOziEditDialog : AppEditDialog", code);
        Assert.DoesNotContain("_stage.Status.Name", summaryCode);
        Assert.DoesNotContain("_stage.Status.Id", summaryCode);
        Assert.DoesNotContain("BuildSummaryLine(\"Выполнили\"", summaryCode);
        Assert.DoesNotContain("BuildSummaryLine(\"Выезд\"", summaryCode);
        Assert.DoesNotContain("BuildSummaryLine(\"Отправка\"", summaryCode);
    }

    [Fact]
    public void StageOziEditDialog_GroupsExecutionInputsInLeftColumn()
    {
        var code = File.ReadAllText(DialogPath);
        var executionTitleIndex = code.IndexOf("BuildSectionTitle(\"Выполнение\")", StringComparison.Ordinal);
        var performersIndex = code.IndexOf("BuildLabeledControl(\"Исполнители\", _performersMultiSelect)", StringComparison.Ordinal);
        var rideOutIndex = code.IndexOf("BuildCheckDateRow(_isRideOutBox, _rideOutAtEditor)", StringComparison.Ordinal);
        var sendedIndex = code.IndexOf("BuildCheckDateRow(_isSendedBox, _sendedAtEditor)", StringComparison.Ordinal);
        var registryIndex = code.IndexOf("left.Children.Add(BuildRegistryEditors())", StringComparison.Ordinal);
        var stateTitleIndex = code.IndexOf("BuildSectionTitle(\"Состояние\")", StringComparison.Ordinal);

        Assert.True(executionTitleIndex >= 0);
        Assert.True(executionTitleIndex < performersIndex);
        Assert.True(performersIndex < rideOutIndex);
        Assert.True(rideOutIndex < sendedIndex);
        Assert.True(sendedIndex < registryIndex);
        Assert.True(registryIndex < stateTitleIndex);
        Assert.DoesNotContain("right.Children.Add(BuildRegistryEditors())", code);
    }

    [Fact]
    public void StageOziEditDialog_ShowsCompletionDateLikeClosedDate()
    {
        var code = File.ReadAllText(DialogPath);
        var statusIndex = code.IndexOf("BuildLabeledControl(\"Статус этапа\", _statusBox)", StringComparison.Ordinal);
        var completedIndex = code.IndexOf("BuildLabeledControl(\"Работа выполнена\", _completedAtEditor)", StringComparison.Ordinal);
        var closedIndex = code.IndexOf("BuildLabeledControl(\"Закрыт\", _closedAtEditor)", StringComparison.Ordinal);

        Assert.True(statusIndex >= 0);
        Assert.True(statusIndex < completedIndex);
        Assert.True(completedIndex < closedIndex);
        Assert.DoesNotContain("BuildInlineDateRow(\"Выполнили\"", code);
    }

    private static string ExtractMethodBody(string code, string signature)
    {
        var start = code.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0);
        var nextMethod = code.IndexOf("    private UIElement BuildEditorsArea()", start, StringComparison.Ordinal);
        Assert.True(nextMethod > start);
        return code[start..nextMethod];
    }
}
