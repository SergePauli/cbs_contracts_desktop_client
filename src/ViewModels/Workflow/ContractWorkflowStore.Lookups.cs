using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services.References;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class ContractWorkflowStore
    {
        private IReadOnlyList<CbsTableFilterOptionDefinition>? _allStatusOptions;

        private Task<IReadOnlyList<CbsTableFilterOptionDefinition>>? _allStatusOptionsTask;

        public Task<IReadOnlyList<CbsTableFilterOptionDefinition>> GetAllStatusOptionsAsync(
            IReferenceLookupCacheService referenceLookupCacheService,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(referenceLookupCacheService);

            if (_allStatusOptions is not null)
            {
                return Task.FromResult(_allStatusOptions);
            }

            _allStatusOptionsTask ??= LoadAllStatusOptionsAsync(referenceLookupCacheService, cancellationToken);
            return _allStatusOptionsTask;
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadAllStatusOptionsAsync(
            IReferenceLookupCacheService referenceLookupCacheService,
            CancellationToken cancellationToken)
        {
            var options = await referenceLookupCacheService.GetOptionsAsync("Status", cancellationToken: cancellationToken);
            _allStatusOptions = options;
            _allStatusOptionsTask = null;
            return options;
        }
    }
}
