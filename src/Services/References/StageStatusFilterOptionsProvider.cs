using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.Shared.Dialogs;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class StageStatusFilterOptionsProvider(IReferenceLookupCacheService lookups)
    {
        public async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            var options = await lookups.GetOptionsAsync("Status", cancellationToken: cancellationToken);
            var optionsById = options
                .Where(static option => JsonDataReader.TryGetLong(option.Value) is not null)
                .GroupBy(static option => JsonDataReader.TryGetLong(option.Value)!.Value)
                .ToDictionary(static group => group.Key, static group => group.First());

            var result = new List<CbsTableFilterOptionDefinition>
            {
                new()
                {
                    Value = null,
                    Label = "Не определен"
                }
            };

            foreach (var statusId in StageContractStatusDialogControls.StageStatusIds.Order())
            {
                if (optionsById.TryGetValue(statusId, out var option))
                {
                    result.Add(option);
                }
            }

            return result;
        }
    }
}
