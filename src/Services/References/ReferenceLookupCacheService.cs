using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class ReferenceLookupCacheService : IReferenceLookupCacheService
    {
        private const int DefaultLookupLimit = 1000;
        private static readonly HashSet<string> CacheableModels = new(StringComparer.OrdinalIgnoreCase)
        {
            "Area",
            "Department",
            "Ownership",
            "Status",
            "TaskKind"
        };

        private readonly IDataQueryService _dataQueryService;
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, IReadOnlyList<ReferenceLookupItem>> _itemsByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SemaphoreSlim> _gatesByKey = new(StringComparer.OrdinalIgnoreCase);

        public ReferenceLookupCacheService(IDataQueryService dataQueryService)
        {
            _dataQueryService = dataQueryService;
        }

        public async Task<IReadOnlyList<ReferenceLookupItem>> GetItemsAsync(
            string model,
            string? preset = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return [];
            }

            var effectivePreset = ResolvePreset(model, preset);
            if (!CacheableModels.Contains(model))
            {
                return await LoadItemsAsync(model, effectivePreset, cancellationToken);
            }

            var cacheKey = BuildCacheKey(model, effectivePreset);

            if (TryGetCachedItems(cacheKey, out var cachedItems))
            {
                return cachedItems;
            }

            var gate = GetGate(cacheKey);
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (TryGetCachedItems(cacheKey, out cachedItems))
                {
                    return cachedItems;
                }

                var loadedItems = await LoadItemsAsync(model, effectivePreset, cancellationToken);
                lock (_syncRoot)
                {
                    _itemsByKey[cacheKey] = loadedItems;
                }

                return loadedItems;
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> GetOptionsAsync(
            string model,
            string? preset = null,
            CancellationToken cancellationToken = default)
        {
            var items = await GetItemsAsync(model, preset, cancellationToken);
            return items
                .Select(static item => item.ToOption())
                .Where(static option => option.Value is not null && !string.IsNullOrWhiteSpace(option.Label))
                .DistinctBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public async Task<ReferenceLookupItem?> FindByIdAsync(
            string model,
            object? id,
            string? preset = null,
            CancellationToken cancellationToken = default)
        {
            var normalizedId = NormalizeLookupValue(id);
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                return null;
            }

            var items = await GetItemsAsync(model, preset, cancellationToken);
            return items.FirstOrDefault(item =>
                string.Equals(NormalizeLookupValue(item.Id), normalizedId, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<ReferenceLookupItem?> FindOwnershipAsync(
            object? id,
            string? code,
            CancellationToken cancellationToken = default)
        {
            var byId = await FindByIdAsync("Ownership", id, "card", cancellationToken);
            if (byId is not null)
            {
                return byId;
            }

            var normalizedCode = NormalizeLookupValue(code);
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                return null;
            }

            var items = await GetItemsAsync("Ownership", "card", cancellationToken);
            return items.FirstOrDefault(item =>
                string.Equals(NormalizeLookupValue(item.Code), normalizedCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeLookupValue(item.Row.GetValue("okopf")), normalizedCode, StringComparison.OrdinalIgnoreCase));
        }

        public void Invalidate(string model, string? preset = null)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return;
            }

            lock (_syncRoot)
            {
                if (string.IsNullOrWhiteSpace(preset))
                {
                    var prefix = $"{model.Trim()}:";
                    foreach (var key in _itemsByKey.Keys
                        .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        .ToArray())
                    {
                        _itemsByKey.Remove(key);
                    }

                    return;
                }

                _itemsByKey.Remove(BuildCacheKey(model, ResolvePreset(model, preset)));
            }
        }

        private async Task<IReadOnlyList<ReferenceLookupItem>> LoadItemsAsync(
            string model,
            string preset,
            CancellationToken cancellationToken)
        {
            var rows = await _dataQueryService.GetDataAsync<ReferenceDataRow>(
                new DataQueryRequest
                {
                    Model = model,
                    Preset = preset,
                    Sorts = [GetSortField(model, preset)],
                    Limit = DefaultLookupLimit
                },
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(row => ToLookupItem(model, preset, row))
                .Where(static item => item.Id is not null && !string.IsNullOrWhiteSpace(item.DisplayName))
                .ToList();
        }

        private static ReferenceLookupItem ToLookupItem(string model, string preset, ReferenceDataRow row)
        {
            var name = GetText(row, "name", "short_name", "title", "display_name", "full_name");
            var fullName = GetText(row, "full_name", "name", "title", "display_name");
            var code = GetText(row, "code", "okopf");

            return new ReferenceLookupItem
            {
                Model = model,
                Preset = preset,
                Id = row.GetValue("id"),
                Name = name ?? string.Empty,
                FullName = fullName ?? string.Empty,
                Code = code ?? string.Empty,
                Row = row
            };
        }

        private static string ResolvePreset(string model, string? preset)
        {
            if (!string.IsNullOrWhiteSpace(preset))
            {
                return preset.Trim();
            }

            return string.Equals(model, "Ownership", StringComparison.OrdinalIgnoreCase)
                ? "card"
                : "item";
        }

        private static string GetSortField(string model, string preset)
        {
            return string.Equals(model, "Contragent", StringComparison.OrdinalIgnoreCase)
                ? "org.name asc"
                : "name asc";
        }

        private bool TryGetCachedItems(string cacheKey, out IReadOnlyList<ReferenceLookupItem> items)
        {
            lock (_syncRoot)
            {
                return _itemsByKey.TryGetValue(cacheKey, out items!);
            }
        }

        private SemaphoreSlim GetGate(string cacheKey)
        {
            lock (_syncRoot)
            {
                if (!_gatesByKey.TryGetValue(cacheKey, out var gate))
                {
                    gate = new SemaphoreSlim(1, 1);
                    _gatesByKey[cacheKey] = gate;
                }

                return gate;
            }
        }

        private static string BuildCacheKey(string model, string preset)
        {
            return $"{model.Trim()}:{preset.Trim()}";
        }

        private static string? NormalizeLookupValue(object? value)
        {
            return value switch
            {
                null => null,
                string text => string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
                _ => value.ToString()?.Trim()
            };
        }
    }
}
