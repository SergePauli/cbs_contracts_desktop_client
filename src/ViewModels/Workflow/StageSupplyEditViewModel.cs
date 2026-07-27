using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class StageSupplyEditViewModel : ObservableObject
    {
        private readonly IReadOnlyList<IsecurityToolCatalogItem> _allTools;
        private bool _isInitializing;

        public StageSupplyEditViewModel(
            StageSupplyEditState state,
            IReadOnlyList<IsecurityToolCatalogItem> tools)
        {
            _isInitializing = true;
            State = state;
            _allTools = tools;
            SelectedTool = tools.SingleOrDefault(option => option.Id == state.ToolId);
            ToolInput = SelectedTool?.Name ?? string.Empty;
            ToolSuggestions = tools.Select(static option => option.Name).ToList();
            SeverityOptions = Enum.GetValues<StageOrderSeverity>()
                .Select(value => new ReferenceEnumOption((long)value, StageOrderSeverityText.GetLabel(value)))
                .ToList();
            SelectedSeverity = SeverityOptions.SingleOrDefault(option => option.Value == state.Severity);
            PriceCost = state.PriceCost;
            Amount = state.Amount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            Cost = state.Cost;
            _isInitializing = false;
        }

        public StageSupplyEditState State { get; }
        public IReadOnlyList<ReferenceEnumOption> SeverityOptions { get; }

        [ObservableProperty] public partial string ToolInput { get; set; } = string.Empty;
        [ObservableProperty] public partial IReadOnlyList<string> ToolSuggestions { get; set; } = [];
        [ObservableProperty] public partial IsecurityToolCatalogItem? SelectedTool { get; set; }
        [ObservableProperty] public partial ReferenceEnumOption? SelectedSeverity { get; set; }
        [ObservableProperty] public partial string Amount { get; set; } = string.Empty;
        [ObservableProperty] public partial decimal? PriceCost { get; set; }
        [ObservableProperty] public partial decimal? Cost { get; set; }

        public Task UpdateToolSuggestionsAsync(string input)
        {
            ToolInput = input;
            SelectedTool = null;
            ToolSuggestions = _allTools
                .Where(option => option.Name.Contains(input.Trim(), StringComparison.CurrentCultureIgnoreCase))
                .Select(static option => option.Name)
                .Take(25)
                .ToList();
            return Task.CompletedTask;
        }

        public bool TrySelectTool(string? name)
        {
            var option = _allTools.FirstOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
            if (option is null)
                return false;
            SelectedTool = option;
            ToolInput = option.Name;
            if (State.IsCreateMode)
                PriceCost = option.DefaultCost;
            return true;
        }

        public void CommitTool(string? input)
        {
            if (!TrySelectTool(input?.Trim()))
            {
                SelectedTool = null;
                ToolInput = string.Empty;
            }
        }

        partial void OnAmountChanged(string value) => RecalculateCost();
        partial void OnPriceCostChanged(decimal? value) => RecalculateCost();

        private void RecalculateCost()
        {
            if (_isInitializing)
                return;
            Cost = PriceCost is not null && TryParseAmount(Amount, out var amount)
                ? PriceCost.Value * amount
                : null;
        }

        private static bool TryParseAmount(string value, out decimal amount) =>
            decimal.TryParse(
                value.Trim().Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out amount);
    }
}
