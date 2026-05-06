using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.ViewModels.References
{
    public partial class ContragentEditViewModel : ObservableObject
    {
        private readonly Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>> _loadAddressOptionsAsync;
        private CancellationTokenSource? _addressLookupCts;

        public ContragentEditViewModel(
            ContragentEditDialogState state,
            Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>>? loadAddressOptionsAsync = null)
        {
            State = state;
            _loadAddressOptionsAsync = loadAddressOptionsAsync ?? LoadEmptyAddressOptionsAsync;
            Inn = state.Inn;
            Kpp = state.Kpp;
            Division = state.Division;
            SelectedOwnershipId = state.OwnershipId;
            Name = state.Name;
            SelectedRegionId = state.RegionId;
            AddressReal = state.AddressReal;
            FullName = state.FullName;
            Description = state.Description;
            ContactsText = state.ContactsText;
            Ogrn = state.Ogrn;
            Okfc = state.Okfc;
            Okopf = state.Okopf;
            Okpo = state.Okpo;
            Okogu = state.Okogu;
            Okved = state.Okved;
            Oktmo = state.Oktmo;
            BankName = state.BankName;
            BankBik = state.BankBik;
            BankAccount = state.BankAccount;
            BankCorAccount = state.BankCorAccount;
            SelectedAddressOption = state.InitialAddressOption;
            AddressOptions = state.InitialAddressOption is null
                ? []
                : [state.InitialAddressOption];
            ActiveRegistration = state.OrganizationHistory.FirstOrDefault(static item => item.IsActive);
        }

        public ContragentEditDialogState State { get; }

        public string DialogTitle => State.IsCreateMode
            ? "Новый контрагент"
            : "Редактирование контрагента";

        public string PrimaryButtonText => "Сохранить";

        public long? Id => State.Id;

        public IReadOnlyList<CbsTableFilterOptionDefinition> OwnershipOptions => State.OwnershipOptions;

        public IReadOnlyList<CbsTableFilterOptionDefinition> RegionOptions => State.RegionOptions;

        public IReadOnlyList<string> AddressSuggestionLabels => AddressOptions
            .Select(static option => option.Label)
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        public long? SelectedAddressId => ToLong(SelectedAddressOption?.Value);

        public IReadOnlyList<ContragentOrganizationHistoryItem> OrganizationHistory => State.OrganizationHistory;

        public IReadOnlyList<ContragentOrganizationHistoryItem> VisibleOrganizationHistory =>
            State.OrganizationHistory.Where(static item => !item.IsMarkedForDestroy).ToList();

        public ContragentOrganizationHistoryItem? ActiveRegistration { get; private set; }

        public bool CanSubmit => State.IsCreateMode
            ? HasRequiredFields()
            : HasRequiredFields() && HasChanges();

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Inn { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Kpp { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Division { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial long? SelectedOwnershipId { get; set; }

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Name { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial long? SelectedRegionId { get; set; }

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string AddressReal { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [NotifyPropertyChangedFor(nameof(AddressSuggestionLabels))]
        [ObservableProperty]
        public partial IReadOnlyList<CbsTableFilterOptionDefinition> AddressOptions { get; set; } = [];

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [NotifyPropertyChangedFor(nameof(SelectedAddressId))]
        [ObservableProperty]
        public partial CbsTableFilterOptionDefinition? SelectedAddressOption { get; set; }

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string FullName { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string ContactsText { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Description { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Ogrn { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Okfc { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Okopf { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Okpo { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Okogu { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Okved { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string Oktmo { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string BankName { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string BankBik { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string BankAccount { get; set; } = string.Empty;

        [NotifyPropertyChangedFor(nameof(CanSubmit))]
        [ObservableProperty]
        public partial string BankCorAccount { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ErrorInfoMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsErrorInfoVisible { get; set; }

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

        public async Task UpdateAddressOptionsAsync(string rawInput)
        {
            _addressLookupCts?.Cancel();
            _addressLookupCts?.Dispose();

            var searchText = rawInput?.Trim() ?? string.Empty;
            AddressReal = rawInput ?? string.Empty;
            SelectedAddressOption = null;
            var cancellationTokenSource = new CancellationTokenSource();
            _addressLookupCts = cancellationTokenSource;

            if (string.IsNullOrWhiteSpace(searchText)
                || string.Equals(searchText, State.AddressReal.Trim(), StringComparison.CurrentCultureIgnoreCase))
            {
                AddressOptions = [];
                NotifyAddressStateChanged();
                return;
            }

            try
            {
                var options = await _loadAddressOptionsAsync(searchText, cancellationTokenSource.Token);
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    AddressOptions = MergeSelectedOption(options, SelectedAddressOption);
                }
            }
            catch (OperationCanceledException)
            {
            }

            NotifyAddressStateChanged();
        }

        public void SelectAddressOption(CbsTableFilterOptionDefinition? option)
        {
            SelectedAddressOption = option;
            AddressReal = option?.Label ?? string.Empty;
            NotifyAddressStateChanged();
        }

        public CbsTableFilterOptionDefinition? FindAddressOption(string? label)
        {
            return FindOption(AddressOptions, label);
        }

        public void CommitAddressInput(string? rawInput)
        {
            var trimmedInput = rawInput?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedInput))
            {
                AddressReal = string.Empty;
                SelectedAddressOption = null;
                NotifyAddressStateChanged();
                return;
            }

            var option = FindOption(AddressOptions, trimmedInput);
            if (option is not null)
            {
                SelectAddressOption(option);
                return;
            }

            AddressReal = trimmedInput;
            SelectedAddressOption = null;
            NotifyAddressStateChanged();
        }

        public bool ActivateRegistration(ContragentOrganizationHistoryItem registration)
        {
            if (State.IsCreateMode || registration.IsActive || registration.IsMarkedForDestroy)
            {
                return false;
            }

            foreach (var item in OrganizationHistory)
            {
                item.IsActive = ReferenceEquals(item, registration);
            }

            ActiveRegistration = registration;
            ApplyRegistrationRequisites(registration);
            OnPropertyChanged(nameof(OrganizationHistory));
            OnPropertyChanged(nameof(VisibleOrganizationHistory));
            OnPropertyChanged(nameof(CanSubmit));
            return true;
        }

        public bool MarkRegistrationForDestroy(ContragentOrganizationHistoryItem registration)
        {
            if (State.IsCreateMode || registration.IsActive || registration.Id is null)
            {
                return false;
            }

            registration.IsMarkedForDestroy = true;
            OnPropertyChanged(nameof(OrganizationHistory));
            OnPropertyChanged(nameof(VisibleOrganizationHistory));
            OnPropertyChanged(nameof(CanSubmit));
            return true;
        }

        private bool HasRequiredFields()
        {
            return !string.IsNullOrWhiteSpace(Inn)
                && !string.IsNullOrWhiteSpace(Name)
                && SelectedOwnershipId is not null;
        }

        private bool HasChanges()
        {
            return !Same(Inn, State.Inn)
                || HasRegistrationUsageChanges()
                || HasRegistrationDestroyChanges()
                || !Same(Kpp, State.Kpp)
                || !Same(Division, State.Division)
                || SelectedOwnershipId != State.OwnershipId
                || !Same(Name, State.Name)
                || SelectedRegionId != State.RegionId
                || !Same(AddressReal, State.AddressReal)
                || SelectedAddressId != State.AddressRealAddressId
                || !Same(FullName, State.FullName)
                || !Same(Description, State.Description)
                || !Same(ContactsText, State.ContactsText)
                || !Same(Ogrn, State.Ogrn)
                || !Same(Okfc, State.Okfc)
                || !Same(Okopf, State.Okopf)
                || !Same(Okpo, State.Okpo)
                || !Same(Okogu, State.Okogu)
                || !Same(Okved, State.Okved)
                || !Same(Oktmo, State.Oktmo)
                || !Same(BankName, State.BankName)
                || !Same(BankBik, State.BankBik)
                || !Same(BankAccount, State.BankAccount)
                || !Same(BankCorAccount, State.BankCorAccount);
        }

        private void ApplyRegistrationRequisites(ContragentOrganizationHistoryItem registration)
        {
            Inn = registration.Inn;
            Kpp = registration.Kpp;
            Division = registration.Division;
            SelectedOwnershipId = registration.OwnershipId;
            Name = registration.Name;
            FullName = registration.FullName;
            Ogrn = registration.Ogrn;
            Okfc = registration.Okfc;
            Okopf = registration.Okopf;
            Okpo = registration.Okpo;
            Okogu = registration.Okogu;
            Okved = registration.Okved;
            Oktmo = registration.Oktmo;
        }

        private bool HasRegistrationUsageChanges()
        {
            return OrganizationHistory.Any(static item => !item.IsMarkedForDestroy && item.IsActive != item.OriginalIsActive);
        }

        private bool HasRegistrationDestroyChanges()
        {
            return OrganizationHistory.Any(static item => item.IsMarkedForDestroy);
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.CurrentCulture);
        }

        private void NotifyAddressStateChanged()
        {
            OnPropertyChanged(nameof(AddressSuggestionLabels));
            OnPropertyChanged(nameof(SelectedAddressId));
            OnPropertyChanged(nameof(CanSubmit));
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

        private static long? ToLong(object? value)
        {
            return value switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string text when long.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadEmptyAddressOptionsAsync(
            string _,
            CancellationToken __)
        {
            return Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>([]);
        }
    }
}
