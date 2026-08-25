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

    private static readonly string CommentBoxXamlPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "CommentBox.xaml");

    private static readonly string CommentBoxCodePath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "CommentBox.xaml.cs");

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
        Assert.Contains("_view.ExternalNumberInput.Text = _contract?.ExternalNumber ?? string.Empty;", code);
        Assert.Contains("_contract.BuildExternalNumberPayload(_view.ExternalNumberInput.Text)", code);
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
        var fundedIndex = xaml.IndexOf("x:Name=\"FundedAtEditor\"", StringComparison.Ordinal);
        var commentIndex = xaml.IndexOf("x:Name=\"CommentEditor\"", StringComparison.Ordinal);

        Assert.True(fundedIndex >= 0);
        Assert.True(fundedIndex < commentIndex);
        Assert.Contains("<Grid Grid.Row=\"1\" ColumnSpacing=\"14\">", xaml);
        Assert.Contains("<ColumnDefinition Width=\"116\" />", xaml);
        Assert.Contains("Background=\"#EFFFF2\"", xaml);
        Assert.Contains("<controls:CalendarInput", xaml);
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

        Assert.Contains("_view.ExternalNumberInput.TabIndex = 0;", code);
        Assert.Contains("_view.InvoiceAtInput.TabIndex = 1;", code);
        Assert.Contains("_view.PaymentAtInput.TabIndex = 2;", code);
        Assert.Contains("_view.PrepaymentAtInput.TabIndex = 3;", code);
        Assert.Contains("_view.FundedAtInput.TabIndex = 4;", code);
        Assert.Contains("_view.CommentInput.TabIndex = 5;", code);
        Assert.Contains("_view.InvoiceAtInput.OnTab = (_, args) => FocusDateEditor(_view.PaymentAtInput, args);", code);
        Assert.Contains("_view.FundedAtInput.OnTab = (_, args) => FocusTextBox(_view.CommentInput, args);", code);
    }

    [Fact]
    public void StageFinEditDialog_FocusesExternalNumberOnOpen()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("FocusExternalNumberBox();", code);
        Assert.Contains("_view.ExternalNumberInput.Focus(FocusState.Programmatic);", code);
        Assert.Contains("_view.ExternalNumberInput.Select(0, _view.ExternalNumberInput.Text.Length);", code);
    }

    [Fact]
    public void StageFinEditView_OwnsAllEditorsInStaticXaml()
    {
        var code = File.ReadAllText(DialogPath);
        var xaml = File.ReadAllText(ViewPath);

        Assert.Contains("x:Name=\"ExternalNumberEditor\"", xaml);
        Assert.Contains("x:Name=\"InvoiceAtEditor\"", xaml);
        Assert.Contains("x:Name=\"PaymentAtEditor\"", xaml);
        Assert.Contains("x:Name=\"PrepaymentAtEditor\"", xaml);
        Assert.Contains("x:Name=\"FundedAtEditor\"", xaml);
        Assert.Contains("x:Name=\"CommentEditor\"", xaml);
        Assert.Contains("x:Name=\"CommentsList\"", xaml);
        Assert.DoesNotContain("new CalendarInput", code);
        Assert.DoesNotContain("new TextBox", code);
        Assert.DoesNotContain("InitializeEditorSlots", code);
    }

    [Fact]
    public void CommentBox_UsesStaticListTemplate()
    {
        var xaml = File.ReadAllText(CommentBoxXamlPath);
        var code = File.ReadAllText(CommentBoxCodePath);

        Assert.Contains("<DataTemplate x:Key=\"CommentItemTemplate\">", xaml);
        Assert.Contains("<ListView", xaml);
        Assert.Contains("ItemTemplate=\"{StaticResource CommentItemTemplate}\"", xaml);
        Assert.DoesNotContain("CommentsPanel.Children", code);
        Assert.DoesNotContain("BuildCommentRow", code);
    }
}
