using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.ViewModels.References
{
    public partial class EmployeeEditViewModel : ObservableObject
    {
        private readonly Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>> _loadPositionOptionsAsync;
        private readonly Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>> _loadContragentOptionsAsync;
        private CancellationTokenSource? _positionLookupCts;
        private CancellationTokenSource? _contragentLookupCts;

        public EmployeeEditViewModel(
            EmployeeEditDialogState state,
            Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>>? loadPositionOptionsAsync = null,
            Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>>? loadContragentOptionsAsync = null)
        {
            State = state;
            _loadPositionOptionsAsync = loadPositionOptionsAsync ?? LoadEmptyOptionsAsync;
            _loadContragentOptionsAsync = loadContragentOptionsAsync ?? LoadEmptyOptionsAsync;

            PersonName = state.PersonName;
            PositionInput = state.PositionName;
            ContragentInput = state.ContragentName;
            SelectedPositionOption = state.InitialPositionOption;
            SelectedContragentOption = state.InitialContragentOption;
            ContragentOptions = state.InitialContragentOption is null
                ? []
                : [state.InitialContragentOption];
            IsUsed = state.IsUsed;
            PriorityText = state.Priority?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            Description = state.Description;
            ContactsText = state.ContactsText;
        }

        public EmployeeEditDialogState State { get; }

        public string DialogTitle => State.IsCreateMode
            ? "Создание сотрудника"
            : "Редактирование сотрудника";

        public string PrimaryButtonText => "Сохранить";

        public long? Id => State.Id;

        public string PositionName => ResolvePositionName();

        public string ContragentName => ResolveContragentName();

        public IReadOnlyList<string> PositionSuggestionLabels => PositionOptions
            .Select(static option => option.Label)
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        public IReadOnlyList<string> ContragentSuggestionLabels => ContragentOptions
            .Select(static option => option.Label)
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        public bool CanSubmit => State.IsCreateMode || HasChanges();

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string PersonName { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string PositionInput { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string ContragentInput { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [NotifyPropertyChangedFor(nameof(PositionSuggestionLabels))]
        [ObservableProperty]
        public partial IReadOnlyList<CbsTableFilterOptionDefinition> PositionOptions { get; set; } = [];

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [NotifyPropertyChangedFor(nameof(ContragentSuggestionLabels))]
        [ObservableProperty]
        public partial IReadOnlyList<CbsTableFilterOptionDefinition> ContragentOptions { get; set; } = [];

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial CbsTableFilterOptionDefinition? SelectedPositionOption { get; set; }

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial CbsTableFilterOptionDefinition? SelectedContragentOption { get; set; }

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial bool IsUsed { get; set; }

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string PriorityText { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Description { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string ContactsText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ErrorInfoMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsErrorInfoVisible { get; set; }

        public async Task UpdatePositionOptionsAsync(string rawInput)
        {
            _positionLookupCts?.Cancel();
            _positionLookupCts?.Dispose();
            await UpdateLookupOptionsAsync(
                rawInput,
                value => PositionInput = value,
                value => PositionOptions = value,
                () => SelectedPositionOption = null,
                () => State.PositionName,
                token => _positionLookupCts = token,
                _loadPositionOptionsAsync);
            NotifyPositionStateChanged();
        }

        public async Task UpdateContragentOptionsAsync(string rawInput)
        {
            _contragentLookupCts?.Cancel();
            _contragentLookupCts?.Dispose();
            await UpdateLookupOptionsAsync(
                rawInput,
                value => ContragentInput = value,
                value => ContragentOptions = MergeSelectedOption(value, SelectedContragentOption),
                () => SelectedContragentOption = null,
                () => State.ContragentName,
                token => _contragentLookupCts = token,
                _loadContragentOptionsAsync);
            NotifyContragentStateChanged();
        }

        public void SelectPositionOption(CbsTableFilterOptionDefinition? option)
        {
            SelectedPositionOption = option;
            PositionInput = option?.Label ?? string.Empty;
            NotifyPositionStateChanged();
        }

        public void SelectContragentOption(CbsTableFilterOptionDefinition? option)
        {
            SelectedContragentOption = option;
            ContragentInput = option?.Label ?? string.Empty;
            NotifyContragentStateChanged();
        }

        public CbsTableFilterOptionDefinition? FindPositionOption(string? label)
        {
            return FindOption(PositionOptions, label);
        }

        public CbsTableFilterOptionDefinition? FindContragentOption(string? label)
        {
            return FindOption(ContragentOptions, label);
        }

        public void CommitPositionInput(string? rawInput)
        {
            CommitLookupInput(rawInput, PositionOptions, SelectPositionOption, value => PositionInput = value);
            NotifyPositionStateChanged();
        }

        public void CommitContragentInput(string? rawInput)
        {
            var option = FindOption(ContragentOptions, rawInput);
            if (option is not null)
            {
                SelectContragentOption(option);
                return;
            }

            if (SelectedContragentOption is not null)
            {
                ContragentInput = SelectedContragentOption.Label;
            }
            else
            {
                ContragentInput = string.Empty;
            }

            NotifyContragentStateChanged();
        }

        public void ShowErrorInfo(string? message)
        {
            ErrorInfoMessage = message?.Trim() ?? string.Empty;
            IsErrorInfoVisible = !string.IsNullOrWhiteSpace(ErrorInfoMessage);
        }

        public void ClearErrorInfo()
        {
            ErrorInfoMessage = string.Empty;
            IsErrorInfoVisible = false;
        }

        private async Task UpdateLookupOptionsAsync(
            string rawInput,
            Action<string> setInput,
            Action<IReadOnlyList<CbsTableFilterOptionDefinition>> setOptions,
            Action clearSelectedOption,
            Func<string> getPersistedText,
            Action<CancellationTokenSource> setCancellationTokenSource,
            Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>> loadOptionsAsync)
        {
            var searchText = rawInput?.Trim() ?? string.Empty;
            setInput(rawInput ?? string.Empty);

            var cancellationTokenSource = new CancellationTokenSource();
            setCancellationTokenSource(cancellationTokenSource);
            var cancellationToken = cancellationTokenSource.Token;

            if (string.IsNullOrWhiteSpace(searchText) || MatchesPersistedValue(searchText, getPersistedText()))
            {
                setOptions([]);
                clearSelectedOption();
                return;
            }

            try
            {
                var options = await loadOptionsAsync(searchText, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    setOptions(options);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private string ResolvePositionName()
        {
            if (SelectedPositionOption is not null)
            {
                return SelectedPositionOption.Label;
            }

            return PositionOptions.Count == 0
                ? CapitalizeFirst(PositionInput)
                : PositionInput.Trim();
        }

        private string ResolveContragentName()
        {
            return SelectedContragentOption?.Label ?? ContragentInput.Trim();
        }

        private bool HasChanges()
        {
            return !string.Equals(CollapseWhitespace(PersonName), CollapseWhitespace(State.PersonName), StringComparison.CurrentCulture)
                || !string.Equals(PositionName.Trim(), State.PositionName.Trim(), StringComparison.CurrentCultureIgnoreCase)
                || !string.Equals(ContragentName.Trim(), State.ContragentName.Trim(), StringComparison.CurrentCultureIgnoreCase)
                || IsUsed != State.IsUsed
                || !string.Equals(PriorityText.Trim(), State.Priority?.ToString(CultureInfo.CurrentCulture) ?? string.Empty, StringComparison.CurrentCulture)
                || !string.Equals(Description.Trim(), State.Description.Trim(), StringComparison.CurrentCulture)
                || !string.Equals(NormalizeContactsText(ContactsText), NormalizeContactsText(State.ContactsText), StringComparison.CurrentCultureIgnoreCase);
        }

        private static void CommitLookupInput(
            string? rawInput,
            IReadOnlyList<CbsTableFilterOptionDefinition> options,
            Action<CbsTableFilterOptionDefinition?> selectOption,
            Action<string> setInput)
        {
            var trimmedInput = rawInput?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedInput))
            {
                setInput(string.Empty);
                selectOption(null);
                return;
            }

            var exactMatch = FindOption(options, trimmedInput);
            if (exactMatch is not null)
            {
                selectOption(exactMatch);
                return;
            }

            setInput(CapitalizeFirst(trimmedInput));
        }

        private static CbsTableFilterOptionDefinition? FindOption(
            IReadOnlyList<CbsTableFilterOptionDefinition> options,
            string? label)
        {
            var normalizedLabel = label?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedLabel))
            {
                return null;
            }

            return options.FirstOrDefault(option =>
                string.Equals(option.Label, normalizedLabel, StringComparison.CurrentCultureIgnoreCase));
        }

        private static bool MatchesPersistedValue(string searchText, string persistedText)
        {
            return !string.IsNullOrWhiteSpace(persistedText)
                && string.Equals(
                    persistedText.Trim(),
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

            return trimmed.Length == 1
                ? trimmed.ToUpper(CultureInfo.CurrentCulture)
                : char.ToUpper(trimmed[0], CultureInfo.CurrentCulture) + trimmed[1..];
        }

        private static string CollapseWhitespace(string value)
        {
            return string.Join(
                " ",
                value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private static string NormalizeContactsText(string value)
        {
            return string.Join(
                Environment.NewLine,
                value.Split([Environment.NewLine, "\n", ";", ","], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(static item => !string.IsNullOrWhiteSpace(item)));
        }

        private static Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadEmptyOptionsAsync(
            string _,
            CancellationToken __)
        {
            return Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>([]);
        }

        private void NotifyPositionStateChanged()
        {
            OnPropertyChanged(nameof(PositionName));
            OnPropertyChanged(nameof(PositionSuggestionLabels));
            OnPropertyChanged(nameof(CanSubmit));
        }

        private void NotifyContragentStateChanged()
        {
            OnPropertyChanged(nameof(ContragentName));
            OnPropertyChanged(nameof(ContragentSuggestionLabels));
            OnPropertyChanged(nameof(CanSubmit));
        }

        private static IReadOnlyList<CbsTableFilterOptionDefinition> MergeSelectedOption(
            IReadOnlyList<CbsTableFilterOptionDefinition> options,
            CbsTableFilterOptionDefinition? selectedOption)
        {
            if (selectedOption is null || options.Any(option => Equals(option.Value, selectedOption.Value)))
            {
                return options;
            }

            return [selectedOption, .. options];
        }
    }
}
