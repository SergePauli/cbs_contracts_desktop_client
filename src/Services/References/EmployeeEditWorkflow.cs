// Coordinates Employee edit dialog state, lookups, payload creation, save, and cache invalidation.
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.ViewModels.References;
using CbsContractsDesktopClient.Views.References;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class EmployeeEditWorkflow : IEmployeeEditWorkflow
    {
        private readonly IDataQueryService _dataQueryService;
        private readonly IModelMutationService _modelMutationService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private readonly IContragentLookupService _contragentLookupService;

        public EmployeeEditWorkflow(
            IDataQueryService dataQueryService,
            IModelMutationService modelMutationService,
            IReferenceDefinitionService referenceDefinitionService,
            IReferenceLookupCacheService referenceLookupCacheService,
            IContragentLookupService contragentLookupService)
        {
            _dataQueryService = dataQueryService;
            _modelMutationService = modelMutationService;
            _referenceDefinitionService = referenceDefinitionService;
            _referenceLookupCacheService = referenceLookupCacheService;
            _contragentLookupService = contragentLookupService;
        }

        public async Task<EmployeeEditWorkflowResult?> ShowAsync(
            EmployeeEditWorkflowRequest request,
            CancellationToken cancellationToken = default)
        {
            var definition = request.Definition ?? ResolveEmployeeReference();
            if (definition is null)
            {
                return null;
            }

            TableDataRow? sourceRow = null;
            if (!request.IsCreateMode && request.InitialState is null)
            {
                sourceRow = await LoadEmployeeEditRowAsync(request.EmployeeId, cancellationToken);
                if (sourceRow is null)
                {
                    throw new InvalidOperationException("Не удалось загрузить свежую карточку сотрудника.");
                }
            }

            var state = request.InitialState
                ?? EmployeeEditStateFactory.Create(definition, request.IsCreateMode, sourceRow);
            var viewModel = new EmployeeEditViewModel(state, LoadPositionOptionsAsync, _contragentLookupService.LoadOptionsAsync);
            var dialog = new EmployeeEditDialog(viewModel)
            {
                XamlRoot = request.XamlRoot
            };

            TableDataRow? savedRow = null;

            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    viewModel.ClearErrorInfo();

                    var payload = request.IsCreateMode
                        ? EmployeeEditPayloadBuilder.BuildForCreate(viewModel)
                        : EmployeeEditPayloadBuilder.BuildForUpdate(viewModel);

                    if (!request.IsCreateMode && payload.Count <= 1)
                    {
                        viewModel.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    savedRow = request.IsCreateMode
                        ? await _modelMutationService.CreateAsync(definition.Model, payload)
                        : await _modelMutationService.UpdateAsync(definition.Model, payload);
                }
                catch (Exception ex)
                {
                    viewModel.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };

            await dialog.ShowAsync();
            if (!dialog.WasSaved || savedRow is null)
            {
                return null;
            }

            _referenceLookupCacheService.Invalidate(definition.Model);
            return new EmployeeEditWorkflowResult
            {
                Definition = definition,
                SavedRow = savedRow
            };
        }

        private ReferenceDefinition? ResolveEmployeeReference()
        {
            return _referenceDefinitionService.TryGetByRoute("/employees", out var reference)
                ? reference
                : null;
        }

        private async Task<TableDataRow?> LoadEmployeeEditRowAsync(
            long? employeeId,
            CancellationToken cancellationToken)
        {
            if (employeeId is null)
            {
                return null;
            }

            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = "Employee",
                    Preset = "edit",
                    Filters = new Dictionary<string, object?>
                    {
                        ["id__eq"] = employeeId.Value
                    },
                    Limit = 1
                },
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadPositionOptionsAsync(
            string searchText,
            CancellationToken cancellationToken)
        {
            var normalizedSearchText = searchText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedSearchText))
            {
                return [];
            }

            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = "Position",
                    Preset = "item",
                    Filters = new Dictionary<string, object?>
                    {
                        ["name__cnt"] = normalizedSearchText
                    },
                    Sorts = ["name asc"],
                    Limit = 25
                },
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(static row => new CbsTableFilterOptionDefinition
                {
                    Value = row.GetValue("id"),
                    Label = row.GetValue("name")?.ToString() ?? string.Empty
                })
                .Where(static option => option.Value is not null && !string.IsNullOrWhiteSpace(option.Label))
                .DistinctBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

    }
}
