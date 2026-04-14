using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.ViewModels.Data;

namespace CbsContractsDesktopClient.ViewModels.Shell
{
    public partial class StatusTableViewModel : ObservableObject
    {
        private readonly LazyDataViewState<StatusItem> _state;

        public StatusTableViewModel(IDataQueryService dataQueryService)
        {
            _state = new LazyDataViewState<StatusItem>(
                dataQueryService,
                model: "Status",
                preset: "item",
                pageSize: 3,
                fieldMap: new Dictionary<string, string>
                {
                    ["id"] = "id",
                    ["name"] = "name"
                },
                placeholderFactory: static () => new StatusItem(),
                initialSorts:
                [
                    new DataSortCriterion
                    {
                        FieldKey = "id",
                        Direction = DataSortDirection.Ascending
                    }
                ]);

            ((INotifyPropertyChanged)_state.Items).PropertyChanged += OnItemsPropertyChanged;
        }

        public LazyDataViewState<StatusItem> State => _state;

        public CbsContractsDesktopClient.Collections.LazyDataCollection<StatusItem> Items => _state.Items;

        [ObservableProperty]
        public partial bool IsLoading { get; set; }

        [ObservableProperty]
        public partial string ErrorMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial int TotalCount { get; set; }

        public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        {
            await _state.InitializeAsync(cancellationToken);
        }

        public async Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            await _state.RefreshAsync(cancellationToken);
        }

        private void OnItemsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CbsContractsDesktopClient.Collections.LazyDataCollection<StatusItem>.IsLoading))
            {
                IsLoading = _state.Items.IsLoading;
            }
            else if (e.PropertyName == nameof(CbsContractsDesktopClient.Collections.LazyDataCollection<StatusItem>.ErrorMessage))
            {
                ErrorMessage = string.IsNullOrWhiteSpace(_state.Items.ErrorMessage)
                    ? string.Empty
                    : $"Не удалось загрузить справочник Status: {_state.Items.ErrorMessage}";
            }
            else if (e.PropertyName == nameof(CbsContractsDesktopClient.Collections.LazyDataCollection<StatusItem>.TotalCount))
            {
                TotalCount = _state.Items.TotalCount;
            }
        }
    }
}
