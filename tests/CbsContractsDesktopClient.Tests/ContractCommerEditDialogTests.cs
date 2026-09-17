using CbsContractsDesktopClient.Views.Functional;
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
        Assert.Contains("treeItem.UpdateHeader(GetStageTreeName(stage));", code);
    }

    [Fact]
    public void StageTreeContent_PreservesEditorsAcrossSixExpansionCyclesFor25Stages()
    {
        var created = 0;
        var branches = Enumerable.Range(1, 25).Select(index =>
            new ContractStageTreeItem($"Этап {index}",
                [new ContractStageTreeItem(() =>
                {
                    created++;
                    return new Dictionary<string, string> { ["comment"] = $"Комментарий {index}" };
                })])).ToList();
        Assert.All(branches, branch => Assert.Null(branch.Children[0].Content));
        Assert.Equal(0, created);

        var editors = new List<object>();
        for (var cycle = 0; cycle < 6; cycle++)
        {
            for (var index = 0; index < branches.Count; index++)
            {
                var branch = branches[index];
                branch.IsExpanded = true;
                branch.PrepareVisibleContent();
                var editor = Assert.IsType<Dictionary<string, string>>(branch.Children[0].Content);
                if (cycle == 0)
                {
                    editors.Add(editor);
                    editor["comment"] = $"Введено {index}";
                }
                Assert.Same(editors[index], editor);
                Assert.Equal($"Введено {index}", editor["comment"]);
                branch.IsExpanded = false;
                branch.PrepareVisibleContent();
            }
        }
        Assert.Equal(25, created);
    }

    [Fact]
    public void StageTreeExpansion_UsesSavedStateAndDelegatesChangesToStore()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("stage.Used);", code);
        Assert.Contains("_workflowStore.SetStageExpanded(stage, isExpanded);", code);
        Assert.Contains("_workflowStore.SetAllStagesExpanded(isExpanded);", code);
        Assert.DoesNotContain("var isExpanded = _openStagesTabOnLoad", code);
        Assert.DoesNotContain("BuildActiveStageCheckBox", code);
        Assert.DoesNotContain("\"АЭ\"", code);
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
        var hostCode = File.ReadAllText(TestProjectPaths.FromRepositoryRoot("src", "Views", "Shell", "ContractWorkflowHostViewBase.cs"));

        Assert.Contains("ConfigureSaveWithoutClose(\"Закрыть\");", code);
        Assert.Contains("public void ReloadAsEdit(TableDataRow contract)", code);
        Assert.Contains("public void AcceptCreatedContractIdentity(long id, string? listKey)", code);
        Assert.Contains("DialogTitle = BuildDialogTitle();", code);
        Assert.Contains("_isCreateMode = false;", code);
        Assert.Contains("_workflowStore.BeginContractEdit(contract);", code);
        Assert.Contains("ResetEditorsFromContract();", code);
        Assert.Contains("var createMode = isCreateMode;", hostCode);
        Assert.Contains("var result = await _contractSaveWorkflow.SaveAsync(savePlan);", hostCode);
        Assert.Contains("dialog.AcceptCreatedContractIdentity(", hostCode);
        Assert.Contains("ReloadContractEditRowAsync(result.ContractId)", hostCode);
        Assert.Contains("dialog.ReloadAsEdit(editRow);", hostCode);
        Assert.Contains("createMode = false;", hostCode);
        Assert.True(
            hostCode.IndexOf("dialog.AcceptCreatedContractIdentity(", StringComparison.Ordinal)
            < hostCode.IndexOf("ReloadContractEditRowAsync(result.ContractId)", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveNotification_IsShownInSaveHandlerAndDetailsRefreshAfterClose()
    {
        var hostCode = File.ReadAllText(TestProjectPaths.FromRepositoryRoot(
            "src", "Views", "Shell", "ContractWorkflowHostViewBase.cs"));
        var handlerStart = hostCode.IndexOf("async Task SaveAsync(AppEditDialogSaveRequestedEventArgs args)", StringComparison.Ordinal);
        var handlerEnd = hostCode.IndexOf("dialog.SaveRequestedAsync += SaveAsync;", handlerStart, StringComparison.Ordinal);
        var handlerCode = hostCode[handlerStart..handlerEnd];
        Assert.Contains("ShowSuccessNotification(", handlerCode);
        Assert.DoesNotContain("dialog.ShowAsync()", handlerCode);
        Assert.DoesNotContain("refreshAsync()", handlerCode);
        Assert.True(hostCode.IndexOf("await dialog.ShowAsync()", StringComparison.Ordinal)
            < hostCode.IndexOf("await refreshAsync()", StringComparison.Ordinal));
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
        Assert.Contains("ShowContractWorkflowDialogAsync(dialog,", contractHostCode);
        Assert.Contains("ShowContractWorkflowDialogAsync(dialog,", stageHostCode);
        Assert.Contains("ShowContractWorkflowDialogAsync(dialog,", revisionHostCode);
        Assert.Contains("UpdateAsync(StageModel", workflowCode);
        Assert.Contains("UpdateAsync(RevisionModel", workflowCode);
    }

    [Fact]
    public void EmptyRevisionsTab_OffersFirstRevisionWithoutLegacyFlags()
    {
        var code = File.ReadAllText(DialogPath);
        var xaml = File.ReadAllText(ViewPath);

        Assert.Contains("BuildPlaceholder(\"Нет ревизий контракта\")", code);
        Assert.Contains("Content = \"Добавить ревизию\"", code);
        Assert.Contains("addRevisionButton.Click += ExtAgreementBox_Checked;", code);
        Assert.DoesNotContain("ExtAgreementHost", xaml);
        Assert.DoesNotContain("MultiStageHost", xaml);
        Assert.DoesNotContain("BuildFlagHost", code);
    }

    [Fact]
    public void DialogLoad_DoesNotFocusContractStatus()
    {
        var code = File.ReadAllText(DialogPath);
        var methodStart = code.IndexOf("private async void ContractCommerEditDialog_Loaded", StringComparison.Ordinal);
        var methodEnd = code.IndexOf("private void FocusInitialStageEditor", methodStart, StringComparison.Ordinal);
        var methodCode = code[methodStart..methodEnd];

        Assert.DoesNotContain("_statusBox.Focus", methodCode);
    }

    [Fact]
    public void FileRows_ProvideClearAction()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("BuildFileActionButton(\"\\ue894\", \"Очистить\")", code);
        Assert.Contains("clearButton.Click += (_, _) => editor.Text = string.Empty;", code);
    }
}
