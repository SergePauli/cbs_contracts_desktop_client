using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Data;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class StageOrderNeedsCreateViewModel(
        Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>> loadContragentsAsync)
        : ObservableObject
    {
        private CancellationTokenSource? _contragentCts;

        public IReadOnlyList<string> ContragentSuggestionLabels =>
            ContragentOptions.Select(static option => option.Label).ToList();

        [ObservableProperty] public partial string ContragentInput { get; set; } = string.Empty;
        [ObservableProperty] public partial IReadOnlyList<CbsTableFilterOptionDefinition> ContragentOptions { get; set; } = [];
        [ObservableProperty] public partial CbsTableFilterOptionDefinition? SelectedContragent { get; set; }
        [ObservableProperty] public partial string Description { get; set; } = string.Empty;

        public async Task UpdateContragentOptionsAsync(string text)
        {
            _contragentCts?.Cancel();
            _contragentCts = new CancellationTokenSource();
            ContragentInput = text;
            SelectedContragent = null;
            ContragentOptions = await loadContragentsAsync(text, _contragentCts.Token);
            OnPropertyChanged(nameof(ContragentSuggestionLabels));
        }

        public bool TrySelectContragent(string? label)
        {
            var option = ContragentOptions.SingleOrDefault(item =>
                string.Equals(item.Label, label, StringComparison.CurrentCultureIgnoreCase));
            if (option is null)
            {
                return false;
            }

            SelectedContragent = option;
            ContragentInput = option.Label;
            return true;
        }

        public void CommitContragent(string? text)
        {
            if (!TrySelectContragent(text))
            {
                ContragentInput = SelectedContragent?.Label ?? string.Empty;
            }
        }

        public CreateOrderFromNeedsInput BuildInput()
        {
            var contragentId = JsonDataReader.TryGetLong(SelectedContragent?.Value)
                ?? throw new InvalidOperationException("Выберите поставщика.");
            return new CreateOrderFromNeedsInput(contragentId, Description);
        }
    }
}
