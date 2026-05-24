namespace CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

public interface IEditState
{
    bool HasChanges { get; }

    void RestoreOriginal();
}
