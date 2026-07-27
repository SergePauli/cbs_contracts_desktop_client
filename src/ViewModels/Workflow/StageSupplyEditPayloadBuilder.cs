using System.Globalization;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public static class StageSupplyEditPayloadBuilder
    {
        public static IReadOnlyDictionary<string, object?> Build(StageSupplyEditViewModel viewModel)
        {
            var state = viewModel.State;
            if (!state.CanEdit)
                throw new InvalidOperationException($"Позиция уже включена в заказ {state.OrderNumber} и недоступна для изменения.");
            var toolId = viewModel.SelectedTool?.Id ?? throw new InvalidOperationException("Выберите наименование.");
            var severity = checked((int)(viewModel.SelectedSeverity?.Value
                ?? throw new InvalidOperationException("Выберите важность.")));
            var amount = ParseDecimal(viewModel.Amount);
            var values = new Dictionary<string, object?>();
            if (state.IsCreateMode)
                values["stage_id"] = state.StageId;
            else
                values["id"] = state.Id ?? throw new InvalidOperationException("StageOrder.id отсутствует.");
            values["list_key"] = state.ListKey;
            Add(values, "isecurity_tool_id", toolId, state.ToolId, state.IsCreateMode);
            Add(values, "price_cost", viewModel.PriceCost, state.PriceCost, state.IsCreateMode);
            Add(values, "amount", amount, state.Amount, state.IsCreateMode);
            Add(values, "cost", viewModel.Cost, state.Cost, state.IsCreateMode);
            Add(values, "severity", severity, state.Severity, state.IsCreateMode);
            return values;
        }

        private static void Add(
            Dictionary<string, object?> payload,
            string key,
            object? value,
            object? original,
            bool create)
        {
            if ((create && value is not null) || (!create && !Equals(value, original)))
                payload[key] = value;
        }

        private static decimal? ParseDecimal(string value) => string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.Parse(value.Trim().Replace(',', '.'), CultureInfo.InvariantCulture);
    }
}
