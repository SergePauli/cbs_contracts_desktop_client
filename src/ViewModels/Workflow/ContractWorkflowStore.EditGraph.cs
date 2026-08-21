using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class ContractWorkflowStore
    {
        private const string ContractRevisionDescription = "Договор";
        private const string AdditionalRevisionDescription = "Доп.соглашение";
        [ObservableProperty]
        public partial IReadOnlyList<StageEditState> ContractStageEditStates { get; set; } = [];

        [ObservableProperty]
        public partial IReadOnlyList<RevisionEditState> ContractRevisionEditStates { get; set; } = [];

        public string? ContractEditGraphRepairMessage { get; private set; }

        public void BeginContractEdit(TableDataRow contract, TableDataRow? contragent = null)
        {
            ArgumentNullException.ThrowIfNull(contract);

            _selectionKind = ContractRowDetailSelectionKind.Contract;
            Contract = contract;
            SelectedContractEditState = ContractEditState.FromRow(contract);
            Contragent = contragent;
            SelectedStage = ResolveSelectedStage(ContractRowDetailSelectionKind.Contract, contract, contract);
            SelectedRevision = null;
            FocusedRevisionPriority = null;
            ResetEditGraph();
        }

        public void BeginStageEdit(TableDataRow contract, TableDataRow selectedStage, TableDataRow? contragent = null)
        {
            ArgumentNullException.ThrowIfNull(contract);
            ArgumentNullException.ThrowIfNull(selectedStage);

            _selectionKind = ContractRowDetailSelectionKind.Stage;
            Contract = contract;
            SelectedContractEditState = ContractEditState.FromRow(contract);
            Contragent = contragent;
            SelectedRevision = null;
            FocusedRevisionPriority = null;
            SelectedStage = selectedStage;
            ResetEditGraph();
            SelectStageEditState(selectedStage);
        }

        public void ResetEditGraph()
        {
            var wasRepaired = false;
            ContractStageEditStates = EnsureInitialStage(ReadStageEditStates(Contract), ref wasRepaired);
            ContractRevisionEditStates = EnsureInitialRevision(ReadRevisionEditStates(Contract), ref wasRepaired);
            ContractEditGraphRepairMessage = wasRepaired
                ? "Структура контракта содержала ошибки и была исправлена - сохраните изменения."
                : null;
            SelectedStageEditState = ResolveSelectedStageEditState();
        }

        public void SetContractStageEditStates(IEnumerable<StageEditState> stages)
        {
            ArgumentNullException.ThrowIfNull(stages);
            ContractStageEditStates = stages.OrderBy(static stage => stage.Priority ?? 0).ToList();
            EnsureSingleActiveStage();
            SelectedStageEditState = ResolveSelectedStageEditState();
        }

        public void SetContractRevisionEditStates(IEnumerable<RevisionEditState> revisions)
        {
            ArgumentNullException.ThrowIfNull(revisions);
            ContractRevisionEditStates = revisions.OrderBy(static revision => revision.Priority).ToList();
        }

        public void AddStageAfter(StageEditState stage)
        {
            ArgumentNullException.ThrowIfNull(stage);

            var stages = ContractStageEditStates.ToList();
            var visibleStages = stages.Where(static item => !item.IsDestroyed).ToList();
            var newPriority = (stage.Priority ?? 0) + 1;
            if (visibleStages.Count == 1 && visibleStages[0].Priority == 0)
            {
                visibleStages[0].Priority = 1;
                newPriority = 2;
            }

            var occupiedPriorities = visibleStages
                .Select(static existing => existing.Priority)
                .ToHashSet();
            while (occupiedPriorities.Contains(newPriority))
            {
                newPriority++;
            }

            stages.Add(StageEditState.CreateNew(newPriority));
            SetContractStageEditStates(stages);
        }

        public void DeleteStage(StageEditState stage)
        {
            ArgumentNullException.ThrowIfNull(stage);

            var stages = ContractStageEditStates.ToList();
            var visibleStages = stages.Where(static item => !item.IsDestroyed).ToList();
            if (visibleStages.Count <= 1)
            {
                throw new InvalidOperationException("Нельзя удалить последний этап.");
            }

            if (stage.Id > 0)
            {
                stage.IsDestroyed = true;
            }
            else
            {
                stages.Remove(stage);
            }

            visibleStages = stages.Where(static item => !item.IsDestroyed).ToList();
            if (visibleStages.Count == 1)
            {
                visibleStages[0].Priority = 0;
                visibleStages[0].Used = true;
            }

            SetContractStageEditStates(stages);
        }

        public void SetActiveStage(StageEditState selectedStage)
        {
            ArgumentNullException.ThrowIfNull(selectedStage);

            foreach (var stage in ContractStageEditStates.Where(static stage => !stage.IsDestroyed))
            {
                stage.Used = ReferenceEquals(stage, selectedStage);
            }

            SelectedStageEditState = selectedStage;
        }

        public void SelectStageEditState(TableDataRow selectedStage)
        {
            ArgumentNullException.ThrowIfNull(selectedStage);

            var selectedId = TryGetLong(selectedStage.GetValue("id"));
            var selectedListKey = selectedStage.GetValue("list_key")?.ToString();
            SelectedStageEditState =
                ContractStageEditStates.FirstOrDefault(stage => !stage.IsDestroyed && SameIdentity(stage.Id, stage.ListKey, selectedId, selectedListKey))
                ?? throw new InvalidOperationException("Contract edit graph does not contain selected stage.");
        }

        public IReadOnlyList<StageEditState> GetVisibleStageEditStates()
        {
            return ContractStageEditStates
                .Where(static stage => !stage.IsDestroyed)
                .OrderBy(static stage => stage.Priority ?? 0)
                .ToList();
        }

        public RevisionEditState GetContractDocumentRevisionEditState()
        {
            return ContractRevisionEditStates.FirstOrDefault(static revision => revision.Priority == 0)
                ?? throw new InvalidOperationException("ContractWorkflowStore.GetContractDocumentRevisionEditState: contract edit graph must contain revision priority 0.");
        }

        public bool TrySelectAdjacentStageEditState(int direction)
        {
            if (direction == 0)
            {
                return false;
            }

            var stages = GetVisibleStageEditStates();
            if (stages.Count == 0)
            {
                return false;
            }

            var selectedIndex = SelectedStageEditState is null
                ? 0
                : FindStageEditStateIndex(stages, SelectedStageEditState);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            var nextIndex = selectedIndex + Math.Sign(direction);
            if (nextIndex < 0 || nextIndex >= stages.Count)
            {
                return false;
            }

            SelectedStageEditState = stages[nextIndex];
            return true;
        }

        public bool ShouldCloseContractAfterSelectedStageClosed()
        {
            var selectedStage = SelectedStageEditState
                ?? throw new InvalidOperationException("ContractWorkflowStore.ShouldCloseContractAfterSelectedStageClosed: SelectedStageEditState is not set.");
            return ShouldCloseContractAfterStageClosed(selectedStage);
        }

        public bool ShouldCloseContractAfterStageClosed(StageEditState selectedStage)
        {
            ArgumentNullException.ThrowIfNull(selectedStage);
            var selectedContract = SelectedContractEditState
                ?? throw new InvalidOperationException("ContractWorkflowStore.ShouldCloseContractAfterStageClosed: SelectedContractEditState is not set.");

            if (selectedStage.Status.Id != WorkflowStatusIds.Closed
                || selectedStage.ClosedAt is null
                || selectedContract.Original.Status.Id == WorkflowStatusIds.Closed)
            {
                return false;
            }

            var stages = GetVisibleStageEditStates();
            if (!stages.Any(stage => SameIdentity(stage.Id, stage.ListKey, selectedStage.Id, selectedStage.ListKey)))
            {
                throw new InvalidOperationException("ContractWorkflowStore.ShouldCloseContractAfterStageClosed: selected stage is absent from ContractStageEditStates.");
            }

            return stages
                .Where(stage => !SameIdentity(stage.Id, stage.ListKey, selectedStage.Id, selectedStage.ListKey))
                .All(stage => stage.Status.Id == WorkflowStatusIds.Closed);
        }

        public string BuildContractCloseDecisionTrace(bool result)
        {
            var selectedStage = SelectedStageEditState
                ?? throw new InvalidOperationException("ContractWorkflowStore.BuildContractCloseDecisionTrace: SelectedStageEditState is not set.");
            var selectedContract = SelectedContractEditState
                ?? throw new InvalidOperationException("ContractWorkflowStore.BuildContractCloseDecisionTrace: SelectedContractEditState is not set.");
            var stages = GetVisibleStageEditStates();
            return
                $"result={result} "
                + $"contract={selectedContract.Id} contractStatus={selectedContract.Status.Id} contractOriginalStatus={selectedContract.Original.Status.Id} "
                + $"selectedStage={selectedStage.Id} selectedStatus={selectedStage.Status.Id} selectedClosedAt={FormatTraceDate(selectedStage.ClosedAt)} "
                + $"stages=[{string.Join("; ", stages.Select(stage => $"id={stage.Id},priority={stage.Priority},status={stage.Status.Id},closedAt={FormatTraceDate(stage.ClosedAt)},used={stage.Used}"))}]";
        }

        private static int FindStageEditStateIndex(
            IReadOnlyList<StageEditState> stages,
            StageEditState selectedStage)
        {
            for (var index = 0; index < stages.Count; index++)
            {
                var stage = stages[index];
                if (SameIdentity(stage.Id, stage.ListKey, selectedStage.Id, selectedStage.ListKey))
                {
                    return index;
                }
            }

            return -1;
        }

        private static string FormatTraceDate(DateTimeOffset? date)
        {
            return date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "<null>";
        }

        public void AddRevisionAfter(RevisionEditState revision)
        {
            ArgumentNullException.ThrowIfNull(revision);

            var revisions = ContractRevisionEditStates.ToList();
            var newPriority = revision.Priority + 1;
            if (revisions.Any(existing => existing.Priority == newPriority))
            {
                throw new InvalidOperationException($"Ревизия с номером {newPriority} уже существует.");
            }

            revisions.Add(RevisionEditState.CreateNew(newPriority, AdditionalRevisionDescription));
            SetContractRevisionEditStates(revisions);
        }

        public void DeleteRevision(RevisionEditState revision)
        {
            ArgumentNullException.ThrowIfNull(revision);

            var revisions = ContractRevisionEditStates.ToList();
            if (revision.Priority == 1 && revisions.Any(item => !item.IsDestroyed && item.Priority > revision.Priority))
            {
                throw new InvalidOperationException($"Нельзя удалить ревизию № {revision.Priority}, пока существуют ревизии с большим номером.");
            }

            if (revision.Id is long id && id > 0)
            {
                revision.IsDestroyed = true;
            }
            else
            {
                revisions.Remove(revision);
            }

            SetContractRevisionEditStates(revisions);
        }

        public IReadOnlyDictionary<string, object?> BuildContractCommerPayload(ContractCommerEditPayloadInput input)
        {
            if (Contract is null)
            {
                throw new InvalidOperationException("Contract edit graph must contain contract source row.");
            }

            return ContractCommerEditPayloadBuilder.Build(
                Contract,
                input,
                ContractStageEditStates,
                ContractRevisionEditStates);
        }

        private StageEditState? ResolveSelectedStageEditState()
        {
            if (SelectedStage is not null)
            {
                var selectedId = TryGetLong(SelectedStage.GetValue("id"));
                var selectedListKey = SelectedStage.GetValue("list_key")?.ToString();
                return ContractStageEditStates.FirstOrDefault(stage => !stage.IsDestroyed && SameIdentity(stage.Id, stage.ListKey, selectedId, selectedListKey))
                    ?? ContractStageEditStates.FirstOrDefault(static stage => !stage.IsDestroyed);
            }

            return ContractStageEditStates.FirstOrDefault(static stage => !stage.IsDestroyed);
        }

        private void EnsureSingleActiveStage()
        {
            var visibleStages = ContractStageEditStates.Where(static stage => !stage.IsDestroyed).ToList();
            if (visibleStages.Count == 0)
            {
                return;
            }

            var activeStage = visibleStages.FirstOrDefault(static stage => stage.Used)
                ?? visibleStages.OrderBy(static stage => stage.Priority ?? 0).First();
            foreach (var stage in visibleStages)
            {
                stage.Used = ReferenceEquals(stage, activeStage);
            }
        }

        private static IReadOnlyList<StageEditState> ReadStageEditStates(TableDataRow? contract)
        {
            if (contract is null || contract.IsPlaceholder)
            {
                return [];
            }

            return EnumerateObjectArray(contract, "stages")
                .Where(static stage => stage.ValueKind == JsonValueKind.Object)
                .Select(static stage => StageEditState.FromRow(ToTableDataRow(stage)))
                .OrderBy(static stage => stage.Priority ?? 0)
                .ToList();
        }

        private IReadOnlyList<StageEditState> EnsureInitialStage(IReadOnlyList<StageEditState> stages, ref bool wasRepaired)
        {
            if (stages.Count == 0)
            {
                wasRepaired |= !IsNewContractEditGraph();
                return [StageEditState.CreateNew(0, used: true)];
            }

            var visibleStages = stages.Where(static stage => !stage.IsDestroyed).ToList();
            var isMultiStageShape = visibleStages.Any(static stage => (stage.Priority ?? 0) > 0);
            if (!isMultiStageShape || visibleStages.Any(static stage => stage.Priority == 1))
            {
                return stages;
            }

            wasRepaired |= !IsNewContractEditGraph();
            var firstStage = StageEditState.CreateNew(1, used: visibleStages.All(static stage => !stage.Used));
            return [firstStage, .. stages];
        }

        private static IReadOnlyList<RevisionEditState> ReadRevisionEditStates(TableDataRow? contract)
        {
            if (contract is null || contract.IsPlaceholder)
            {
                return [];
            }

            return EnumerateObjectArray(contract, "revisions")
                .Where(static revision => revision.ValueKind == JsonValueKind.Object)
                .Select(static revision => RevisionEditState.FromRow(ToTableDataRow(revision)))
                .OrderBy(static revision => revision.Priority)
                .ToList();
        }

        private IReadOnlyList<RevisionEditState> EnsureInitialRevision(IReadOnlyList<RevisionEditState> revisions, ref bool wasRepaired)
        {
            if (revisions.Any(static revision => revision.Priority == 0))
            {
                return revisions;
            }

            wasRepaired |= !IsNewContractEditGraph();
            return [RevisionEditState.CreateNew(0, ContractRevisionDescription), .. revisions];
        }

        private bool IsNewContractEditGraph()
        {
            return Contract is not null
                && !Contract.IsPlaceholder
                && TryGetLong(Contract.GetValue("id")) is null;
        }

        private static bool SameIdentity(long? leftId, string? leftListKey, long? rightId, string? rightListKey)
        {
            if (leftId > 0 && rightId > 0)
            {
                return leftId == rightId;
            }

            return !string.IsNullOrWhiteSpace(leftListKey)
                && !string.IsNullOrWhiteSpace(rightListKey)
                && string.Equals(leftListKey, rightListKey, StringComparison.Ordinal);
        }
    }
}
