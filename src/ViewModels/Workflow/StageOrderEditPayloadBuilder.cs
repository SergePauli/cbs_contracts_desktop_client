using System.Globalization;
using CbsContractsDesktopClient.Shared.Data;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public static class StageOrderEditPayloadBuilder
    {
        public static IReadOnlyDictionary<string, object?> Build(StageOrderEditViewModel vm)
        {
            var state = vm.State;
            var stageId = vm.SelectedStage?.Id;
            var toolId = vm.SelectedTool?.Id ?? throw new InvalidOperationException("Выберите товар.");
            var severity = vm.SelectedSeverity is null ? (int?)null : checked((int)vm.SelectedSeverity.Value);
            var values = new Dictionary<string, object?>();
            if (state.IsCreateMode && state.OrderId is not null) values["order_id"] = state.OrderId;
            else values["id"] = state.Id ?? throw new InvalidOperationException("StageOrder.id отсутствует.");
            values["list_key"] = state.ListKey;
            Add(values, "stage_id", stageId, state.StageId, state.IsCreateMode);
            Add(values, "isecurity_tool_id", toolId, state.ToolId, state.IsCreateMode);
            Add(values, "severity", severity, state.Severity, state.IsCreateMode);
            Add(values, "price_cost", Decimal(vm.PriceCost), state.PriceCost, state.IsCreateMode);
            Add(values, "amount", Decimal(vm.Amount), state.Amount, state.IsCreateMode);
            Add(values, "cost", Decimal(vm.Cost), state.Cost, state.IsCreateMode);
            Add(values, "priority", Integer(vm.Priority), state.Priority, state.IsCreateMode);
            Add(values, "description", Empty(vm.Description), Empty(state.Description), state.IsCreateMode);
            return values;
        }
        private static void Add(Dictionary<string, object?> p, string k, object? v, object? o, bool create) { if ((create && v is not null) || (!create && !Equals(v, o))) p[k] = v; }
        private static decimal? Decimal(string v) => string.IsNullOrWhiteSpace(v) ? null : decimal.Parse(v.Replace(',', '.'), CultureInfo.InvariantCulture);
        private static int? Integer(string v) => string.IsNullOrWhiteSpace(v) ? null : int.Parse(v, CultureInfo.InvariantCulture);
        private static string? Empty(string v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
