using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.Views.Functional;
using Microsoft.Extensions.DependencyInjection;

namespace CbsContractsDesktopClient.Views.Shell;

// Owns the save/notification/close/refresh lifecycle for contract workflow editors.
public abstract class ContractWorkflowHostViewBase : ComplexHostViewBase
{
    private static string? _activeWorkflowCommand;

    protected async Task RunWorkflowDialogCommandAsync(
        Func<Task> command,
        [System.Runtime.CompilerServices.CallerMemberName] string caller = "")
    {
        var context = $"{GetType().Name}.{caller} model={Store.CurrentTablePage?.Model} row={Store.SelectedRow?.GetValue("id")}";
        Store.AppendUiTrace($"WORKFLOW COMMAND ENTER {context} active={_activeWorkflowCommand ?? "<none>"} stack={Environment.StackTrace}");
        if (_activeWorkflowCommand is not null)
        {
            Store.AppendUiTrace($"WORKFLOW COMMAND ALREADY ACTIVE {context} active={_activeWorkflowCommand}");
            return;
        }

        _activeWorkflowCommand = context;
        try
        {
            await command();
        }
        catch (Exception ex)
        {
            Store.AppendUiTrace($"WORKFLOW COMMAND FAILED {context} exception={ex}");
            Store.ErrorMessage = $"Не удалось выполнить действие с диалогом: {ex.Message}";
        }
        finally
        {
            _activeWorkflowCommand = null;
            Store.AppendUiTrace($"WORKFLOW COMMAND EXIT {context}");
        }
    }

    private readonly ContractCommerSaveWorkflow _contractSaveWorkflow = App.Services.GetRequiredService<ContractCommerSaveWorkflow>();
    private readonly ContractWorkflowFactory _workflowFactory = App.Services.GetRequiredService<ContractWorkflowFactory>();
    private readonly IReferenceLookupCacheService _lookupCache = App.Services.GetRequiredService<IReferenceLookupCacheService>();
    private readonly IUserService _currentUser = App.Services.GetRequiredService<IUserService>();

    protected abstract Task LoadWorkflowDetailsAfterSaveAsync();

    protected static bool HasUpdatePayloadChanges(IReadOnlyDictionary<string, object?> payload)
    {
        return payload.Keys.Any(static key =>
            !string.Equals(key, "id", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(key, "list_key", StringComparison.OrdinalIgnoreCase));
    }

    protected sealed override Task OnTableRowRefreshedAfterSaveAsync(TableDataRow freshRow)
        => LoadWorkflowDetailsAfterSaveAsync();

    protected sealed override Task OnTableReloadedAfterSaveAsync()
        => LoadWorkflowDetailsAfterSaveAsync();

    protected Task ShowContractWorkflowDialogAsync(
        ContractCommerEditDialog dialog, TableDataRow? sourceRow, bool isCreateMode = false)
    {
        var createMode = isCreateMode;
        var savedAsCreate = false;
        TableDataRow? savedContractRow = null;
        return RunWorkflowDialogAsync(dialog, async () =>
        {
            var savePlan = dialog.BuildSavePlan(_currentUser.CurrentUser?.ProfileId);
            if (!savePlan.HasChanges) return false;

            savedAsCreate = createMode;
            var result = await _contractSaveWorkflow.SaveAsync(savePlan);
            if (createMode)
            {
                dialog.AcceptCreatedContractIdentity(result.ContractId, result.ContractMutationRow?.GetValue("list_key")?.ToString());
                createMode = false;
            }

            var editRow = await _workflowFactory.ReloadContractEditRowAsync(result.ContractId);
            dialog.ReloadAsEdit(editRow);
            savedContractRow = editRow;
            return true;
        }, async () =>
        {
            _lookupCache.Invalidate("Contract");
            _lookupCache.Invalidate("Stage");
            _lookupCache.Invalidate("Revision");
            await RefreshTableRowAfterSaveAsync(isCreateMode, isCreateMode ? savedContractRow : sourceRow);
        }, () => savedAsCreate ? "Контракт создан" : "Контракт сохранен");
    }

    protected Task ShowStageWorkflowDialogAsync(
        AppEditDialog dialog, Func<Task<TableDataRow?>> saveAsync)
    {
        TableDataRow? refreshRow = null;
        return RunWorkflowDialogAsync(dialog, async () =>
        {
            refreshRow = await saveAsync();
            return refreshRow is not null;
        }, async () =>
        {
            _lookupCache.Invalidate("Stage");
            _lookupCache.Invalidate("Contract");
            await RefreshTableRowAfterSaveAsync(false, refreshRow);
        }, () => "Этап сохранен");
    }

    private async Task RunWorkflowDialogAsync(
        AppEditDialog dialog, Func<Task<bool>> saveAsync, Func<Task> refreshAsync, Func<string> successTitle)
    {
        async Task SaveAsync(AppEditDialogSaveRequestedEventArgs args)
        {
            try
            {
                dialog.ShowErrorInfo(string.Empty);
                if (!await saveAsync())
                {
                    dialog.ShowErrorInfo("Нет изменений для сохранения.");
                    args.Cancel = true;
                    return;
                }
                ShowSuccessNotification(successTitle(), "Изменения сохранены.");
            }
            catch (Exception ex)
            {
                dialog.ShowErrorInfo(ex.Message);
                args.Cancel = true;
            }
        }

        dialog.SaveRequestedAsync += SaveAsync;
        try
        {
            var openPopups = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(dialog.XamlRoot);
            Store.AppendUiTrace($"WORKFLOW DIALOG SHOW type={dialog.GetType().Name} active={_activeWorkflowCommand} popups={string.Join(",", openPopups.Select(popup => popup.Child?.GetType().FullName))}");
            await dialog.ShowAsync();
            Store.AppendUiTrace($"WORKFLOW DIALOG CLOSED type={dialog.GetType().Name}");
        }
        finally
        {
            dialog.SaveRequestedAsync -= SaveAsync;
        }
        if (dialog.WasSaved)
        {
            await refreshAsync();
        }
    }
}
