// Stores UI option sources shared by table filters and dialogs within a host view.
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed class OptionsSourceRegistry
    {
        private readonly Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>> _sources =
            new(StringComparer.OrdinalIgnoreCase);

        public void Clear()
        {
            _sources.Clear();
        }

        public void Set(string key, IReadOnlyList<CbsTableFilterOptionDefinition> options)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(options);

            _sources[key] = options;
        }

        public void SetRange(IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>> sources)
        {
            ArgumentNullException.ThrowIfNull(sources);

            foreach (var (key, options) in sources)
            {
                Set(key, options);
            }
        }

        public void ReplaceWith(IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>> sources)
        {
            Clear();
            SetRange(sources);
        }

        public IReadOnlyList<CbsTableFilterOptionDefinition> Get(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return _sources.TryGetValue(key, out var options) ? options : [];
        }

        public IReadOnlyDictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>> Snapshot()
        {
            return new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(
                _sources,
                StringComparer.OrdinalIgnoreCase);
        }
    }
}
