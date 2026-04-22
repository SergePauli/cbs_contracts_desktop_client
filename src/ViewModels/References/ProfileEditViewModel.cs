using System.Globalization;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.ViewModels.References
{
    public partial class ProfileEditViewModel : ObservableObject
    {
        private readonly Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>> _loadPositionOptionsAsync;
        private CancellationTokenSource? _positionLookupCts;

        public ProfileEditViewModel(
            ProfileEditDialogState state,
            Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>>? loadPositionOptionsAsync = null)
        {
            State = state;
            _loadPositionOptionsAsync = loadPositionOptionsAsync ?? LoadEmptyPositionOptionsAsync;

            PositionInput = state.PositionName;
            SelectedDepartmentId = state.DepartmentId;
            Password = state.Password;
            IsActivated = state.IsActive;
        }

        public ProfileEditDialogState State { get; }

        public string DialogTitle => State.IsCreateMode
            ? $"{State.Definition.Title}: новый профиль"
            : $"{State.Definition.Title}: профиль пользователя";

        public string DescriptionText => "Специализированный диалог редактирования пользователей.";

        public string PrimaryButtonText => "Сохранить";

        public bool CanSubmit => false;

        public long? Id => State.Id;

        public string Login => State.Login;

        public string Email => State.Email;

        public string PersonName => State.PersonName;

        public string Role => State.Role;

        public string PositionName => ResolvePositionName();

        public long? PositionId => SelectedPositionOption?.Value switch
        {
            long int64Value => int64Value,
            int int32Value => int32Value,
            decimal decimalValue => (long)decimalValue,
            _ => State.PositionId
        };

        public string DepartmentName => State.DepartmentName;

        public string LastLoginText => State.LastLoginText;

        public IReadOnlyList<CbsTableFilterOptionDefinition> DepartmentOptions => State.DepartmentOptions;

        public IReadOnlyList<string> PositionSuggestionLabels => PositionOptions
            .Select(static option => option.Label)
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        [ObservableProperty]
        public partial string PositionInput { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(PositionSuggestionLabels))]
        [ObservableProperty]
        public partial IReadOnlyList<CbsTableFilterOptionDefinition> PositionOptions { get; set; } = [];

        [ObservableProperty]
        public partial CbsTableFilterOptionDefinition? SelectedPositionOption { get; set; }

        [ObservableProperty]
        public partial long? SelectedDepartmentId { get; set; }

        [ObservableProperty]
        public partial string Password { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsActivated { get; set; }

        public async Task UpdatePositionOptionsAsync(string rawInput)
        {
            var searchText = rawInput?.Trim() ?? string.Empty;
            PositionInput = rawInput ?? string.Empty;

            _positionLookupCts?.Cancel();
            _positionLookupCts?.Dispose();
            _positionLookupCts = new CancellationTokenSource();
            var cancellationToken = _positionLookupCts.Token;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                PositionOptions = [];
                SelectedPositionOption = null;
                OnPropertyChanged(nameof(PositionName));
                return;
            }

            if (MatchesPersistedPositionValue(searchText))
            {
                PositionOptions = [];
                SelectedPositionOption = null;
                OnPropertyChanged(nameof(PositionName));
                return;
            }

            try
            {
                var options = await _loadPositionOptionsAsync(searchText, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                PositionOptions = options;
                if (SelectedPositionOption is not null
                    && !options.Any(option => Equals(option.Value, SelectedPositionOption.Value)))
                {
                    SelectedPositionOption = null;
                }
            }
            catch (OperationCanceledException)
            {
            }

            OnPropertyChanged(nameof(PositionName));
        }

        public void SelectPositionOption(CbsTableFilterOptionDefinition? option)
        {
            SelectedPositionOption = option;
            PositionInput = option?.Label ?? string.Empty;
            OnPropertyChanged(nameof(PositionName));
        }

        public CbsTableFilterOptionDefinition? FindPositionOption(string? label)
        {
            var normalizedLabel = label?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedLabel))
            {
                return null;
            }

            return PositionOptions.FirstOrDefault(option =>
                string.Equals(option.Label, normalizedLabel, StringComparison.CurrentCultureIgnoreCase));
        }

        public void CommitPositionInput(string? rawInput)
        {
            var trimmedInput = rawInput?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedInput))
            {
                PositionInput = string.Empty;
                SelectedPositionOption = null;
                OnPropertyChanged(nameof(PositionName));
                return;
            }

            if (PositionOptions.Count > 0)
            {
                var exactMatch = PositionOptions.FirstOrDefault(option =>
                    string.Equals(option.Label, trimmedInput, StringComparison.CurrentCultureIgnoreCase));

                if (exactMatch is not null)
                {
                    SelectPositionOption(exactMatch);
                    return;
                }

                PositionInput = trimmedInput;
                OnPropertyChanged(nameof(PositionName));
                return;
            }

            PositionInput = CapitalizeFirst(trimmedInput);
            SelectedPositionOption = null;
            OnPropertyChanged(nameof(PositionName));
        }

        private string ResolvePositionName()
        {
            if (SelectedPositionOption is not null)
            {
                return SelectedPositionOption.Label;
            }

            return PositionOptions.Count == 0
                ? CapitalizeFirst(PositionInput)
                : string.Empty;
        }

        private bool MatchesPersistedPositionValue(string searchText)
        {
            return !string.IsNullOrWhiteSpace(State.PositionName)
                && string.Equals(
                    State.PositionName.Trim(),
                    searchText,
                    StringComparison.CurrentCultureIgnoreCase);
        }

        private static string CapitalizeFirst(string? value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return string.Empty;
            }

            if (trimmed.Length == 1)
            {
                return trimmed.ToUpper(CultureInfo.CurrentCulture);
            }

            return char.ToUpper(trimmed[0], CultureInfo.CurrentCulture) + trimmed[1..];
        }

        private static Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadEmptyPositionOptionsAsync(
            string _,
            CancellationToken __)
        {
            return Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>([]);
        }
    }
}
