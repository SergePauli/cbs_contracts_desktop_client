// Coordinates IsecurityTool dialog state, catalog values, payload creation, and persistence.
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.ViewModels.References;
using CbsContractsDesktopClient.Views.References;
using Microsoft.UI.Xaml;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class IsecurityToolEditWorkflow
    {
        private readonly IsecurityToolCatalogService _catalogService;
        private readonly IModelMutationService _modelMutationService;

        public IsecurityToolEditWorkflow(
            IsecurityToolCatalogService catalogService,
            IModelMutationService modelMutationService)
        {
            _catalogService = catalogService;
            _modelMutationService = modelMutationService;
        }

        public async Task<TableDataRow?> ShowAsync(
            ReferenceDefinition definition,
            bool isCreateMode,
            TableDataRow? sourceRow,
            XamlRoot xamlRoot,
            CancellationToken cancellationToken = default)
        {
            var unitOptions = await _catalogService.LoadUnitOptionsAsync(cancellationToken);
            var viewModel = isCreateMode
                ? ReferenceEditViewModel.CreateForCreate(definition)
                : ReferenceEditViewModel.CreateForEdit(definition, sourceRow!);
            var dialog = new IsecurityToolEditDialog(viewModel, unitOptions)
            {
                XamlRoot = xamlRoot
            };

            TableDataRow? savedRow = null;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    var payload = isCreateMode
                        ? ReferenceEditPayloadBuilder.BuildForCreate(viewModel)
                        : ReferenceEditPayloadBuilder.BuildForUpdate(viewModel);

                    savedRow = isCreateMode
                        ? await _modelMutationService.CreateAsync(definition.Model, payload, cancellationToken)
                        : await _modelMutationService.UpdateAsync(definition.Model, payload, cancellationToken);
                }
                catch (Exception ex)
                {
                    dialog.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };

            await dialog.ShowAsync();
            return dialog.WasSaved ? savedRow : null;
        }
    }
}
