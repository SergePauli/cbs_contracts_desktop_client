using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class OrderEditViewModel : ObservableObject
    {
        private readonly Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>> _loadContragentsAsync;
        private CancellationTokenSource? _contragentCts;

        public OrderEditViewModel(
            OrderEditState state,
            IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions,
            Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>> loadContragentsAsync)
        {
            State = state;
            StatusOptions = statusOptions;
            _loadContragentsAsync = loadContragentsAsync;
            OrderNumber = state.OrderNumber;
            CostText = state.Cost?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            RequestedAt = state.RequestedAt;
            OrderedAt = state.OrderedAt;
            PaymentAt = state.PaymentAt;
            ReceivedAt = state.ReceivedAt;
            Description = state.Description;
            SelectedStatus = statusOptions.SingleOrDefault(option => JsonDataReader.TryGetLong(option.Value) == state.OrderStatusId);
            SelectedContragent = state.ContragentId is long id
                ? new CbsTableFilterOptionDefinition { Value = id, Label = state.ContragentName }
                : null;
            ContragentInput = SelectedContragent?.Label ?? string.Empty;
            ContragentOptions = SelectedContragent is null ? [] : [SelectedContragent];
        }

        public OrderEditState State { get; }
        public IReadOnlyList<CbsTableFilterOptionDefinition> StatusOptions { get; }
        public IReadOnlyList<string> ContragentSuggestionLabels => ContragentOptions.Select(static option => option.Label).ToList();

        [ObservableProperty] public partial string OrderNumber { get; set; } = string.Empty;
        [ObservableProperty] public partial string ContragentInput { get; set; } = string.Empty;
        [ObservableProperty] public partial IReadOnlyList<CbsTableFilterOptionDefinition> ContragentOptions { get; set; } = [];
        [ObservableProperty] public partial CbsTableFilterOptionDefinition? SelectedContragent { get; set; }
        [ObservableProperty] public partial CbsTableFilterOptionDefinition? SelectedStatus { get; set; }
        [ObservableProperty] public partial string CostText { get; set; } = string.Empty;
        [ObservableProperty] public partial DateTimeOffset? RequestedAt { get; set; }
        [ObservableProperty] public partial DateTimeOffset? OrderedAt { get; set; }
        [ObservableProperty] public partial DateTimeOffset? PaymentAt { get; set; }
        [ObservableProperty] public partial DateTimeOffset? ReceivedAt { get; set; }
        [ObservableProperty] public partial string Description { get; set; } = string.Empty;

        public async Task UpdateContragentOptionsAsync(string text)
        {
            _contragentCts?.Cancel();
            _contragentCts = new CancellationTokenSource();
            ContragentInput = text;
            SelectedContragent = null;
            ContragentOptions = await _loadContragentsAsync(text, _contragentCts.Token);
            OnPropertyChanged(nameof(ContragentSuggestionLabels));
        }

        public bool TrySelectContragent(string? label)
        {
            var option = ContragentOptions.SingleOrDefault(item => string.Equals(item.Label, label, StringComparison.CurrentCultureIgnoreCase));
            if (option is null) return false;
            SelectedContragent = option;
            ContragentInput = option.Label;
            return true;
        }

        public void CommitContragent(string? text)
        {
            if (!TrySelectContragent(text)) ContragentInput = SelectedContragent?.Label ?? string.Empty;
        }
    }
}
