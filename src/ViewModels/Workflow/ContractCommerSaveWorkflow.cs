using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services.Mutations;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow;

public sealed record ContractCommerSaveResult(long ContractId, TableDataRow? ContractMutationRow);

public sealed class ContractCommerSaveWorkflow
{
    private const string ContractModel = "Contract";
    private const string StageModel = "Stage";
    private const string RevisionModel = "Revision";
    private readonly IModelMutationService _modelMutationService;

    public ContractCommerSaveWorkflow(IModelMutationService modelMutationService)
    {
        _modelMutationService = modelMutationService;
    }

    public async Task<ContractCommerSaveResult> SaveAsync(
        ContractCommerEditSavePlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.HasChanges)
        {
            throw new InvalidOperationException("Contract commercial save plan must contain changes.");
        }

        TableDataRow? contractMutationRow = null;
        long contractId;
        if (plan.IsCreateMode)
        {
            contractMutationRow = await _modelMutationService.CreateAsync(
                ContractModel,
                plan.ContractPayload,
                cancellationToken);
            contractId = TryGetLong(contractMutationRow.GetValue("id"))
                ?? throw new InvalidOperationException("Created contract response must contain id.");
        }
        else
        {
            contractId = plan.ContractId
                ?? throw new InvalidOperationException("Contract update save plan must contain contract id.");
            if (plan.HasContractChanges)
            {
                contractMutationRow = await _modelMutationService.UpdateAsync(
                    ContractModel,
                    plan.ContractPayload,
                    cancellationToken);
            }
        }

        foreach (var payload in plan.StageUpdatePayloads)
        {
            await _modelMutationService.UpdateAsync(StageModel, payload, cancellationToken);
        }

        foreach (var payload in plan.RevisionUpdatePayloads)
        {
            await _modelMutationService.UpdateAsync(RevisionModel, payload, cancellationToken);
        }

        return new ContractCommerSaveResult(contractId, contractMutationRow);
    }
}
