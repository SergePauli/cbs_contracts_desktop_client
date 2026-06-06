// Hosts the Employee table and delegates employee editing to the shared workflow.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed class EmployeeHostView : ComplexHostViewBase
    {
        private readonly IEmployeeEditWorkflow _employeeEditWorkflow;
        private readonly IModelMutationService _modelMutationService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private readonly EmployeeDetailView _detailView = new();
        private Button? _createButton;
        private Button? _editButton;
        private Button? _deleteButton;

        public EmployeeHostView()
        {
            _employeeEditWorkflow = App.Services.GetRequiredService<IEmployeeEditWorkflow>();
            _modelMutationService = App.Services.GetRequiredService<IModelMutationService>();
            _referenceDefinitionService = App.Services.GetRequiredService<IReferenceDefinitionService>();
            _referenceLookupCacheService = App.Services.GetRequiredService<IReferenceLookupCacheService>();
            SetDetailContent(_detailView, isVisible: false);
        }

        protected override IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            _editButton = CreateHeaderIconButton("\uE70F", "Редактировать сотрудника");
            _editButton.Click += async (_, _) => await ShowEmployeeEditDialogAsync(isCreateMode: false);

            _deleteButton = CreateHeaderIconButton("\uE74D", "Удалить сотрудника");
            _deleteButton.Click += async (_, _) => await DeleteSelectedEmployeeAsync();

            _createButton = CreateHeaderIconButton("\uF8AA", "Добавить сотрудника");
            _createButton.Click += async (_, _) => await ShowEmployeeEditDialogAsync(isCreateMode: true);

            UpdateActionButtonState();
            return [_editButton, _deleteButton, _createButton];
        }

        protected override Task OnRouteLoaded(TablePageDefinition definition)
        {
            UpdateActionButtonState();
            UpdateDetailView();
            SetDetailContentVisible(true);
            return Task.CompletedTask;
        }

        protected override Task OnRowSelected(TableDataRow? row)
        {
            UpdateActionButtonState();
            UpdateDetailView();
            return Task.CompletedTask;
        }

        protected override async Task OpenEditDialogAsync(TableDataRow? row)
        {
            if (row is not null)
            {
                await ShowEmployeeEditDialogAsync(isCreateMode: false);
            }
        }

        protected override string BuildSelectedFooterText(TableDataRow row)
        {
            var name =
                row.GetValue("person.full_name")?.ToString()
                ?? row.GetValue("name")?.ToString()
                ?? row.GetValue("head")?.ToString();
            var id = TryGetSelectedRowId(row);

            if (!string.IsNullOrWhiteSpace(name) && id is long value)
            {
                return $"{name} (ID: {value})";
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            return id is long idValue
                ? $"ID: {idValue}"
                : string.Empty;
        }

        private void UpdateActionButtonState()
        {
            var hasSelectedRow = Store.SelectedRow is not null && !Store.SelectedRow.IsPlaceholder;

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

        private void UpdateDetailView()
        {
            _detailView.Row = Store.SelectedRow;
        }

        private async Task ShowEmployeeEditDialogAsync(bool isCreateMode)
        {
            var id = Store.SelectedRow is null ? null : TryGetSelectedRowId(Store.SelectedRow);

            EmployeeEditWorkflowResult? result;
            try
            {
                result = await _employeeEditWorkflow.ShowAsync(
                    new EmployeeEditWorkflowRequest
                    {
                        XamlRoot = XamlRoot,
                        IsCreateMode = isCreateMode,
                        EmployeeId = id
                    });
            }
            catch (InvalidOperationException ex)
            {
                await ShowErrorDialogAsync("Не удалось открыть сотрудника.", ex.Message);
                return;
            }

            if (result is null)
            {
                return;
            }

            await RefreshTableRowAfterSaveAsync(isCreateMode, result.SavedRow, result.SavedPayload);
            ShowSuccessNotification(
                isCreateMode ? "Сотрудник создан" : "Изменения сотрудника сохранены",
                BuildReferenceNotificationMessage(result.Definition.Title, TryGetSelectedRowId(result.SavedRow)));
        }

        private async Task DeleteSelectedEmployeeAsync()
        {
            var definition = ResolveEmployeeReference();
            if (definition is null || Store.SelectedRow is null)
            {
                return;
            }

            var id = TryGetSelectedRowId(Store.SelectedRow);
            if (id is null)
            {
                await ShowErrorDialogAsync(
                    "Не удалось удалить сотрудника.",
                    "У выбранной записи отсутствует корректный ID.");
                return;
            }

            if (!await ConfirmDialogAsync(
                    "Удаление сотрудника",
                    "Удалить выбранного сотрудника?",
                    "Удалить",
                    defaultButton: ContentDialogButton.Close))
            {
                return;
            }

            try
            {
                await _modelMutationService.DeleteAsync(definition.Model, id.Value);
                _referenceLookupCacheService.Invalidate(definition.Model);
                await Store.ReloadCurrentReferenceAsync();
                ShowSuccessNotification(
                    "Сотрудник удален",
                    BuildReferenceNotificationMessage(definition.Title, id.Value));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось удалить сотрудника.", ex.Message);
            }
        }

        private CbsContractsDesktopClient.Models.References.ReferenceDefinition? ResolveEmployeeReference()
        {
            return _referenceDefinitionService.TryGetByRoute("/employees", out var reference)
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
