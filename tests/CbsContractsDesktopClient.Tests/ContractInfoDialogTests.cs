using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContractInfoDialogTests
{
    private static readonly string DialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "ContractInfoDialog.cs");

    private static readonly string ViewPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "ContractInfoView.xaml");

    [Fact]
    public void ContractInfoDialog_IsReadOnlyReferenceDialog()
    {
        var code = File.ReadAllText(DialogPath);
        var xaml = File.ReadAllText(ViewPath);

        Assert.Contains("public sealed class ContractInfoDialog : ContentDialog", code);
        Assert.DoesNotContain("BuildPayload", code);
        Assert.DoesNotContain("BuildContractClosePayload", code);
        Assert.DoesNotContain("SaveRequestedAsync", code);
        Assert.DoesNotContain("Validate()", code);
        Assert.DoesNotContain("<TextBox", xaml);
        Assert.DoesNotContain("<ComboBox", xaml);
        Assert.DoesNotContain("<controls:CalendarInput", xaml);
        Assert.DoesNotContain("<pauli:Dropdown", xaml);
    }

    [Fact]
    public void ContractInfoDialog_CollectsStaticProfileSections()
    {
        var code = File.ReadAllText(DialogPath);
        var xaml = File.ReadAllText(ViewPath);

        Assert.Contains("RenderContractSummary", code);
        Assert.Contains("RenderStageSummary", code);
        Assert.Contains("RenderStageDetails", code);
        Assert.Contains("Text=\"Срок выполнения\"", xaml);
        Assert.Contains("Text=\"Оплата\"", xaml);
        Assert.Contains("Text=\"Состояние\"", xaml);
        Assert.Contains("Text=\"Исполнители\"", xaml);
        Assert.Contains("Text=\"Прочие задачи\"", xaml);
        Assert.DoesNotContain("Text=\"Комментарий\"", xaml);
        Assert.Contains("Text=\"В наличии\"", xaml);
        Assert.Contains("Text=\"Создан\"", xaml);
        Assert.Contains("Text=\"Создал\"", xaml);
        Assert.Contains("Text=\"Закрыл\"", xaml);
        Assert.Contains("x:Name=\"ContractStatusHost\"", xaml);
        Assert.Contains("x:Name=\"StageStatusHost\"", xaml);
    }

    [Fact]
    public void ContractInfoDialog_KeepsSharedStageNavigation()
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
        Assert.Contains("RenderContent();", code);
        Assert.DoesNotContain("ReplaceDialogBody", code);
    }
}
