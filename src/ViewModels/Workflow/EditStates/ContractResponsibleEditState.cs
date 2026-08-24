namespace CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

public sealed record ContractResponsibleEditState(
    long? Id,
    string? ListKey,
    long EmployeeId,
    string FullName);
