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
            if (request.OrderId is null && request.StageId is null)
                throw new InvalidOperationException("StageOrder должен быть открыт в контексте заказа или этапа.");
            var toolsTask = _catalog.LoadToolOptionsAsync();
            var stagesTask = request.StageId is long stageId
                ? Task.FromResult<IReadOnlyList<StageOrderStageOption>>([new StageOrderStageOption(stageId, request.StageLabel)])
                : LoadStagesAsync();
            await Task.WhenAll(stagesTask, toolsTask);
            var state = request.SourceRow is { } row
                ? StageOrderEditState.FromRow(request, row)
                : StageOrderEditState.Create(request.OrderId, request.StageId, request.IsStageFixed);
            var vm = new StageOrderEditViewModel(state, stagesTask.Result, toolsTask.Result);
            var dialog = new StageOrderEditDialog(vm) { XamlRoot = request.XamlRoot };
            dialog.SaveRequestedAsync += async args => { try { var p = StageOrderEditPayloadBuilder.Build(vm); if (!state.IsCreateMode && p.Count == 2) { dialog.ShowErrorInfo("Нет изменений."); args.Cancel = true; return; } if (state.IsCreateMode) await _mutations.CreateAsync("StageOrder", p); else await _mutations.UpdateAsync("StageOrder", p); } catch (Exception ex) { dialog.ShowErrorInfo(ex.Message); args.Cancel = true; } };
            await dialog.ShowAsync(); return dialog.WasSaved;
        }
        private async Task<IReadOnlyList<StageOrderStageOption>> LoadStagesAsync()
        {
            var rows = await _queries.GetDataAsync<TableDataRow>(new DataQueryRequest { Model = "Stage", Preset = "order", Sorts = ["id desc"], Limit = 1000 });
            return rows.Select(row => new StageOrderStageOption(
                Shared.Data.JsonDataReader.TryGetLong(row.GetValue("id")) ?? throw new InvalidOperationException("Stage.order не содержит id."),
                row.GetValue("name")?.ToString() ?? throw new InvalidOperationException("Stage.order не содержит name."))).ToList();
        }
    }
}
