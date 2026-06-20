using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageOziEditDialogTests
{
    private static readonly string DialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "StageOziEditDialog.cs");

    private static readonly string ViewPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "StageOziEditView.xaml");

    [Fact]
    public void StageOziEditDialog_KeepsSummaryFreeOfInputDuplicates()
    {
        var code = File.ReadAllText(DialogPath);
        var xaml = File.ReadAllText(ViewPath);

        Assert.Contains("public sealed class StageOziEditDialog : AppEditDialog", code);
        Assert.Contains("x:Name=\"StageTitleText\"", xaml);
        Assert.DoesNotContain("x:Name=\"StageStatus", xaml);
        Assert.DoesNotContain("Text=\"Статус этапа\"", ExtractSummaryXaml(xaml));
        Assert.DoesNotContain("Text=\"Выполнили\"", xaml);
        Assert.DoesNotContain("Text=\"Выезд\"", xaml);
        Assert.DoesNotContain("Text=\"Отправка\"", xaml);
    }

    [Fact]
    public void StageOziEditDialog_GroupsExecutionInputsInLeftColumn()
    {
        var xaml = File.ReadAllText(ViewPath);
        var executionTitleIndex = xaml.IndexOf("Text=\"Выполнение\"", StringComparison.Ordinal);
        var performersIndex = xaml.IndexOf("x:Name=\"PerformersHost\"", StringComparison.Ordinal);
        var rideOutIndex = xaml.IndexOf("x:Name=\"RideOutCheckHost\"", StringComparison.Ordinal);
        var sendedIndex = xaml.IndexOf("x:Name=\"SendedCheckHost\"", StringComparison.Ordinal);
        var stateTitleIndex = xaml.IndexOf("Text=\"Состояние\"", StringComparison.Ordinal);

        Assert.True(executionTitleIndex >= 0);
        Assert.True(executionTitleIndex < performersIndex);
        Assert.True(performersIndex < rideOutIndex);
        Assert.True(rideOutIndex < sendedIndex);
        Assert.True(sendedIndex < stateTitleIndex);
    }

    [Fact]
    public void StageOziEditDialog_ShowsCompletionAndClosedDatesInFixedColumns()
    {
        var xaml = File.ReadAllText(ViewPath);
        var statusIndex = xaml.IndexOf("Text=\"Статус этапа\"", StringComparison.Ordinal);
        var completedIndex = xaml.IndexOf("Text=\"Работа выполнена\"", StringComparison.Ordinal);
        var closedIndex = xaml.IndexOf("Text=\"Закрыт\"", StringComparison.Ordinal);
        var registryIndex = xaml.IndexOf("x:Name=\"RegistryCheckHost\"", StringComparison.Ordinal);

        Assert.True(statusIndex >= 0);
        Assert.True(statusIndex < completedIndex);
        Assert.True(completedIndex < closedIndex);
        Assert.True(closedIndex < registryIndex);
        Assert.Contains("<ColumnDefinition Width=\"116\" />", xaml);
        Assert.DoesNotContain("Text=\"Выполнили\"", xaml);
    }

    [Fact]
    public void StageOziEditDialog_UsesSharedStageNavigationControls()
    {
        var code = File.ReadAllText(DialogPath);
        var xaml = File.ReadAllText(ViewPath);

        Assert.Contains("StageEditDialogNavigationState? navigationState = null", code);
        Assert.Contains("Func<StageEditDialogNavigationDirection, Task<StageEditDialogNavigationResult?>>? navigateAsync = null", code);
        Assert.Contains("x:Name=\"PreviousStageButton\"", xaml);
        Assert.Contains("x:Name=\"NextStageButton\"", xaml);
        Assert.Contains("InitializeNavigationButton(_view.PreviousButton", code);
        Assert.Contains("InitializeNavigationButton(_view.NextButton", code);
        Assert.Contains("private async void RequestNavigation(StageEditDialogNavigationDirection direction)", code);
        Assert.Contains("Content = BuildContent();", code);
        Assert.DoesNotContain("ReplaceDialogBody", code);
    }

    [Fact]
    public void StageOziEditDialog_UsesXamlViewAndDropdownStatus()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("private readonly StageOziEditView _view = new();", code);
        Assert.Contains("scrollViewer.Content = _view;", code);
        Assert.Contains("private readonly Dropdown _statusBox = new();", code);
        Assert.Contains("ConfigureStatusDropdown(_statusBox", code);
        Assert.DoesNotContain("private readonly ComboBox _statusBox", code);
    }

    private static string ExtractSummaryXaml(string xaml)
    {
        var end = xaml.IndexOf("<Border\r\n            Grid.Row=\"1\"", StringComparison.Ordinal);
        if (end < 0)
        {
            end = xaml.IndexOf("<Border\n            Grid.Row=\"1\"", StringComparison.Ordinal);
        }

        Assert.True(end > 0);
        return xaml[..end];
    }
}
