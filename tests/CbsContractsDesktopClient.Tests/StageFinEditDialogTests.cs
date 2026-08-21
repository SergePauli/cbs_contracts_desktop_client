using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageFinEditDialogTests
{
    private static readonly string DialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "StageFinEditDialog.cs");

    private static readonly string ViewPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "StageFinEditView.xaml");

    [Fact]
    public void StageFinEditDialog_UsesCenteredSectionTitlesAndAccentMoney()
    {
        var code = File.ReadAllText(DialogPath);
        var xaml = File.ReadAllText(ViewPath);

        Assert.Contains("public sealed class StageFinEditDialog : AppEditDialog", code);
        Assert.Contains("BuildDialogSectionTitle(RequireContract().GetSectionTitle())", code);
        Assert.Contains("x:Name=\"ContractTitleHost\"", xaml);
        Assert.Contains("x:Name=\"StageTitleText\"", xaml);
        Assert.Contains("TextAlignment=\"Center\"", xaml);
        Assert.Contains("_stage.GetSectionTitleAmount(_contract)", code);
        Assert.Contains("Foreground=\"{StaticResource ShellAccentBrush}\"", xaml);
        Assert.Contains("_view.ContractCostValue.Text = FormatSummaryValue(FormatMoney(_contract?.Cost));", code);
        Assert.DoesNotContain("BuildSummaryLine(\"Стоимость\", FormatMoney(_stage.Cost))", code);
    }

    [Fact]
    public void StageFinEditDialog_ReadsRequiredContractStatusDirectlyAndUsesSharedDynamicSummaryText()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("RequireContract().Status.Name!", code);
        Assert.Contains("RequireContract().Status.Id", code);
        Assert.Contains("_view.StartAtValue.Text = FormatSummaryValue(FormatDisplayDate(_startAt));", code);
        Assert.DoesNotContain("ResolveContractStatusName", code);
        Assert.DoesNotContain("ResolveContractStatusId", code);
        Assert.DoesNotContain("ResolveStageStatusName", code);
        Assert.DoesNotContain("ResolveStageStatusId", code);
        Assert.DoesNotContain("ResolveContractExternalNumber", code);
        Assert.Contains("_externalNumberBox.Text = _contract?.ExternalNumber ?? string.Empty;", code);
        Assert.Contains("_contract.BuildExternalNumberPayload(_externalNumberBox.Text)", code);
        Assert.DoesNotContain("private static TextBlock BuildDynamicSummaryText()", code);
    }

    [Fact]
    public void StageFinEditDialog_UsesSharedStageNavigationControls()
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
    public void StageFinEditView_PlacesFundedDateAndCommentInOneRow()
    {
        var xaml = File.ReadAllText(ViewPath);
        var fundedIndex = xaml.IndexOf("x:Name=\"FundedAtHost\"", StringComparison.Ordinal);
        var commentIndex = xaml.IndexOf("x:Name=\"CommentHost\"", StringComparison.Ordinal);

        Assert.True(fundedIndex >= 0);
        Assert.True(fundedIndex < commentIndex);
        Assert.Contains("<Grid Grid.Row=\"1\" ColumnSpacing=\"14\">", xaml);
        Assert.Contains("<ColumnDefinition Width=\"116\" />", xaml);
        Assert.Contains("Background=\"#EFFFF2\"", xaml);
        Assert.Contains("HorizontalContentAlignment=\"Left\"", xaml);
        Assert.Contains("Text=\"Бух.закрытие\"", xaml);
        Assert.Contains("Text=\"Комментарий\"", xaml);
        Assert.DoesNotContain("Text=\"Дата бух. закрытия\"", xaml);
        Assert.DoesNotContain("Text=\"Дата счета\"", xaml);
        Assert.DoesNotContain("Text=\"Дата оплаты\"", xaml);
    }

    [Fact]
    public void StageFinEditDialog_ConfiguresFastTabChain()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("_externalNumberBox.TabIndex = 0;", code);
        Assert.Contains("_invoiceAtEditor.TabIndex = 1;", code);
        Assert.Contains("_paymentAtEditor.TabIndex = 2;", code);
        Assert.Contains("_prepaymentAtEditor.TabIndex = 3;", code);
        Assert.Contains("_fundedAtEditor.TabIndex = 4;", code);
        Assert.Contains("_commentBox.TabIndex = 5;", code);
        Assert.Contains("_invoiceAtEditor.OnTab = (_, args) => FocusDateEditor(_paymentAtEditor, args);", code);
        Assert.Contains("_fundedAtEditor.OnTab = (_, args) => FocusTextBox(_commentBox, args);", code);
    }

    [Fact]
    public void StageFinEditDialog_FocusesExternalNumberOnOpen()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("FocusExternalNumberBox();", code);
        Assert.Contains("_externalNumberBox.Focus(FocusState.Programmatic);", code);
        Assert.Contains("_externalNumberBox.Select(0, _externalNumberBox.Text.Length);", code);
    }
}
