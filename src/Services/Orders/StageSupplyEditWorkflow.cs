using System.Text.Json;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

namespace CbsContractsDesktopClient.Services.Orders
{
    public sealed class StageSupplyEditWorkflow(
        IsecurityToolCatalogService catalog,
        IModelMutationService mutations)
    {
        public Task<StageSupplyEditViewModel> CreateViewModelAsync(
            long stageId,
            TableDataRow? sourceRow,
            CancellationToken cancellationToken = default)
        {
            var state = sourceRow is null
                ? StageSupplyEditState.Create(stageId)
                : StageSupplyEditState.FromRow(stageId, JsonSerializer.SerializeToElement(sourceRow.Values));
            if (!state.CanEdit)
                throw new InvalidOperationException($"Позиция уже включена в заказ {state.OrderNumber} и недоступна для изменения.");
            return Task.FromResult(new StageSupplyEditViewModel(state, catalog.SearchToolOptionsAsync));
        }

        public async Task<long> SaveAsync(
            StageSupplyEditViewModel viewModel,
            CancellationToken cancellationToken = default)
        {
            var payload = StageSupplyEditPayloadBuilder.Build(viewModel);
            if (!viewModel.State.IsCreateMode && payload.Count == 2)
                throw new InvalidOperationException("Нет изменений.");
            var saved = viewModel.State.IsCreateMode
                ? await mutations.CreateAsync("StageOrder", payload, cancellationToken)
                : await mutations.UpdateAsync("StageOrder", payload, cancellationToken);
            return Shared.Data.JsonDataReader.TryGetLong(saved.GetValue("id"))
                ?? throw new InvalidOperationException("Mutation StageOrder не вернула идентификатор сохраненной позиции.");
        }

        public async Task DeleteAsync(
            TableDataRow sourceRow,
            CancellationToken cancellationToken = default)
        {
            var orderNumber = sourceRow.GetValue("order.order_number")?.ToString();
            if (!string.IsNullOrWhiteSpace(orderNumber))
                throw new InvalidOperationException(
                    $"Позиция уже включена в заказ {orderNumber} и недоступна для удаления.");
            var id = sourceRow.GetValue("id") switch
            {
                long value => value,
                _ => throw new InvalidOperationException("StageOrder.id отсутствует.")
            };
            await mutations.DeleteAsync("StageOrder", id, cancellationToken);
        }
    }
}
