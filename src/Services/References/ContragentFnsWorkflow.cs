// Owns FNS compare/import/legal-entity workflows for Contragent edit scenarios.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.References;
using CbsContractsDesktopClient.ViewModels.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class ContragentFnsWorkflow : IContragentFnsWorkflow
    {
        private const string AddressModel = "Address";
        private readonly IDataQueryService _dataQueryService;
        private readonly IFnsContragentService _fnsContragentService;
        private readonly IModelMutationService _modelMutationService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;

        public ContragentFnsWorkflow(
            IDataQueryService dataQueryService,
            IFnsContragentService fnsContragentService,
            IModelMutationService modelMutationService,
            IReferenceLookupCacheService referenceLookupCacheService)
        {
            _dataQueryService = dataQueryService;
            _fnsContragentService = fnsContragentService;
            _modelMutationService = modelMutationService;
            _referenceLookupCacheService = referenceLookupCacheService;
        }

        public async Task<ContragentFnsWorkflowResult?> CompareSelectedAsync(
            ContragentFnsWorkflowRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.SelectedContragentId is null)
            {
                return null;
            }

            try
            {
                var sourceRow = await LoadContragentEditRowAsync(request.SelectedContragentId.Value, cancellationToken);
                if (sourceRow is null)
                {
                    await ShowErrorDialogAsync(
                        request.XamlRoot,
                        "Не удалось выполнить сверку",
                        "Не удалось загрузить свежую карточку контрагента.");
                    return null;
                }

                var state = await CreateEditStateAsync(request.Definition, isCreateMode: false, sourceRow, cancellationToken);
                if (string.IsNullOrWhiteSpace(state.Inn) || !IsValidInn(state.Inn))
                {
                    await ShowErrorDialogAsync(request.XamlRoot, "Сверка с ФНС", "Укажите корректный ИНН для сверки.");
                    return null;
                }

                var fnsResults = await _fnsContragentService.SearchByReqAsync(state.Inn.Trim(), state.Kpp);
                if (fnsResults.Count == 0)
                {
                    await ShowErrorDialogAsync(request.XamlRoot, "Сверка с ФНС", "Данные в ФНС не найдены.");
                    return null;
                }

                var remote = SelectFnsResult(fnsResults, state.Kpp);
                var editViewModel = new ContragentEditViewModel(state, LoadAddressOptionsAsync);
                var compareRows = BuildFnsCompareRows(editViewModel, remote);
                if (compareRows.Count == 0 || compareRows.All(static row => string.IsNullOrWhiteSpace(row.RemoteValue)))
                {
                    await ShowErrorDialogAsync(
                        request.XamlRoot,
                        "Сверка с ФНС",
                        "ФНС не вернула данные, которые можно применить к карточке.");
                    return null;
                }

                if (!await ShowFnsCompareDialogAsync(request.XamlRoot, compareRows))
                {
                    return null;
                }

                foreach (var row in compareRows.Where(static row => row.IsChecked))
                {
                    row.Apply(editViewModel);
                }

                await EnsureContragentAddressAsync(editViewModel, cancellationToken);
                if (!editViewModel.CanSubmit)
                {
                    await ShowErrorDialogAsync(
                        request.XamlRoot,
                        "Сверка с ФНС",
                        "Отмеченные строки не меняют карточку контрагента.");
                    return null;
                }

                var payload = ContragentEditPayloadBuilder.BuildForUpdate(editViewModel);
                if (payload.Count <= 1)
                {
                    await ShowErrorDialogAsync(
                        request.XamlRoot,
                        "Сверка с ФНС",
                        "Отмеченные строки не меняют карточку контрагента.");
                    return null;
                }

                var savedRow = await _modelMutationService.UpdateAsync(request.Definition.Model, payload, cancellationToken);
                _referenceLookupCacheService.Invalidate(request.Definition.Model);
                return CreateResult(request.Definition, isCreateMode: false, savedRow, "Данные обновлены");
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(request.XamlRoot, "Не удалось выполнить сверку с ФНС", ex.Message);
                return null;
            }
        }

        public async Task<ContragentFnsWorkflowResult?> ImportAsync(
            ContragentFnsWorkflowRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var selectedResult = await SelectFnsImportResultAsync(request.XamlRoot, cancellationToken);
                if (selectedResult is null)
                {
                    return null;
                }

                var state = await CreateContragentImportStateAsync(request.Definition, selectedResult, cancellationToken);
                return await ShowContragentEditDialogAsync(
                    request.XamlRoot,
                    request.Definition,
                    isCreateMode: true,
                    state,
                    isLegalEntityChangeMode: false,
                    "Контрагент создан",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(request.XamlRoot, "Не удалось импортировать данные из ФНС", ex.Message);
                return null;
            }
        }

        public async Task<ContragentFnsWorkflowResult?> ChangeLegalEntityFromFnsAsync(
            ContragentFnsWorkflowRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.SelectedContragentId is null)
            {
                return null;
            }

            try
            {
                var fnsResult = await SelectFnsImportResultAsync(request.XamlRoot, cancellationToken);
                if (fnsResult is null)
                {
                    return null;
                }

                var state = await CreateLegalEntityChangeStateAsync(
                    request.XamlRoot,
                    request.Definition,
                    request.SelectedContragentId.Value,
                    fnsResult,
                    cancellationToken);
                if (state is null)
                {
                    return null;
                }

                return await ShowContragentEditDialogAsync(
                    request.XamlRoot,
                    request.Definition,
                    isCreateMode: false,
                    state,
                    isLegalEntityChangeMode: true,
                    "Юр.лицо контрагента изменено",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(request.XamlRoot, "Не удалось сменить юр.лицо через ФНС", ex.Message);
                return null;
            }
        }

        public async Task<ContragentFnsWorkflowResult?> ChangeLegalEntityManuallyAsync(
            ContragentFnsWorkflowRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.SelectedContragentId is null)
            {
                return null;
            }

            var state = await CreateLegalEntityChangeStateAsync(
                request.XamlRoot,
                request.Definition,
                request.SelectedContragentId.Value,
                result: null,
                cancellationToken);
            if (state is null)
            {
                return null;
            }

            return await ShowContragentEditDialogAsync(
                request.XamlRoot,
                request.Definition,
                isCreateMode: false,
                state,
                isLegalEntityChangeMode: true,
                "Юр.лицо контрагента изменено",
                cancellationToken);
        }

        private async Task<ContragentFnsWorkflowResult?> ShowContragentEditDialogAsync(
            XamlRoot xamlRoot,
            ReferenceDefinition definition,
            bool isCreateMode,
            ContragentEditDialogState state,
            bool isLegalEntityChangeMode,
            string successTitle,
            CancellationToken cancellationToken)
        {
            var viewModel = new ContragentEditViewModel(state, LoadAddressOptionsAsync);
            var dialog = new ContragentEditDialog(viewModel)
            {
                XamlRoot = xamlRoot
            };

            TableDataRow? savedRow = null;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    viewModel.ClearErrorInfo();
                    await EnsureContragentAddressAsync(viewModel, cancellationToken);

                    var payload = isLegalEntityChangeMode
                        ? ContragentEditPayloadBuilder.BuildForLegalEntityChange(viewModel)
                        : isCreateMode
                            ? ContragentEditPayloadBuilder.BuildForCreate(viewModel)
                            : ContragentEditPayloadBuilder.BuildForUpdate(viewModel);

                    if (!isCreateMode && !isLegalEntityChangeMode && payload.Count <= 1)
                    {
                        viewModel.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    savedRow = isCreateMode
                        ? await _modelMutationService.CreateAsync(definition.Model, payload, cancellationToken)
                        : await _modelMutationService.UpdateAsync(definition.Model, payload, cancellationToken);
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
            return CreateResult(definition, isCreateMode, savedRow, successTitle);
        }

        private async Task<ContragentEditDialogState> CreateEditStateAsync(
            ReferenceDefinition definition,
            bool isCreateMode,
            TableDataRow? sourceRow,
            CancellationToken cancellationToken)
        {
            var ownershipOptions = await LoadSimpleReferenceOptionsAsync("Ownership", "card", cancellationToken);
            var regionOptions = await LoadSimpleReferenceOptionsAsync("Area", "item", cancellationToken);
            return ContragentEditStateFactory.Create(
                definition,
                isCreateMode,
                sourceRow,
                ownershipOptions,
                regionOptions);
        }

        private async Task<ContragentEditDialogState> CreateContragentImportStateAsync(
            ReferenceDefinition definition,
            FnsContragentLookupResult result,
            CancellationToken cancellationToken)
        {
            var ownershipOptions = await LoadSimpleReferenceOptionsAsync("Ownership", "card", cancellationToken);
            var regionOptions = await LoadSimpleReferenceOptionsAsync("Area", "item", cancellationToken);

            return new ContragentEditDialogState
            {
                Definition = definition,
                IsCreateMode = true,
                ObjUuid = string.IsNullOrWhiteSpace(result.ObjUuid) ? Guid.NewGuid().ToString() : result.ObjUuid,
                RequisitesListKey = result.RequisitesListKey,
                Inn = NormalizeSingleLine(result.Organization.Inn),
                Kpp = NormalizeSingleLine(result.Organization.Kpp),
                OwnershipId = result.Organization.OwnershipId,
                Name = NormalizeSingleLine(result.Organization.Name),
                FullName = NormalizeSingleLine(result.Organization.FullName),
                RegionId = result.Region?.Id ?? result.RealAddress.AreaId,
                RegionName = result.Region?.Name ?? string.Empty,
                AddressReal = NormalizeSingleLine(result.RealAddress.Value),
                Description = NormalizeSingleLine(result.Description),
                Ogrn = NormalizeSingleLine(result.Organization.Ogrn),
                Okfc = NormalizeSingleLine(result.Organization.Okfc),
                Okopf = NormalizeSingleLine(result.Organization.Okopf),
                Okpo = NormalizeSingleLine(result.Organization.Okpo),
                Okogu = NormalizeSingleLine(result.Organization.Okogu),
                Oktmo = NormalizeSingleLine(result.Organization.Oktmo),
                Contacts = result.Contacts
                    .Where(static contact => !string.IsNullOrWhiteSpace(contact.Value))
                    .Select(static contact => new EmployeeContactEditItem
                    {
                        Value = NormalizeSingleLine(contact.Value),
                        Type = NormalizeSingleLine(contact.Type)
                    })
                    .ToList(),
                ContactsText = string.Join(
                    Environment.NewLine,
                    result.Contacts
                        .Select(static contact => NormalizeSingleLine(contact.Value))
                        .Where(static value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.CurrentCultureIgnoreCase)),
                OwnershipOptions = ownershipOptions,
                RegionOptions = regionOptions
            };
        }

        private async Task<ContragentEditDialogState?> CreateLegalEntityChangeStateAsync(
            XamlRoot xamlRoot,
            ReferenceDefinition definition,
            long contragentId,
            FnsContragentLookupResult? result,
            CancellationToken cancellationToken)
        {
            var sourceRow = await LoadContragentEditRowAsync(contragentId, cancellationToken);
            if (sourceRow is null)
            {
                await ShowErrorDialogAsync(
                    xamlRoot,
                    "Смена юр.лица",
                    "Не удалось загрузить свежую карточку контрагента.");
                return null;
            }

            var currentState = await CreateEditStateAsync(definition, isCreateMode: false, sourceRow, cancellationToken);
            var newRegistration = CreateNewLegalEntityRegistration(result);
            var organizationHistory = currentState.OrganizationHistory
                .Select(static registration => CopyRegistrationForLegalEntityChange(registration))
                .Prepend(newRegistration)
                .ToList();

            return new ContragentEditDialogState
            {
                Definition = currentState.Definition,
                IsCreateMode = false,
                Id = currentState.Id,
                ObjUuid = currentState.ObjUuid,
                RequisitesId = currentState.RequisitesId,
                RequisitesListKey = currentState.RequisitesListKey,
                OrganizationId = currentState.OrganizationId,
                Inn = newRegistration.Inn,
                Kpp = newRegistration.Kpp,
                Division = newRegistration.Division,
                OwnershipId = newRegistration.OwnershipId,
                OwnershipName = newRegistration.OwnershipName,
                Name = newRegistration.Name,
                RegionId = currentState.RegionId,
                RegionName = currentState.RegionName,
                RealAddressId = currentState.RealAddressId,
                RealAddressListKey = currentState.RealAddressListKey,
                AddressRealAddressId = currentState.AddressRealAddressId,
                AddressReal = currentState.AddressReal,
                FullName = newRegistration.FullName,
                Description = currentState.Description,
                Ogrn = newRegistration.Ogrn,
                Okfc = newRegistration.Okfc,
                Okopf = newRegistration.Okopf,
                Okpo = newRegistration.Okpo,
                Okogu = newRegistration.Okogu,
                Okved = newRegistration.Okved,
                Oktmo = newRegistration.Oktmo,
                BankName = currentState.BankName,
                BankBik = currentState.BankBik,
                BankAccount = currentState.BankAccount,
                BankCorAccount = currentState.BankCorAccount,
                Contacts = currentState.Contacts,
                ContactsText = currentState.ContactsText,
                OrganizationHistory = organizationHistory,
                OwnershipOptions = currentState.OwnershipOptions,
                RegionOptions = currentState.RegionOptions,
                InitialAddressOption = currentState.InitialAddressOption
            };
        }

        private async Task<FnsContragentLookupResult?> SelectFnsImportResultAsync(
            XamlRoot xamlRoot,
            CancellationToken cancellationToken)
        {
            var criteria = await ShowFnsImportCriteriaDialogAsync(xamlRoot);
            if (criteria is null)
            {
                return null;
            }

            if (!IsValidInn(criteria.Inn))
            {
                await ShowErrorDialogAsync(xamlRoot, "Импорт из ФНС", "Укажите корректный ИНН.");
                return null;
            }

            var results = await _fnsContragentService.SearchByReqAsync(criteria.Inn);
            if (results.Count == 0)
            {
                await ShowErrorDialogAsync(xamlRoot, "Импорт из ФНС", "Данные в ФНС не найдены.");
                return null;
            }

            var filteredResults = FilterFnsImportResults(results, criteria);
            if (filteredResults.Count == 0)
            {
                await ShowErrorDialogAsync(
                    xamlRoot,
                    "Импорт из ФНС",
                    "По указанным КПП или наименованию данные не найдены.");
                return null;
            }

            return filteredResults.Count == 1
                ? filteredResults[0]
                : await ShowFnsImportResultSelectionDialogAsync(xamlRoot, filteredResults);
        }

        private async Task<FnsImportCriteria?> ShowFnsImportCriteriaDialogAsync(XamlRoot xamlRoot)
        {
            var innBox = new TextBox
            {
                Header = "ИНН",
                PlaceholderText = "Введите ИНН",
                MinWidth = 360
            };
            var kppBox = new TextBox
            {
                Header = "КПП",
                PlaceholderText = "Необязательно",
                MinWidth = 360
            };
            var nameBox = new TextBox
            {
                Header = "Наименование",
                PlaceholderText = "Короткое или полное имя",
                MinWidth = 360
            };
            var content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    innBox,
                    kppBox,
                    nameBox
                }
            };

            var submitted = false;
            ContentDialog? dialog = null;
            dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "Импорт из ФНС",
                PrimaryButtonText = string.Empty,
                CloseButtonText = string.Empty,
                DefaultButton = ContentDialogButton.None,
                Content = BuildDialogContentWithFooter(
                    content,
                    BuildFooterButton("Найти", "\uE721", true, () =>
                    {
                        submitted = true;
                        dialog!.Hide();
                    }),
                    BuildFooterButton("Отмена", "\uE711", false, () => dialog!.Hide()))
            };
            DialogChrome.Apply(dialog);

            await dialog.ShowAsync();
            if (!submitted)
            {
                return null;
            }

            var inn = NormalizeSingleLine(innBox.Text);
            if (string.IsNullOrWhiteSpace(inn))
            {
                return null;
            }

            return new FnsImportCriteria(
                inn,
                NormalizeSingleLine(kppBox.Text),
                NormalizeSingleLine(nameBox.Text));
        }

        private async Task<FnsContragentLookupResult?> ShowFnsImportResultSelectionDialogAsync(
            XamlRoot xamlRoot,
            IReadOnlyList<FnsContragentLookupResult> results)
        {
            var items = results
                .Select(static result => new FnsImportSelectionItem(BuildFnsImportSelectionLabel(result), result))
                .ToList();
            var comboBox = new ComboBox
            {
                Header = "КПП / подразделение",
                ItemsSource = items,
                DisplayMemberPath = nameof(FnsImportSelectionItem.Label),
                SelectedIndex = 0,
                MinWidth = 680
            };

            var submitted = false;
            ContentDialog? dialog = null;
            dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = "Выберите регистрацию",
                PrimaryButtonText = string.Empty,
                CloseButtonText = string.Empty,
                DefaultButton = ContentDialogButton.None,
                Content = BuildDialogContentWithFooter(
                    comboBox,
                    BuildFooterButton("Продолжить", "\uE73E", true, () =>
                    {
                        submitted = true;
                        dialog!.Hide();
                    }),
                    BuildFooterButton("Отмена", "\uE711", false, () => dialog!.Hide()))
            };
            dialog.Resources["ContentDialogMinWidth"] = 760d;
            DialogChrome.Apply(dialog);

            await dialog.ShowAsync();
            return submitted
                ? (comboBox.SelectedItem as FnsImportSelectionItem)?.Result
                : null;
        }

        private async Task<bool> ShowFnsCompareDialogAsync(
            XamlRoot xamlRoot,
            IReadOnlyList<FnsCompareRow> compareRows)
        {
            var dialog = new FnsCompareDialog(compareRows)
            {
                XamlRoot = xamlRoot
            };

            await dialog.ShowAsync();
            return dialog.WasSaved;
        }

        private async Task<TableDataRow?> LoadContragentEditRowAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = "Contragent",
                    Preset = "edit",
                    Filters = new Dictionary<string, object?>
                    {
                        ["id__eq"] = id
                    },
                    Limit = 1
                },
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadSimpleReferenceOptionsAsync(
            string model,
            string preset,
            CancellationToken cancellationToken = default)
        {
            if (string.Equals(model, "Ownership", StringComparison.OrdinalIgnoreCase))
            {
                var items = await _referenceLookupCacheService.GetItemsAsync(model, preset, cancellationToken);
                return items
                    .Select(static item => new CbsTableFilterOptionDefinition
                    {
                        Value = item.Id,
                        Label = BuildOwnershipOptionLabel(item)
                    })
                    .Where(static option => option.Value is not null && !string.IsNullOrWhiteSpace(option.Label))
                    .DistinctBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            return await _referenceLookupCacheService.GetOptionsAsync(model, preset, cancellationToken);
        }

        private async Task EnsureContragentAddressAsync(
            ContragentEditViewModel viewModel,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(viewModel.AddressReal))
            {
                viewModel.SelectAddressOption(null);
                return;
            }

            if (viewModel.SelectedAddressId is not null)
            {
                return;
            }

            var existingAddress = await FindAddressOptionByValueAsync(viewModel.AddressReal, cancellationToken);
            if (existingAddress is not null)
            {
                viewModel.SelectAddressOption(existingAddress);
                return;
            }

            var createdAddress = await _modelMutationService.CreateAsync(
                AddressModel,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["value"] = viewModel.AddressReal.Trim(),
                    ["area_id"] = viewModel.SelectedRegionId
                },
                cancellationToken);

            var createdId = TryGetRowId(createdAddress);
            if (createdId is null)
            {
                throw new InvalidOperationException("Не удалось получить ID созданного адреса.");
            }

            viewModel.SelectAddressOption(new CbsTableFilterOptionDefinition
            {
                Value = createdId.Value,
                Label = viewModel.AddressReal.Trim()
            });
        }

        private async Task<CbsTableFilterOptionDefinition?> FindAddressOptionByValueAsync(
            string value,
            CancellationToken cancellationToken = default)
        {
            var normalizedValue = value.Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                return null;
            }

            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = AddressModel,
                    Preset = "item",
                    Filters = new Dictionary<string, object?>
                    {
                        ["value__eq"] = normalizedValue
                    },
                    Limit = 1
                },
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(ToAddressOption)
                .FirstOrDefault(static option => option is not null);
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadAddressOptionsAsync(
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
                    Model = AddressModel,
                    Preset = "item",
                    Filters = new Dictionary<string, object?>
                    {
                        ["value__cnt"] = normalizedSearchText
                    },
                    Sorts = ["value asc"],
                    Limit = 25
                },
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(ToAddressOption)
                .Where(static option => option is not null)
                .Select(static option => option!)
                .DistinctBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static IReadOnlyList<FnsContragentLookupResult> FilterFnsImportResults(
            IReadOnlyList<FnsContragentLookupResult> results,
            FnsImportCriteria criteria)
        {
            if (string.IsNullOrWhiteSpace(criteria.Kpp) && string.IsNullOrWhiteSpace(criteria.Name))
            {
                return results;
            }

            return results
                .Where(result =>
                    MatchesFnsImportKpp(result, criteria.Kpp)
                    || MatchesFnsImportName(result, criteria.Name))
                .ToList();
        }

        private static bool MatchesFnsImportKpp(FnsContragentLookupResult result, string kpp)
        {
            return !string.IsNullOrWhiteSpace(kpp)
                && string.Equals(
                    NormalizeSingleLine(result.Organization.Kpp),
                    kpp,
                    StringComparison.CurrentCultureIgnoreCase);
        }

        private static bool MatchesFnsImportName(FnsContragentLookupResult result, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var fullName = NormalizeSingleLine(result.Organization.FullName);
            var shortName = NormalizeSingleLine(result.Organization.Name);
            return fullName.Contains(name, StringComparison.CurrentCultureIgnoreCase)
                || shortName.Contains(name, StringComparison.CurrentCultureIgnoreCase);
        }

        private static FnsContragentLookupResult SelectFnsResult(
            IReadOnlyList<FnsContragentLookupResult> results,
            string? kpp)
        {
            if (!string.IsNullOrWhiteSpace(kpp))
            {
                var byKpp = results.FirstOrDefault(result =>
                    string.Equals(result.Organization.Kpp, kpp.Trim(), StringComparison.OrdinalIgnoreCase));

                if (byKpp is not null)
                {
                    return byKpp;
                }
            }

            return results[0];
        }

        private static ContragentOrganizationHistoryItem CreateNewLegalEntityRegistration(
            FnsContragentLookupResult? result)
        {
            return new ContragentOrganizationHistoryItem
            {
                ListKey = Guid.NewGuid().ToString(),
                IsActive = true,
                OriginalIsActive = false,
                Name = NormalizeSingleLine(result?.Organization.Name),
                FullName = NormalizeSingleLine(result?.Organization.FullName),
                Inn = NormalizeSingleLine(result?.Organization.Inn),
                Kpp = NormalizeSingleLine(result?.Organization.Kpp),
                OwnershipId = result?.Organization.OwnershipId,
                Ogrn = NormalizeSingleLine(result?.Organization.Ogrn),
                Okfc = NormalizeSingleLine(result?.Organization.Okfc),
                Okopf = NormalizeSingleLine(result?.Organization.Okopf),
                Okpo = NormalizeSingleLine(result?.Organization.Okpo),
                Okogu = NormalizeSingleLine(result?.Organization.Okogu),
                Oktmo = NormalizeSingleLine(result?.Organization.Oktmo)
            };
        }

        private static ContragentOrganizationHistoryItem CopyRegistrationForLegalEntityChange(
            ContragentOrganizationHistoryItem registration)
        {
            return new ContragentOrganizationHistoryItem
            {
                Id = registration.Id,
                OrganizationId = registration.OrganizationId,
                Name = registration.Name,
                FullName = registration.FullName,
                Inn = registration.Inn,
                Kpp = registration.Kpp,
                Division = registration.Division,
                OwnershipName = registration.OwnershipName,
                OwnershipId = registration.OwnershipId,
                OwnershipCode = registration.OwnershipCode,
                Ogrn = registration.Ogrn,
                Okfc = registration.Okfc,
                Okopf = registration.Okopf,
                Okpo = registration.Okpo,
                Okogu = registration.Okogu,
                Okved = registration.Okved,
                Oktmo = registration.Oktmo,
                ListKey = registration.ListKey,
                CreatedAt = registration.CreatedAt,
                UpdatedAt = registration.UpdatedAt,
                OriginalIsActive = registration.OriginalIsActive,
                IsActive = false,
                IsMarkedForDestroy = registration.IsMarkedForDestroy
            };
        }

        private static IReadOnlyList<FnsCompareRow> BuildFnsCompareRows(
            ContragentEditViewModel local,
            FnsContragentLookupResult remote)
        {
            var remoteContacts = remote.Contacts
                .Where(static contact => !string.IsNullOrWhiteSpace(contact.Value) && !string.IsNullOrWhiteSpace(contact.Type))
                .ToList();
            var localContactValues = SplitContactText(local.ContactsText).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            var newRemoteContacts = remoteContacts
                .Where(contact => !localContactValues.Contains(contact.Value.Trim()))
                .ToList();

            return
            [
                TextRow("name", "Наименование", local.Name, remote.Organization.Name, (viewModel, value) => viewModel.Name = value),
                TextRow("full_name", "Полное наименование", local.FullName, remote.Organization.FullName, (viewModel, value) => viewModel.FullName = value),
                TextRow("inn", "ИНН", local.Inn, remote.Organization.Inn, (viewModel, value) => viewModel.Inn = value),
                TextRow("kpp", "КПП", local.Kpp, remote.Organization.Kpp, (viewModel, value) => viewModel.Kpp = value),
                TextRow("ogrn", "ОГРН", local.Ogrn, remote.Organization.Ogrn, (viewModel, value) => viewModel.Ogrn = value),
                TextRow("okopf", "ОКОПФ", local.Okopf, remote.Organization.Okopf, (viewModel, value) => viewModel.Okopf = value),
                TextRow("okpo", "ОКПО", local.Okpo, remote.Organization.Okpo, (viewModel, value) => viewModel.Okpo = value),
                TextRow("okogu", "ОКОГУ", local.Okogu, remote.Organization.Okogu, (viewModel, value) => viewModel.Okogu = value),
                TextRow("okfc", "ОКФС", local.Okfc, remote.Organization.Okfc, (viewModel, value) => viewModel.Okfc = value),
                TextRow("oktmo", "ОКТМО", local.Oktmo, remote.Organization.Oktmo, (viewModel, value) => viewModel.Oktmo = value),
                LookupRow(
                    "ownership",
                    "Форма собственности",
                    GetOptionLabel(local.OwnershipOptions, local.SelectedOwnershipId),
                    GetOptionLabel(local.OwnershipOptions, remote.Organization.OwnershipId) ?? remote.Organization.OwnershipOkopf,
                    remote.Organization.OwnershipId,
                    (viewModel, value) => viewModel.SelectedOwnershipId = value),
                LookupRow(
                    "region",
                    "Регион",
                    GetOptionLabel(local.RegionOptions, local.SelectedRegionId),
                    GetOptionLabel(local.RegionOptions, remote.Region?.Id) ?? remote.Region?.Name,
                    remote.Region?.Id,
                    (viewModel, value) => viewModel.SelectedRegionId = value),
                TextRow("address", "Адрес", local.AddressReal, remote.RealAddress.Value, (viewModel, value) => viewModel.CommitAddressInput(value)),
                new FnsCompareRow
                {
                    Key = "contacts",
                    Label = "Контакты",
                    LocalValue = string.Join(", ", SplitContactText(local.ContactsText)),
                    RemoteValue = string.Join(", ", remoteContacts.Select(static contact => contact.Value)),
                    CanApply = newRemoteContacts.Count > 0,
                    IsChecked = newRemoteContacts.Count > 0,
                    Apply = viewModel =>
                    {
                        if (newRemoteContacts.Count == 0)
                        {
                            return;
                        }

                        var values = SplitContactText(viewModel.ContactsText).ToList();
                        values.AddRange(newRemoteContacts.Select(static contact => contact.Value.Trim()));
                        viewModel.ContactsText = string.Join(Environment.NewLine, values.Distinct(StringComparer.CurrentCultureIgnoreCase));
                    }
                }
            ];
        }

        private static FnsCompareRow TextRow(
            string key,
            string label,
            string localValue,
            string? remoteValue,
            Action<ContragentEditViewModel, string> apply)
        {
            var normalizedRemote = remoteValue?.Trim() ?? string.Empty;
            return new FnsCompareRow
            {
                Key = key,
                Label = label,
                LocalValue = localValue.Trim(),
                RemoteValue = normalizedRemote,
                CanApply = !string.IsNullOrWhiteSpace(normalizedRemote),
                IsChecked = !string.IsNullOrWhiteSpace(normalizedRemote)
                    && !string.Equals(localValue.Trim(), normalizedRemote, StringComparison.CurrentCulture),
                Apply = viewModel => apply(viewModel, normalizedRemote)
            };
        }

        private static FnsCompareRow LookupRow(
            string key,
            string label,
            string? localValue,
            string? remoteValue,
            long? remoteId,
            Action<ContragentEditViewModel, long?> apply)
        {
            return new FnsCompareRow
            {
                Key = key,
                Label = label,
                LocalValue = localValue?.Trim() ?? string.Empty,
                RemoteValue = remoteValue?.Trim() ?? string.Empty,
                CanApply = remoteId is not null,
                IsChecked = remoteId is not null && !string.Equals(localValue?.Trim(), remoteValue?.Trim(), StringComparison.CurrentCulture),
                Apply = viewModel => apply(viewModel, remoteId)
            };
        }

        private static FrameworkElement BuildFnsCompareDialogContent(IReadOnlyList<FnsCompareRow> rows)
        {
            var root = new StackPanel
            {
                Spacing = 0,
                Width = 1180
            };

            root.Children.Add(BuildFnsCompareHeaderRow());

            for (var index = 0; index < rows.Count; index++)
            {
                root.Children.Add(BuildFnsCompareValueRow(rows[index], index));
            }

            return new ScrollViewer
            {
                MaxHeight = 580,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollMode = ScrollMode.Enabled,
                Content = root
            };
        }

        private static Grid BuildFnsCompareHeaderRow()
        {
            var grid = CreateFnsCompareGrid();
            grid.Padding = new Thickness(0, 0, 0, 6);
            grid.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellMutedPanelBackgroundBrush"];

            AddCompareText(grid, "Поле", 0, isHeader: true);
            AddCompareText(grid, "Локально", 1, isHeader: true);
            AddCompareText(grid, "ФНС", 2, isHeader: true);
            AddCompareText(grid, "Обн", 3, isHeader: true, horizontalAlignment: HorizontalAlignment.Center);
            return grid;
        }

        private static Grid BuildFnsCompareValueRow(FnsCompareRow row, int index)
        {
            var grid = CreateFnsCompareGrid();
            grid.Padding = new Thickness(0, 4, 0, 4);
            grid.Background = index % 2 == 1
                ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellMutedPanelBackgroundBrush"]
                : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellPanelBackgroundBrush"];

            AddCompareText(grid, row.Label, 0);
            AddCompareText(grid, string.IsNullOrWhiteSpace(row.LocalValue) ? "-" : row.LocalValue, 1);
            AddCompareText(grid, string.IsNullOrWhiteSpace(row.RemoteValue) ? "-" : row.RemoteValue, 2);

            var checkBox = new CheckBox
            {
                Content = null,
                IsChecked = row.IsChecked,
                IsEnabled = row.CanApply,
                MinWidth = 0,
                Width = 20,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = row
            };
            checkBox.Checked += FnsCompareCheckBoxChanged;
            checkBox.Unchecked += FnsCompareCheckBoxChanged;

            var checkBoxHost = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            checkBoxHost.Children.Add(checkBox);
            Grid.SetColumn(checkBoxHost, 3);
            grid.Children.Add(checkBoxHost);
            return grid;
        }

        private static Grid CreateFnsCompareGrid()
        {
            var grid = new Grid
            {
                ColumnSpacing = 10,
                Width = 1180
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            return grid;
        }

        private static void AddCompareText(
            Grid grid,
            string text,
            int column,
            bool isHeader = false,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.WrapWholeWords,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = horizontalAlignment,
                FontWeight = isHeader ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
            };

            Grid.SetColumn(textBlock, column);
            grid.Children.Add(textBlock);
        }

        private static void FnsCompareCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { Tag: FnsCompareRow row } checkBox)
            {
                row.IsChecked = checkBox.IsChecked == true;
            }
        }

        private static IReadOnlyList<string> SplitContactText(string contactsText)
        {
            return contactsText
                .Split([Environment.NewLine, "\n", ";", ","], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .ToList();
        }

        private static CbsTableFilterOptionDefinition? ToAddressOption(TableDataRow row)
        {
            var id = row.GetValue("id");
            var label = row.GetValue("value")?.ToString();
            return id is null || string.IsNullOrWhiteSpace(label)
                ? null
                : new CbsTableFilterOptionDefinition
                {
                    Value = id,
                    Label = label
                };
        }

        private static string BuildOwnershipOptionLabel(ReferenceLookupItem item)
        {
            if (string.IsNullOrWhiteSpace(item.FullName)
                || string.Equals(item.Name, item.FullName, StringComparison.CurrentCultureIgnoreCase))
            {
                return item.DisplayName;
            }

            if (string.IsNullOrWhiteSpace(item.Name))
            {
                return item.FullName;
            }

            return $"{item.Name} - {item.FullName}";
        }

        private static string BuildFnsImportSelectionLabel(FnsContragentLookupResult result)
        {
            var requisites = string.Join(
                " / ",
                new[] { result.Organization.Inn, result.Organization.Kpp }
                    .Select(NormalizeSingleLine)
                    .Where(static value => !string.IsNullOrWhiteSpace(value)));
            var name = NormalizeSingleLine(result.Organization.Name);
            var address = NormalizeSingleLine(result.RealAddress.Value);

            return string.Join(
                " · ",
                new[] { requisites, name, address }
                    .Where(static value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string? GetOptionLabel(IReadOnlyList<CbsTableFilterOptionDefinition> options, object? value)
        {
            if (value is null)
            {
                return null;
            }

            var normalizedValue = value.ToString();
            return options.FirstOrDefault(option =>
                string.Equals(option.Value?.ToString(), normalizedValue, StringComparison.OrdinalIgnoreCase))?.Label;
        }

        private static string NormalizeSingleLine(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool IsValidInn(string value)
        {
            var text = value.Trim();
            return (text.Length == 10 || text.Length == 12)
                && text.All(char.IsDigit);
        }

        private static long? TryGetRowId(TableDataRow row)
        {
            var rawId = row.GetValue("id");
            return rawId switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string stringValue when long.TryParse(stringValue, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static ContragentFnsWorkflowResult CreateResult(
            ReferenceDefinition definition,
            bool isCreateMode,
            TableDataRow savedRow,
            string successTitle)
        {
            return new ContragentFnsWorkflowResult
            {
                Definition = definition,
                IsCreateMode = isCreateMode,
                SavedRow = savedRow,
                SuccessTitle = successTitle
            };
        }

        private static async Task ShowErrorDialogAsync(XamlRoot xamlRoot, string title, string message)
        {
            ContentDialog? dialog = null;
            dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = title,
                Content = BuildDialogContentWithFooter(
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    BuildFooterButton("ОК", "\uE73E", true, () => dialog!.Hide())),
                CloseButtonText = string.Empty,
                DefaultButton = ContentDialogButton.None
            };
            DialogChrome.Apply(dialog);

            await dialog.ShowAsync();
        }

        private static FrameworkElement BuildDialogContentWithFooter(UIElement body, params Button[] buttons)
        {
            var root = new Grid
            {
                RowSpacing = 10
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Children.Add(body);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 4
            };
            foreach (var button in buttons)
            {
                footer.Children.Add(button);
            }

            Grid.SetRow(footer, 1);
            root.Children.Add(footer);
            return root;
        }

        private static Button BuildFooterButton(string text, string glyph, bool isPrimary, Action click)
        {
            var foreground = Application.Current.Resources[
                    isPrimary ? "ShellTableRowSelectedBorderBrush" : "ShellSecondaryTextBrush"] as Brush
                ?? new SolidColorBrush(isPrimary ? Microsoft.UI.Colors.SeaGreen : Microsoft.UI.Colors.DimGray);

            var button = new Button
            {
                Width = 112,
                MinHeight = 28,
                Padding = new Thickness(8, 2, 8, 2),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Foreground = foreground,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new FontIcon
                        {
                            Glyph = glyph,
                            FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                            FontSize = 12,
                            Foreground = foreground
                        },
                        new TextBlock
                        {
                            Text = text,
                            Foreground = foreground,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 232, 235, 239));
            button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 225, 231));
            button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 232, 235, 239));
            button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 225, 231));
            button.Click += (_, _) => click();
            return button;
        }

        private sealed record FnsImportCriteria(string Inn, string Kpp, string Name);

        private sealed record FnsImportSelectionItem(string Label, FnsContragentLookupResult Result);

        private sealed class FnsCompareDialog : AppEditDialog
        {
            private readonly IReadOnlyList<FnsCompareRow> _rows;

            public FnsCompareDialog(IReadOnlyList<FnsCompareRow> rows)
            {
                _rows = rows;
                FullSizeDesired = false;
                HorizontalAlignment = HorizontalAlignment.Center;
                Title = "Сверка с ФНС";
                Resources["ContentDialogMinWidth"] = 650d;
                Resources["ContentDialogMinHeight"] = 600d;
                Resources["ContentDialogMaxWidth"] = 1280d;
                Content = BuildEditContent(BuildFnsCompareDialogContent(rows));
                DialogChrome.Apply(this);
            }

            public override bool Validate()
            {
                if (_rows.Any(static row => row.IsChecked && row.CanApply))
                {
                    return true;
                }

                ShowErrorInfo("Выберите данные ФНС, которые нужно применить.");
                return false;
            }
        }

        private sealed class FnsCompareRow
        {
            public required string Key { get; init; }

            public required string Label { get; init; }

            public string LocalValue { get; init; } = string.Empty;

            public string RemoteValue { get; init; } = string.Empty;

            public bool CanApply { get; init; }

            public bool IsChecked { get; set; }

            public required Action<ContragentEditViewModel> Apply { get; init; }
        }
    }
}
