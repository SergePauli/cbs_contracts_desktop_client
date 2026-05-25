namespace CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

public sealed record StagePerformerEditState(
    long? Id,
    string? ListKey,
    long? EmployeeId,
    string Name,
    int? Priority);
