using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContractCommerEditDialogTests
{
    private static readonly string DialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "ContractCommerEditDialog.cs");
    private static readonly string TreeItemPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "ContractStageTreeItem.cs");
    private static readonly string ViewPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "ContractCommerEditView.xaml");
    private static readonly string HostPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Shell",
        "ContractHostView.cs");

    [Fact]
    public void StageTreeName_SeparatesCreateStateFromPersistedApiContract()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("if (stage.Id > 0)", code);
        Assert.Contains("Stage read model must contain Stage.name for contract stages tree.", code);
        Assert.Contains("return \"Новый этап\";", code);
        Assert.Contains("return $\"Э{priority}_{stage.TaskKind.Name}\";", code);
        Assert.Contains("treeItem.RefreshContent();", code);
    }

    [Fact]
    public void StageTreeContent_IsCreatedPerTemplateInstance()
    {
        var code = File.ReadAllText(DialogPath);
        var treeItemCode = File.ReadAllText(TreeItemPath);
        var xaml = File.ReadAllText(ViewPath);

        Assert.Contains("Func<object> contentFactory", treeItemCode);
        Assert.Contains("public object Content => _contentFactory();", treeItemCode);
        Assert.Contains("Content=\"{x:Bind Content, Mode=OneWay}\"", xaml);
        Assert.Contains("() => BuildStageSection(stage, stageItem!)", code);
        Assert.Contains("() => BuildStageCommentsBox(stage)", code);
        Assert.Contains("() => BuildStageSupplyContent(stage)", code);
        Assert.DoesNotContain("_stageSupplyViews", code);
        Assert.DoesNotContain("StagesTree_Expanding", code);
    }

    [Fact]
    public void StageTreeExpansion_UsesSelectedStageOnlyForStageTableEntryPoint()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("var isExpanded = _openStagesTabOnLoad", code);
        Assert.Contains("? ReferenceEquals(stage, _workflowStore.SelectedStageEditState)", code);
        Assert.Contains(": stage.Used;", code);
        Assert.Contains("isExpanded);", code);
    }

    [Fact]
    public void StageTreeRendering_DoesNotMutateWorkflowState()
    {
        var code = File.ReadAllText(DialogPath);
        var refreshStart = code.IndexOf("private void RefreshStagesStack()", StringComparison.Ordinal);
        var refreshEnd = code.IndexOf("private ContractStageTreeItem BuildStageTreeItem", refreshStart, StringComparison.Ordinal);
        var buildEnd = code.IndexOf("private string GetStageTreeName", refreshEnd, StringComparison.Ordinal);
        var renderCode = code[refreshStart..buildEnd];

        Assert.DoesNotContain("_workflowStore.SetContractStageEditStates", renderCode);
        Assert.DoesNotContain("SyncStageTaskKindFromContract", renderCode);
    }

    [Fact]
    public void SignedDate_AppliesStartRuleToEveryNonDestroyedStage()
    {
        var code = File.ReadAllText(DialogPath);
        var methodStart = code.IndexOf("private void ApplyContractSignedDateToEmptyStageStarts()", StringComparison.Ordinal);
        var methodEnd = code.IndexOf("private void SyncStageDeadlineEditorsFromBusinessRules", methodStart, StringComparison.Ordinal);
        var methodCode = code[methodStart..methodEnd];

        Assert.Contains(".Where(static stage => !stage.IsDestroyed)", code);
        Assert.Contains("foreach (var stage in StageEditors)", methodCode);
        Assert.Contains("StageDeadlineBusinessRules.ResolveStartAfterContractSigned", methodCode);
        Assert.DoesNotContain("stage.Used", methodCode);
    }

    [Fact]
    public void Save_KeepsDialogOpenAndReloadsCreatedContractAsEdit()
    {
        var code = File.ReadAllText(DialogPath);
        var hostCode = File.ReadAllText(HostPath);

        Assert.Contains("ConfigureSaveWithoutClose(\"Закрыть\");", code);
        Assert.Contains("public void ReloadAsEdit(TableDataRow contract)", code);
        Assert.Contains("public void AcceptCreatedContractIdentity(long id, string? listKey)", code);
        Assert.Contains("DialogTitle = BuildDialogTitle();", code);
        Assert.Contains("_isCreateMode = false;", code);
        Assert.Contains("_workflowStore.BeginContractEdit(contract);", code);
        Assert.Contains("ResetEditorsFromContract();", code);
        Assert.Contains("var createMode = isCreateMode;", hostCode);
        Assert.Contains("var savedId = saveResult.ContractId;", hostCode);
        Assert.Contains("dialog.AcceptCreatedContractIdentity(", hostCode);
        Assert.Contains("ReloadContractEditRowAsync(savedId)", hostCode);
        Assert.Contains("dialog.ReloadAsEdit(editRow);", hostCode);
        Assert.Contains("createMode = false;", hostCode);
        Assert.True(
            hostCode.IndexOf("dialog.AcceptCreatedContractIdentity(", StringComparison.Ordinal)
            < hostCode.IndexOf("ReloadContractEditRowAsync(savedId)", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveNotification_IsShownBeforeDialogReloadAndClose()
    {
        var hostCode = File.ReadAllText(HostPath);
        var handlerStart = hostCode.IndexOf("private void AttachContractCommerSaveHandler", StringComparison.Ordinal);
        var handlerEnd = hostCode.IndexOf("private bool IsContractCreateAllowedForCurrentUser", handlerStart, StringComparison.Ordinal);
        var handlerCode = hostCode[handlerStart..handlerEnd];

        Assert.Contains("ShowSuccessNotification(", handlerCode);
        Assert.True(
            handlerCode.IndexOf("ShowSuccessNotification(", StringComparison.Ordinal)
            < handlerCode.IndexOf("ReloadContractEditRowAsync(savedId)", StringComparison.Ordinal));
        Assert.DoesNotContain("dialog.ShowAsync()", handlerCode);
    }

    [Fact]
    public void ContractDialogHosts_UseSeparatedContractStageAndRevisionSaveWorkflow()
    {
        var dialogCode = File.ReadAllText(DialogPath);
        var contractHostCode = File.ReadAllText(HostPath);
        var stageHostCode = File.ReadAllText(TestProjectPaths.FromRepositoryRoot(
            "src", "Views", "Shell", "StageHostView.cs"));
        var revisionHostCode = File.ReadAllText(TestProjectPaths.FromRepositoryRoot(
            "src", "Views", "Shell", "RevisionHostView.cs"));
        var workflowCode = File.ReadAllText(TestProjectPaths.FromRepositoryRoot(
            "src", "ViewModels", "Workflow", "ContractCommerSaveWorkflow.cs"));

        Assert.Contains("public ContractCommerEditSavePlan BuildSavePlan", dialogCode);
        Assert.Contains("dialog.BuildSavePlan", contractHostCode);
        Assert.Contains("dialog.BuildSavePlan", stageHostCode);
        Assert.Contains("dialog.BuildSavePlan", revisionHostCode);
        Assert.Contains("UpdateAsync(StageModel", workflowCode);
        Assert.Contains("UpdateAsync(RevisionModel", workflowCode);
    }
}
