namespace CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

public sealed record StageSnapshotEditState(
    long? Id,
    long? StatusId,
    string? StatusName);
