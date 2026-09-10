using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using CbsContractsDesktopClient.Views.Orders;
using Microsoft.UI.Xaml;

namespace CbsContractsDesktopClient.Services.Orders
{
    public sealed class OrderEditWorkflow
    {
        private readonly IModelMutationService _mutations;
        private readonly IReferenceLookupCacheService _lookups;
        private readonly IContragentLookupService _contragents;
        private readonly IDataQueryService _queries;

        public OrderEditWorkflow(IModelMutationService mutations, IReferenceLookupCacheService lookups, IContragentLookupService contragents, IDataQueryService queries)
        {
            _mutations = mutations; _lookups = lookups; _contragents = contragents;
            _queries = queries;
        }

        public async Task<TableDataRow?> ShowAsync(bool create, TableDataRow? card, XamlRoot xamlRoot)
        {
            var state = create ? OrderEditState.CreateNew() : OrderEditState.FromCard(card!);
            var statuses = await _lookups.GetOptionsAsync("OrderStatus");
            var vm = new OrderEditViewModel(state, statuses, _contragents.LoadOptionsAsync);
            if (!create)
            {
                vm.CalculatedCost = await LoadCalculatedCostAsync(state.Id!.Value);
            }
            var dialog = new OrderEditDialog(vm) { XamlRoot = xamlRoot };
            TableDataRow? saved = null;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    var payload = create ? OrderEditPayloadBuilder.BuildForCreate(vm) : OrderEditPayloadBuilder.BuildForUpdate(vm);
                    if (!create && payload.Count == 1) { dialog.ShowErrorInfo("Нет изменений для сохранения."); args.Cancel = true; return; }
                    saved = create ? await _mutations.CreateAsync("Order", payload) : await _mutations.UpdateAsync("Order", payload);
                }
                catch (Exception ex) { dialog.ShowErrorInfo(ex.Message); args.Cancel = true; }
            };
            await dialog.ShowAsync();
            return dialog.WasSaved ? saved : null;
        }

        private async Task<decimal> LoadCalculatedCostAsync(long orderId)
        {
            const int pageSize = 1000;
            decimal total = 0m;
            for (var offset = 0; ; offset += pageSize)
            {
                var positions = await _queries.GetDataAsync<TableDataRow>(new DataQueryRequest
                {
                    Model = "StageOrder",
                    Preset = "order",
                    Filters = new Dictionary<string, object?> { ["order_id"] = orderId },
                    Sorts = ["id asc"],
                    Limit = pageSize,
                    Offset = offset
                });
                total += OrderCostCalculator.Calculate(positions);
                if (positions.Count < pageSize) return total;
            }
        }
    }
}
