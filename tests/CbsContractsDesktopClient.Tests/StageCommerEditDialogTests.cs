using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class StageCommerEditDialogTests
{
    private static readonly string DialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "StageCommerEditDialog.cs");

    private static readonly string ViewPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "StageCommerEditView.xaml");

    [Fact]
    public void StageCommerEditDialog_LocksAutoCalculatedDeadlineFields()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("public sealed class StageCommerEditDialog : AppEditDialog", code);
        Assert.Contains("scrollViewer.Content = _view;", code);
        Assert.Contains("return BuildEditContent(scrollViewer);", code);
        Assert.Contains("private readonly TextBox _durationBox = BuildNumberTextBox();", code);
        Assert.Contains("private readonly TextBox _paymentDurationBox = BuildNumberTextBox();", code);
        Assert.DoesNotContain("private static TextBox BuildNumberTextBox()", code);
        Assert.Contains("IsDeadlineManualMode(GetSelectedDeadlineKind())", code);
        Assert.Contains("IsPaymentDeadlineManualMode(GetSelectedPaymentDeadlineKind())", code);
        Assert.Contains("_deadlineAtEditor.IsReadOnly = !deadlineManual;", code);
        Assert.Contains("_paymentDeadlineAtEditor.IsReadOnly = !paymentDeadlineManual;", code);
    }

    [Fact]
    public void StageCommerEditDialog_ValidatesManualDeadlineDates()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("_deadlineAtEditedManually", code);
        Assert.Contains("_paymentDeadlineAtEditedManually", code);
        Assert.Contains("deadlineAt.Date < startAt.Date", code);
        Assert.Contains("paymentDeadlineAt.Date < fundedAt.Value.Date", code);
    }

    [Fact]
    public void StageCommerEditDialog_AutoClosesContractWhenLastOpenStageIsClosed()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("public bool ShouldCloseContract()", code);
        Assert.Contains("_stage.ShouldCloseContract(_contract, StatusClosed)", code);
        Assert.Contains("public IReadOnlyDictionary<string, object?> BuildContractClosePayload()", code);
        Assert.Contains("StageCommerEditPayloadBuilder.BuildContractClosePayload(RequireContract().Id, _closedAtEditor.Date)", code);
    }

    [Fact]
    public void StageCommerEditDialog_UsesCenteredSectionTitlesAndAccentMoney()
    {
        var code = File.ReadAllText(DialogPath);
        var xaml = File.ReadAllText(ViewPath);

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
    public void StageCommerEditDialog_ReadsRequiredContractStatusDirectly()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("RequireContract().Status.Name!", code);
        Assert.Contains("RequireContract().Status.Id", code);
        Assert.DoesNotContain("ResolveContractStatusName", code);
        Assert.DoesNotContain("ResolveContractStatusId", code);
        Assert.DoesNotContain("FindStatusLabel(_statusOptions", code);
    }

    [Fact]
    public void StageCommerEditDialog_UsesSharedStageNavigationControls()
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
        Assert.Contains("Content = BuildContent(statusOptions);", code);
    }

    [Fact]
    public void StageCommerEditDialog_UserBusinessLogicDoesNotRebuildView()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("private void ApplyBusinessLogicAfterFieldChange(bool applyInitialStart)", code);
        Assert.Contains("if (_isApplyingBusinessLogic)", code);
        Assert.Contains("_isApplyingBusinessLogic = true;", code);
        Assert.DoesNotContain("RenderStageContent", ExtractMethod(code, "ApplyBusinessLogicAfterFieldChange"));
        Assert.DoesNotContain("RenderStageContent", ExtractMethod(code, "ApplyStatusBusinessLogic"));
        Assert.Contains("RenderStageContent(\"StageCommerEditDialog.ApplyNavigationResult.apply\")", code);
    }

    private static string ExtractMethod(string code, string methodName)
    {
        var start = code.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Method {methodName} was not found.");

        var braceStart = code.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"Method {methodName} body was not found.");

        var depth = 0;
        for (var index = braceStart; index < code.Length; index++)
        {
            if (code[index] == '{')
            {
                depth++;
            }
            else if (code[index] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return code[braceStart..(index + 1)];
                }
            }
        }

        throw new InvalidOperationException($"Method {methodName} body was not closed.");
    }
}
