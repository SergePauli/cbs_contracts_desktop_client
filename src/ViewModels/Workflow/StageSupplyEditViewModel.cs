using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class StageSupplyEditViewModel : ObservableObject
    {
        private readonly Func<string, CancellationToken, Task<IReadOnlyList<IsecurityToolCatalogItem>>> _searchToolsAsync;
        private CancellationTokenSource? _toolSearchCts;
        private bool _isInitializing;

        public StageSupplyEditViewModel(
            StageSupplyEditState state,
            Func<string, CancellationToken, Task<IReadOnlyList<IsecurityToolCatalogItem>>> searchToolsAsync)
        {
            _isInitializing = true;
            State = state;
            _searchToolsAsync = searchToolsAsync;
            SelectedTool = state.ToolId is long toolId
                ? new IsecurityToolCatalogItem(toolId, state.ToolName, state.PriceCost)
                : null;
            ToolInput = state.ToolName;
            ToolOptions = SelectedTool is null ? [] : [SelectedTool];
            ToolSuggestions = [];
            SeverityOptions = Enum.GetValues<StageOrderSeverity>()
                .Select(value => new ReferenceEnumOption((long)value, StageOrderSeverityText.GetLabel(value)))
                .ToList();
            SelectedSeverity = SeverityOptions.SingleOrDefault(option => option.Value == state.Severity);
            PriceCost = state.PriceCost;
            Amount = state.Amount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            Cost = state.Cost;
            _isInitializing = false;
        }

        public StageSupplyEditViewModel(
            StageSupplyEditState state,
            IReadOnlyList<IsecurityToolCatalogItem> tools)
            : this(state, (_, _) => Task.FromResult(tools))
        {
            ToolOptions = tools;
            SelectedTool = tools.SingleOrDefault(option => option.Id == state.ToolId);
            ToolInput = SelectedTool?.Name ?? string.Empty;
            PriceCost = state.PriceCost;
            Cost = state.Cost;
        }

        public StageSupplyEditState State { get; }
        public IReadOnlyList<ReferenceEnumOption> SeverityOptions { get; }

        [ObservableProperty] public partial string ToolInput { get; set; } = string.Empty;
        [ObservableProperty] public partial IReadOnlyList<string> ToolSuggestions { get; set; } = [];
        [ObservableProperty] public partial IReadOnlyList<IsecurityToolCatalogItem> ToolOptions { get; set; } = [];
        [ObservableProperty] public partial IsecurityToolCatalogItem? SelectedTool { get; set; }
        [ObservableProperty] public partial ReferenceEnumOption? SelectedSeverity { get; set; }
        [ObservableProperty] public partial string Amount { get; set; } = string.Empty;
        [ObservableProperty] public partial decimal? PriceCost { get; set; }
        [ObservableProperty] public partial decimal? Cost { get; set; }

        public async Task UpdateToolSuggestionsAsync(string input)
        {
            ToolInput = input;
            SelectedTool = null;
            _toolSearchCts?.Cancel();
            var normalized = input.Trim();
            if (normalized.Length == 0)
            {
                ToolSuggestions = [];
                return;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _toolSearchCts = cancellationTokenSource;
            try
            {
                await Task.Delay(300, cancellationTokenSource.Token);
                ToolOptions = await _searchToolsAsync(normalized, cancellationTokenSource.Token);
                ToolSuggestions = ToolOptions.Select(static option => option.Name).ToList();
            }
            catch (OperationCanceledException)
            {
            }
        }

        public bool TrySelectTool(string? name)
        {
            var option = ToolOptions.FirstOrDefault(item =>
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
