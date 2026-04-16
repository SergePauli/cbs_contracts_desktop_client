using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Collections;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Services;

namespace CbsContractsDesktopClient.ViewModels.Data
{
    public partial class LazyDataViewState<TItem> : ObservableObject
        where TItem : class
    {
        private readonly IDataQueryService _dataQueryService;
        private readonly Func<TItem> _placeholderFactory;
        private readonly Func<TItem, bool>? _isPlaceholder;
        private readonly string _model;
        private readonly string? _preset;
        private readonly int _pageSize;
        private readonly IReadOnlyDictionary<string, string> _fieldMap;

        public LazyDataViewState(
            IDataQueryService dataQueryService,
            string model,
            string? preset,
            int pageSize,
            IReadOnlyDictionary<string, string> fieldMap,
            Func<TItem> placeholderFactory,
            Func<TItem, bool>? isPlaceholder = null,
            IEnumerable<DataFilterCriterion>? initialFilters = null,
            IEnumerable<DataSortCriterion>? initialSorts = null)
        {
            _dataQueryService = dataQueryService;
            _placeholderFactory = placeholderFactory;
            _isPlaceholder = isPlaceholder;
            _model = model;
            _preset = preset;
            _pageSize = pageSize;
            _fieldMap = fieldMap;

            Filters = [];
            Sorts = [];

            if (initialFilters is not null)
            {
                foreach (var filter in initialFilters)
                {
                    Filters.Add(filter);
                }
            }

            if (initialSorts is not null)
            {
                foreach (var sort in initialSorts)
                {
                    Sorts.Add(sort);
                }
            }

            Items = new LazyDataCollection<TItem>(_dataQueryService, BuildQuery(), _placeholderFactory, _isPlaceholder);
        }

        public LazyDataCollection<TItem> Items { get; }

        public ObservableCollection<DataFilterCriterion> Filters { get; }

        public ObservableCollection<DataSortCriterion> Sorts { get; }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await Items.InitializeAsync(cancellationToken);
        }

        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            await Items.ReplaceQueryAsync(BuildQuery(), cancellationToken);
        }

        public async Task SetFilterAsync(
            string fieldKey,
            DataFilterMode filterMode,
            DataFilterMatchMode matchMode,
            object? value,
            CancellationToken cancellationToken = default)
        {
            var existing = Filters
                .Where(x => x.FieldKey == fieldKey)
                .ToList();

            foreach (var criterion in existing)
            {
                Filters.Remove(criterion);
            }

            if (value is not null && !(value is string text && string.IsNullOrWhiteSpace(text)))
            {
                Filters.Add(new DataFilterCriterion
                {
                    FieldKey = fieldKey,
                    FilterMode = filterMode,
                    MatchMode = matchMode,
                    Value = value
                });
            }

            await RefreshAsync(cancellationToken);
        }

        public async Task ClearFiltersAsync(CancellationToken cancellationToken = default)
        {
            Filters.Clear();
            await RefreshAsync(cancellationToken);
        }

        public async Task SetSortAsync(
            string fieldKey,
            DataSortDirection direction,
            CancellationToken cancellationToken = default)
        {
            Sorts.Clear();
            Sorts.Add(new DataSortCriterion
            {
                FieldKey = fieldKey,
                Direction = direction
            });

            await RefreshAsync(cancellationToken);
        }

        public async Task ClearSortsAsync(CancellationToken cancellationToken = default)
        {
            Sorts.Clear();
            await RefreshAsync(cancellationToken);
        }

        private LazyDataQuery BuildQuery()
        {
            return new LazyDataQuery
            {
                Model = _model,
                Preset = _preset,
                PageSize = _pageSize,
                Filters = DataQueryStateBuilder.BuildFilters(Filters, _fieldMap),
                Sorts = DataQueryStateBuilder.BuildSorts(Sorts, _fieldMap)
            };
        }
    }
}
