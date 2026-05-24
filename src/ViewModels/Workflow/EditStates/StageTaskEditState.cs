namespace CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

public sealed record StageTaskEditState(
    long? Id,
    string? ListKey,
    long? TaskKindId,
    string? Name);
