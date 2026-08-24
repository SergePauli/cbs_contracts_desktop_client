using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContractResponsibleUiTests
{
    private static readonly string EmployeeBoxPath = TestProjectPaths.FromRepositoryRoot(
        "src", "Views", "References", "EmployeeBox.xaml.cs");
    private static readonly string ContractDetailPath = TestProjectPaths.FromRepositoryRoot(
        "src", "Views", "Functional", "ContractDetailView.xaml.cs");
    private static readonly string ContractDialogPath = TestProjectPaths.FromRepositoryRoot(
        "src", "Views", "Functional", "ContractCommerEditDialog.cs");
    private static readonly string ContractViewPath = TestProjectPaths.FromRepositoryRoot(
        "src", "Views", "Functional", "ContractCommerEditView.xaml");

    [Fact]
    public void EmployeeBox_PinsResponsibleEmployeesAndUsesAccentBackground()
    {
        var code = File.ReadAllText(EmployeeBoxPath);

        Assert.Contains("ResponsibleEmployeeIdsProperty", code);
        Assert.Contains("responsibleOrder.TryGetValue(employeeId, out var order)", code);
        Assert.Contains(".OrderBy(static item => item.ResponsibleOrder ?? int.MaxValue)", code);
        Assert.Contains("ShellAccentPanelBackgroundBrush", code);
    }

    [Fact]
    public void ContractDetail_PassesOrderedResponsibleEmployeeIdsToEmployeeBox()
    {
        var code = File.ReadAllText(ContractDetailPath);

        Assert.Contains("EmployeesBox.ResponsibleEmployeeIds = contractState.ContractResponsibles", code);
        Assert.Contains(".Select(static responsible => responsible.EmployeeId)", code);
    }

    [Fact]
    public void ContractDialog_UsesFullNamesAndClearsResponsiblesWhenContragentChanges()
    {
        var code = File.ReadAllText(ContractDialogPath);
        var xaml = File.ReadAllText(ContractViewPath);

        Assert.Contains("Text=\"Ответственные от контрагента\"", xaml);
        Assert.Contains("TryGetString(employee, \"full_name\")", code);
        Assert.Contains("_workflowStore.ClearContractResponsibles();", code);
        Assert.Contains("_ = LoadContractResponsibleOptionsAsync(selectedContragentId);", code);
        Assert.True(
            code.IndexOf("_workflowStore.ClearContractResponsibles();", StringComparison.Ordinal)
            < code.IndexOf("_ = LoadContractResponsibleOptionsAsync(selectedContragentId);", StringComparison.Ordinal));
    }
}
