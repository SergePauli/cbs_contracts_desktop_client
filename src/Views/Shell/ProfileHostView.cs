// Hosts the Profile/User table and its profile-specific edit dialog workflow.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.References;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed class ProfileHostView : ComplexHostViewBase
    {
        private readonly IDataQueryService _dataQueryService;
        private readonly IModelMutationService _modelMutationService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private Button? _createButton;
        private Button? _editButton;
        private Button? _deleteButton;

        public ProfileHostView()
        {
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _modelMutationService = App.Services.GetRequiredService<IModelMutationService>();
            _referenceDefinitionService = App.Services.GetRequiredService<IReferenceDefinitionService>();
            _referenceLookupCacheService = App.Services.GetRequiredService<IReferenceLookupCacheService>();
        }

        protected override IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            _editButton = CreateHeaderIconButton("\uE70F", "Редактировать пользователя");
            _editButton.Click += async (_, _) => await ShowProfileEditDialogAsync(isCreateMode: false);

            _deleteButton = CreateHeaderIconButton("\uE74D", "Удалить пользователя");
            _deleteButton.Click += async (_, _) => await DeleteSelectedProfileAsync();

            _createButton = CreateHeaderIconButton("\uF8AA", "Добавить пользователя");
            _createButton.Click += async (_, _) => await ShowProfileEditDialogAsync(isCreateMode: true);

            UpdateActionButtonState();
            return [_editButton, _deleteButton, _createButton];
        }

        protected override Task OnRouteLoaded(TablePageDefinition definition)
        {
            UpdateActionButtonState();
            return Task.CompletedTask;
        }

        protected override Task OnRowSelected(TableDataRow? row)
        {
            UpdateActionButtonState();
            return Task.CompletedTask;
        }

        protected override async Task OpenEditDialogAsync(TableDataRow? row)
        {
            if (row is not null)
            {
                await ShowProfileEditDialogAsync(isCreateMode: false);
            }
        }

        protected override string BuildSelectedFooterText(TableDataRow row)
        {
            var login =
                row.GetValue("user.name")?.ToString()
                ?? row.GetValue("name")?.ToString();
            var fio =
                row.GetValue("user.person.full_name")?.ToString()
                ?? row.GetValue("user.person.person_name.naming.fio")?.ToString()
                ?? row.GetValue("full_name")?.ToString()
                ?? row.GetValue("person")?.ToString();

            if (!string.IsNullOrWhiteSpace(login) && !string.IsNullOrWhiteSpace(fio))
            {
                return $"{login} - {fio}";
            }

            if (!string.IsNullOrWhiteSpace(login))
            {
                return login;
            }

            return fio ?? string.Empty;
        }

        private void UpdateActionButtonState()
        {
            var hasSelectedRow = Store.HasSelectedRow;

            if (_editButton is not null)
            {
                _editButton.IsEnabled = hasSelectedRow && Store.CanEditRows;
            }

            if (_deleteButton is not null)
            {
                _deleteButton.IsEnabled = hasSelectedRow && Store.CanDeleteRows;
            }

            if (_createButton is not null)
            {
                _createButton.IsEnabled = Store.CanCreateRows;
            }
        }

        private async Task ShowProfileEditDialogAsync(bool isCreateMode)
        {
            var reference = ResolveProfileReference();
            if (reference is null)
            {
                return;
            }

            if (!isCreateMode && Store.SelectedRow is null)
            {
                return;
            }

            var state = CreateProfileEditDialogState(reference, isCreateMode);
            var viewModel = new ProfileEditViewModel(state, LoadPositionOptionsAsync);
            var dialog = new ProfileEditDialog(viewModel)
            {
                XamlRoot = XamlRoot
            };

            TableDataRow? savedRow = null;
            IReadOnlyDictionary<string, object?>? savedPayload = null;

            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    viewModel.ClearErrorInfo();

                    var payload = isCreateMode
                        ? ProfileEditPayloadBuilder.BuildForCreate(viewModel)
                        : ProfileEditPayloadBuilder.BuildForUpdate(viewModel);
                    savedPayload = payload;

                    if (!isCreateMode && payload.Count <= 1)
                    {
                        viewModel.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    savedRow = isCreateMode
                        ? await _modelMutationService.CreateAsync(reference.Model, payload)
                        : await _modelMutationService.UpdateAsync(reference.Model, payload);
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
                return;
            }

            _referenceLookupCacheService.Invalidate(reference.Model);
            await RefreshAfterSaveAsync(isCreateMode, savedRow, savedPayload);
            ShowSuccessNotification(
                isCreateMode ? "Пользователь создан" : "Изменения сохранены",
                BuildReferenceNotificationMessage(reference.Title, TryGetSelectedRowId(savedRow)));
        }

        private ProfileEditDialogState CreateProfileEditDialogState(
            ReferenceDefinition reference,
            bool isCreateMode)
        {
            var departmentOptions = Store.CurrentFilterOptionsSources.TryGetValue("Department", out var options)
                ? options
                : [];

            return ProfileEditStateFactory.Create(
                reference,
                isCreateMode,
                isCreateMode ? null : Store.SelectedRow,
                departmentOptions);
        }

        private async Task DeleteSelectedProfileAsync()
        {
            var reference = ResolveProfileReference();
            if (reference is null || Store.SelectedRow is null)
            {
                return;
            }

            var id = TryGetSelectedRowId(Store.SelectedRow);
            if (id is null)
            {
                await ShowErrorDialogAsync(
                    "Не удалось удалить пользователя.",
                    "У выбранной записи отсутствует корректный ID.");
                return;
            }

            if (!await ConfirmDialogAsync(
                    "Удаление пользователя",
                    "Удалить выбранного пользователя?",
                    "Удалить",
                    defaultButton: ContentDialogButton.Close))
            {
                return;
            }

            try
            {
                await _modelMutationService.DeleteAsync(reference.Model, id.Value);
                _referenceLookupCacheService.Invalidate(reference.Model);
                await Store.ReloadCurrentReferenceAsync();
                ShowSuccessNotification(
                    "Пользователь удален",
                    BuildReferenceNotificationMessage(reference.Title, id.Value));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось удалить пользователя.", ex.Message);
            }
        }

        private async Task RefreshAfterSaveAsync(
            bool isCreateMode,
            TableDataRow savedRow,
            IReadOnlyDictionary<string, object?>? payload)
        {
            if (isCreateMode
                || payload is null
                || !Store.ApplySavedRowUpdate(savedRow, payload))
            {
                await Store.ReloadCurrentReferenceAsync();
            }
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

        private ReferenceDefinition? ResolveProfileReference()
        {
            return _referenceDefinitionService.TryGetByRoute("/users", out var reference)
                ? reference
                : null;
        }

        private static string BuildReferenceNotificationMessage(string title, long? id)
        {
            return id.HasValue
                ? $"{title}, ID {id.Value}"
                : title;
        }
    }
}
