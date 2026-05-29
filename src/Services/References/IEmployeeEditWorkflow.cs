// Opens the Employee edit workflow for host views that need shared employee editing behavior.
namespace CbsContractsDesktopClient.Services.References
{
    public interface IEmployeeEditWorkflow
    {
        Task<EmployeeEditWorkflowResult?> ShowAsync(
            EmployeeEditWorkflowRequest request,
            CancellationToken cancellationToken = default);
    }
}
