using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContractCommerEditDialogTests
{
    private static readonly string DialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Functional",
        "ContractCommerEditDialog.cs");

    [Fact]
    public void StageTreeName_SeparatesCreateStateFromPersistedApiContract()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("if (stage.Id > 0)", code);
        Assert.Contains("Stage read model must contain Stage.name for contract stages tree.", code);
        Assert.Contains("return \"Новый этап\";", code);
        Assert.Contains("return $\"Э{priority}_{stage.TaskKind.Name}\";", code);
        Assert.Contains("treeHeader.Text = GetStageTreeName(stage);", code);
    }
}
