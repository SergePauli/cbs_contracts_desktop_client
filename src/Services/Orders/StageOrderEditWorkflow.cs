using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using CbsContractsDesktopClient.Views.Orders;

namespace CbsContractsDesktopClient.Services.Orders
{
    public sealed class StageOrderEditWorkflow
    {
        private readonly IDataQueryService _queries; private readonly IsecurityToolCatalogService _catalog; private readonly IModelMutationService _mutations;
        public StageOrderEditWorkflow(IDataQueryService queries, IsecurityToolCatalogService catalog, IModelMutationService mutations) { _queries = queries; _catalog = catalog; _mutations = mutations; }
        public async Task<bool> ShowAsync(StageOrderEditWorkflowRequest request)
        {
            var mode = request.IsCreateMode ? "create" : "edit";
            request.Trace($"ORDER POSITION WORKFLOW ENTER mode={mode} access={request.AccessMode} order={request.OrderId?.ToString() ?? "<null>"} stage={request.StageId?.ToString() ?? "<null>"}");
            if (request.IsCreateMode && request.OrderId is null && request.StageId is null)
                throw new InvalidOperationException("Новая StageOrder должна быть открыта в контексте заказа или этапа.");
            var state = request.SourceRow is { } row
                ? StageOrderEditState.FromRow(request, row)
                : StageOrderEditState.Create(request.OrderId, request.StageId, request.IsStageFixed);
            request.Trace(
                $"ORDER POSITION WORKFLOW STATE mode={mode} id={state.Id?.ToString() ?? "<null>"} list_key={state.ListKey} stage={state.StageId?.ToString() ?? "<null>"}");
            IReadOnlyList<StageOrderStageOption> stages;
            if (request.AccessMode == StageOrderEditAccessMode.ControlFieldsOnly)
            {
                stages = state.StageId is long currentStageId
                    ? [new StageOrderStageOption(currentStageId, state.StageName)]
                    : [];
                request.Trace($"ORDER POSITION WORKFLOW LOAD SKIP mode={mode} access={request.AccessMode}");
            }
            else
            {
                request.Trace($"ORDER POSITION WORKFLOW LOAD START mode={mode} source=stages-open");
                stages = request.StageId is long stageId
                    ? [new StageOrderStageOption(stageId, request.StageLabel)]
                    : await LoadStagesAsync();
                request.Trace($"ORDER POSITION WORKFLOW LOAD COMPLETE mode={mode} stages={stages.Count} tools=on-demand");
            }

            IReadOnlyList<IsecurityToolCatalogItem> tools = state.ToolId is long toolId
                ? [new IsecurityToolCatalogItem(toolId, state.ToolName, state.PriceCost)]
                : [];
            var vm = new StageOrderEditViewModel(state, stages, tools, _catalog.SearchToolOptionsAsync);
            request.Trace($"ORDER POSITION WORKFLOW VIEWMODEL mode={mode}");
            var dialog = new StageOrderEditDialog(vm) { XamlRoot = request.XamlRoot };
            request.Trace($"ORDER POSITION WORKFLOW DIALOG BUILT mode={mode}");
            dialog.SaveRequestedAsync += async args => { try { var p = StageOrderEditPayloadBuilder.Build(vm); if (!state.IsCreateMode && p.Count == 2) { dialog.ShowErrorInfo("Нет изменений."); args.Cancel = true; return; } if (state.IsCreateMode) await _mutations.CreateAsync("StageOrder", p); else await _mutations.UpdateAsync("StageOrder", p); } catch (Exception ex) { dialog.ShowErrorInfo(ex.Message); args.Cancel = true; } };
            request.Trace($"ORDER POSITION WORKFLOW DIALOG SHOW mode={mode}");
            await dialog.ShowAsync();
            request.Trace($"ORDER POSITION WORKFLOW DIALOG CLOSED mode={mode} saved={dialog.WasSaved}");
            return dialog.WasSaved;
        }
        private async Task<IReadOnlyList<StageOrderStageOption>> LoadStagesAsync()
        {
            var rows = await _queries.GetDataAsync<TableDataRow>(new DataQueryRequest
            {
                Model = "Stage",
                Preset = "order",
                Filters = new Dictionary<string, object?>
                {
                    ["status_id__not_eq"] = WorkflowStatusIds.Closed
                },
                Sorts = ["id desc"],
                Limit = 1000
            });
            return rows.Select(row => new StageOrderStageOption(
                Shared.Data.JsonDataReader.TryGetLong(row.GetValue("id")) ?? throw new InvalidOperationException("Stage.order не содержит id."),
                row.GetValue("name")?.ToString() ?? throw new InvalidOperationException("Stage.order не содержит name."))).ToList();
        }
    }
}
