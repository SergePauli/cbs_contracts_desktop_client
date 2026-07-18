using System.Text.Json;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Mutations;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow;

public sealed class ContractCommentWorkflow
{
    private const string ContractModel = "Contract";
    private const string StageModel = "Stage";
    private readonly IModelMutationService _modelMutationService;
    private readonly IUserService _userService;
    private readonly ContractWorkflowFactory _workflowFactory;
    private readonly ContractWorkflowStore _workflowStore;

    public ContractCommentWorkflow(
        IModelMutationService modelMutationService,
        IUserService userService,
        ContractWorkflowFactory workflowFactory,
        ContractWorkflowStore workflowStore)
    {
        _modelMutationService = modelMutationService;
        _userService = userService;
        _workflowFactory = workflowFactory;
        _workflowStore = workflowStore;
    }

    public async Task<ContractCommentSaveResult> SaveContractCommentAsync(
        long contractId,
        string? listKey,
        string comment,
        CancellationToken cancellationToken = default)
    {
        var profileId = RequireProfileId();
        var payload = ContractCommerEditPayloadBuilder.BuildCommentUpdate(
            contractId,
            listKey,
            RequireComment(comment),
            profileId);
        await _modelMutationService.UpdateAsync(ContractModel, payload, cancellationToken);

        var contract = await _workflowFactory.ReloadContractEditRowAsync(contractId, cancellationToken);
        var comments = ReadContractComments(contract);
        _workflowStore.ApplyCommentReadModel(contract);
        return new ContractCommentSaveResult(contract, comments);
    }

    public async Task<ContractCommentSaveResult> SaveStageCommentAsync(
        long contractId,
        long stageId,
        string? listKey,
        string comment,
        CancellationToken cancellationToken = default)
    {
        var profileId = RequireProfileId();
        var payload = StageEditPayloadBuilderHelpers.BuildCommentUpdate(
            stageId,
            listKey,
            RequireComment(comment),
            profileId);
        await _modelMutationService.UpdateAsync(StageModel, payload, cancellationToken);

        var contract = await _workflowFactory.ReloadContractEditRowAsync(contractId, cancellationToken);
        var stages = TryGetArray(contract, "stages")
            ?? throw new InvalidOperationException("Contract edit row must contain stages after stage comment save.");
        var stage = stages
            .EnumerateArray()
            .FirstOrDefault(item => item.ValueKind == JsonValueKind.Object && TryGetLong(item, "id") == stageId);
        if (stage.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Contract edit row does not contain stage {stageId} after comment save.");
        }

        var comments = ReadComments(stage);
        _workflowStore.ApplyCommentReadModel(contract, stageId);
        return new ContractCommentSaveResult(contract, comments);
    }

    public IReadOnlyList<TableDataRow> ReadContractComments()
    {
        return ReadContractComments(RequireContract());
    }

    public IReadOnlyList<TableDataRow> ReadStageComments(long stageId)
    {
        var contract = RequireContract();
        var stages = TryGetArray(contract, "stages")
            ?? throw new InvalidOperationException("Contract edit row must contain stages for stage comments.");
        var stage = stages
            .EnumerateArray()
            .FirstOrDefault(item => item.ValueKind == JsonValueKind.Object && TryGetLong(item, "id") == stageId);
        if (stage.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Contract edit row does not contain stage {stageId} for comments.");
        }

        return ReadComments(stage);
    }

    private int RequireProfileId()
    {
        return _userService.CurrentUser?.ProfileId
            ?? throw new InvalidOperationException("Не удалось определить profile_id пользователя для комментария.");
    }

    private TableDataRow RequireContract()
    {
        return _workflowStore.Contract
            ?? throw new InvalidOperationException("ContractWorkflowStore.Contract is not set for comments.");
    }

    private static string RequireComment(string comment)
    {
        var normalized = comment.Trim();
        return normalized.Length > 0
            ? normalized
            : throw new InvalidOperationException("Комментарий не может быть пустым.");
    }

    private static IReadOnlyList<TableDataRow> ReadComments(JsonElement source)
    {
        var comments = TryGetArray(source, "comments")
            ?? throw new InvalidOperationException("Comment read model must contain comments.");
        return comments
            .EnumerateArray()
            .Select(static comment => comment.ValueKind == JsonValueKind.Object
                ? ToTableDataRow(comment)
                : throw new InvalidOperationException("Comment read model item must be an object."))
            .ToList();
    }

    private static IReadOnlyList<TableDataRow> ReadContractComments(TableDataRow source)
    {
        var comments = TryGetArray(source, "comments")
            ?? throw new InvalidOperationException("Contract comment read model must contain comments.");
        return comments
            .EnumerateArray()
            .Select(static comment => comment.ValueKind == JsonValueKind.Object
                ? ToTableDataRow(comment)
                : throw new InvalidOperationException("Comment read model item must be an object."))
            .ToList();
    }
}

public sealed record ContractCommentSaveResult(
    TableDataRow Contract,
    IReadOnlyList<TableDataRow> Comments);
