using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class StageOrderEditViewModel : ObservableObject
    {
        private readonly IReadOnlyList<StageOrderStageOption> _allStages;

        public StageOrderEditViewModel(
            StageOrderEditState state,
            IReadOnlyList<StageOrderStageOption> stages,
            IReadOnlyList<IsecurityToolCatalogItem> tools)
        {
            State = state;
            _allStages = stages;
            ToolOptions = tools;
            SelectedStage = stages.SingleOrDefault(option => option.Id == state.StageId);
            StageInput = SelectedStage?.Label ?? string.Empty;
            StageSuggestions = SelectedStage is null ? [] : [SelectedStage.Label];
            SelectedTool = tools.SingleOrDefault(option => option.Id == state.ToolId);
            SeverityOptions = Enum.GetValues<StageOrderSeverity>()
                .Take(2)
                .Select(severity => new ReferenceEnumOption((long)severity, StageOrderSeverityText.GetLabel(severity)))
                .ToList();
            SelectedSeverity = SeverityOptions.SingleOrDefault(option => option.Value == state.Severity);
            PriceCost = Text(state.PriceCost);
            Amount = Text(state.Amount);
            Cost = Text(state.Cost);
            Priority = state.Priority?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            Description = state.Description;
        }

        public StageOrderEditState State { get; }
        public IReadOnlyList<IsecurityToolCatalogItem> ToolOptions { get; }
        public IReadOnlyList<ReferenceEnumOption> SeverityOptions { get; }

        [ObservableProperty] public partial string StageInput { get; set; } = string.Empty;
        [ObservableProperty] public partial IReadOnlyList<string> StageSuggestions { get; set; } = [];
        [ObservableProperty] public partial StageOrderStageOption? SelectedStage { get; set; }
        [ObservableProperty] public partial IsecurityToolCatalogItem? SelectedTool { get; set; }
        [ObservableProperty] public partial ReferenceEnumOption? SelectedSeverity { get; set; }
        [ObservableProperty] public partial string PriceCost { get; set; } = string.Empty;
        [ObservableProperty] public partial string Amount { get; set; } = string.Empty;
        [ObservableProperty] public partial string Cost { get; set; } = string.Empty;
        [ObservableProperty] public partial string Priority { get; set; } = string.Empty;
        [ObservableProperty] public partial string Description { get; set; } = string.Empty;

        public Task UpdateStageSuggestionsAsync(string input)
        {
            StageInput = input;
            SelectedStage = null;
            StageSuggestions = _allStages
                .Where(option => option.Label.Contains(input.Trim(), StringComparison.CurrentCultureIgnoreCase))
                .Take(25)
                .Select(option => option.Label)
                .ToList();
            return Task.CompletedTask;
        }

        public bool TrySelectStage(string? label)
        {
            var option = _allStages.SingleOrDefault(item => string.Equals(item.Label, label, StringComparison.CurrentCultureIgnoreCase));
            if (option is null)
            {
                return false;
            }

            SelectedStage = option;
            StageInput = option.Label;
            return true;
        }

        public void CommitStage(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                SelectedStage = null;
                StageInput = string.Empty;
                return;
            }

            if (!TrySelectStage(input))
            {
                StageInput = SelectedStage?.Label ?? string.Empty;
            }
        }

        partial void OnSelectedToolChanged(IsecurityToolCatalogItem? value)
        {
            if (value is not null)
            {
                PriceCost = Text(value.DefaultCost);
                RecalculateCost();
            }
        }

        partial void OnPriceCostChanged(string value) => RecalculateCost();
        partial void OnAmountChanged(string value) => RecalculateCost();

        private void RecalculateCost()
        {
            if (TryDecimal(PriceCost, out var price) && TryDecimal(Amount, out var amount))
            {
                Cost = Text(price * amount);
            }
        }

        private static bool TryDecimal(string value, out decimal result) =>
            decimal.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out result);

        private static string Text(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
