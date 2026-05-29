// Runs FNS-driven Contragent scenarios without loading that behavior into generic host views.
using System.Threading;
using System.Threading.Tasks;

namespace CbsContractsDesktopClient.Services.References
{
    public interface IContragentFnsWorkflow
    {
        Task<ContragentFnsWorkflowResult?> CompareSelectedAsync(
            ContragentFnsWorkflowRequest request,
            CancellationToken cancellationToken = default);

        Task<ContragentFnsWorkflowResult?> ImportAsync(
            ContragentFnsWorkflowRequest request,
            CancellationToken cancellationToken = default);

        Task<ContragentFnsWorkflowResult?> ChangeLegalEntityFromFnsAsync(
            ContragentFnsWorkflowRequest request,
            CancellationToken cancellationToken = default);

        Task<ContragentFnsWorkflowResult?> ChangeLegalEntityManuallyAsync(
            ContragentFnsWorkflowRequest request,
            CancellationToken cancellationToken = default);
    }
}
