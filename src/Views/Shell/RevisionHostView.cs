// Hosts the Revisions table, revision edit dialog, and contract detail footer.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.Views.Functional;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed class RevisionHostView : ComplexHostViewBase
    {
        private const string RevisionTitle = "Дополнительное соглашение";
        private readonly IDataQueryService _dataQueryService;
        private readonly IModelMutationService _modelMutationService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private readonly ContractWorkflowStore _contractWorkflowStore;
        private readonly RevisionRowDetailStrategy _rowDetailStrategy = new();
        private readonly ContractDetailView _detailView = new();
        private CancellationTokenSource? _detailCts;
        private Button? _editButton;
        private Button? _copyButton;

        public RevisionHostView()
        {
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _modelMutationService = App.Services.GetRequiredService<IModelMutationService>();
            _referenceLookupCacheService = App.Services.GetRequiredService<IReferenceLookupCacheService>();
            _contractWorkflowStore = App.Services.GetRequiredService<ContractWorkflowStore>();
            SetDetailContent(_detailView, isVisible: false);
        }

        protected override IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            _editButton = CreateHeaderIconButton("\uE70F", "Редактировать доп. соглашение");
            _editButton.Click += async (_, _) => await ShowRevisionEditDialogAsync();

            _copyButton = CreateHeaderIconButton("\uE8C8", "Скопировать карточку контракта");
            _copyButton.Click += (_, _) => CopyContractDetails();

            UpdateActionButtonState();
            return [_editButton, _copyButton];
        }

        protected override Task OnRouteLoaded(TablePageDefinition definition)
        {
            UpdateActionButtonState();
            if (Store.SelectedRow is not null && !Store.SelectedRow.IsPlaceholder)
            {
                UpdateDetailView(Store.SelectedRow);
                _ = RefreshDetailAsync();
            }
            return Task.CompletedTask;
        }

        protected override Task OnRowSelected(TableDataRow? row)
        {
            UpdateActionButtonState();
            if (row is null || row.IsPlaceholder)
            {
                ClearDetailView();
                return Task.CompletedTask;
            }

            UpdateDetailView(row);
            _ = RefreshDetailAsync();
            return Task.CompletedTask;
        }

        protected override async Task OpenEditDialogAsync(TableDataRow? row)
        {
            if (row is not null)
            {
                await ShowRevisionEditDialogAsync();
            }
        }

        protected override string BuildSelectedFooterText(TableDataRow row)
        {
            return _contractWorkflowStore.SelectedFooterText;
        }

        private void UpdateActionButtonState()
        {
            var hasSelectedRow = Store.SelectedRow is not null && !Store.SelectedRow.IsPlaceholder;

            ApplyEditButtonState(_editButton, hasSelectedRow && Store.CanEditRows);
            ApplyDefaultActionButtonState(
                _copyButton,
                hasSelectedRow && _contractWorkflowStore.Contract is { IsPlaceholder: false });
        }

        private void UpdateDetailView(TableDataRow? row)
        {
            if (row is null || row.IsPlaceholder)
            {
                return;
            }

            SetDetailContentVisible(true);
            _detailView.Visibility = Visibility.Visible;
            _detailView.RevisionRow = row;
        }

        private async Task RefreshDetailAsync()
        {
            _detailCts?.Cancel();

            if (Store.SelectedRow is null || Store.SelectedRow.IsPlaceholder)
            {
                UpdateActionButtonState();
                return;
            }

            _detailView.ContractRow = null;
            _detailView.ContragentRow = null;
            _contractWorkflowStore.ClearRowDetailSelection();
            RefreshSelectedFooterText();

            var contractId = _rowDetailStrategy.ResolveContractId(Store.SelectedRow);
            var listContragentId = _rowDetailStrategy.ResolveContragentId(Store.SelectedRow);
            if (contractId is null && listContragentId is null)
            {
                UpdateActionButtonState();
                return;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _detailCts = cancellationTokenSource;

            try
            {
                var contractTask = contractId is long selectedContractId
                    ? LoadRowDetailRowSafelyAsync(
                        () => LoadRevisionContractCardAsync(selectedContractId, cancellationTokenSource.Token),
                        cancellationTokenSource.Token)
                    : Task.FromResult<TableDataRow?>(null);
                var contragentTask = listContragentId is long selectedContragentId
                    ? LoadRowDetailRowSafelyAsync(
                        () => LoadRevisionContragentCardAsync(selectedContragentId, cancellationTokenSource.Token),
                        cancellationTokenSource.Token)
                    : Task.FromResult<TableDataRow?>(null);

                var contract = await contractTask;
                var contragent = await contragentTask;
                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                if (Store.SelectedRow is null || !_rowDetailStrategy.IsSameSelection(Store.SelectedRow, contractId))
                {
                    return;
                }

                var contractContragentId = contract is null
                    ? null
                    : TryGetLongValue(contract, "contragent.id");
                if (contragent is null && contractContragentId is long loadedContragentId)
                {
                    contragent = await LoadRowDetailRowSafelyAsync(
                        () => LoadRevisionContragentCardAsync(loadedContragentId, cancellationTokenSource.Token),
                        cancellationTokenSource.Token);
                }

                _rowDetailStrategy.ApplySelection(_contractWorkflowStore, Store.SelectedRow, contract, contragent);
                _detailView.ContractRow = contract;
                _detailView.ContragentRow = contragent;
                RefreshSelectedFooterText();
                UpdateActionButtonState();
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (!cancellationTokenSource.IsCancellationRequested)
                {
                    UpdateActionButtonState();
                }
            }
        }

        private void ClearDetailView()
        {
            _detailCts?.Cancel();
            _detailView.RevisionRow = null;
            _detailView.ContractRow = null;
            _detailView.ContragentRow = null;
            _detailView.Visibility = Visibility.Collapsed;
            SetDetailContentVisible(false);
            _contractWorkflowStore.ClearRowDetailSelection();
            RefreshSelectedFooterText();
        }

        private static async Task<TableDataRow?> LoadRowDetailRowSafelyAsync(
            Func<Task<TableDataRow?>> loadAsync,
            CancellationToken cancellationToken)
        {
            try
            {
                return await loadAsync();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }

        private async Task ShowRevisionEditDialogAsync()
        {
            if (Store.SelectedRow is null)
            {
                return;
            }

            RevisionEditDialog dialog;
            try
            {
                dialog = new RevisionEditDialog(Store.SelectedRow)
                {
                    XamlRoot = XamlRoot
                };
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось открыть доп. соглашение.", ex.Message);
                return;
            }

            TableDataRow? savedRow = null;
            dialog.SaveRequestedAsync += async args =>
            {
                try
                {
                    var revisionPayload = dialog.BuildPayload();
                    savedRow = await _modelMutationService.UpdateAsync(
                        GetCurrentTableModel(),
                        revisionPayload);
                }
                catch (Exception ex)
                {
                    dialog.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };

            await dialog.ShowAsync();
            if (!dialog.WasSaved || savedRow is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(GetCurrentTableModel());
            await RefreshTableRowAfterSaveAsync(false, savedRow);
            ShowSuccessNotification(
                "Доп. соглашение сохранено",
                BuildReferenceNotificationMessage(RevisionTitle, TryGetSelectedRowId(savedRow)));
        }

        private void CopyContractDetails()
        {
            var text = _detailView.BuildClipboardText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
            ShowSuccessNotification(
                "Данные скопированы",
                "Карточка контракта скопирована в буфер обмена.");
        }

        protected override async Task OnTableRowRefreshedAfterSaveAsync(TableDataRow freshRow)
        {
            UpdateDetailView(Store.SelectedRow);
            await RefreshDetailAsync();
        }

        protected override async Task OnTableReloadedAfterSaveAsync()
        {
            UpdateDetailView(Store.SelectedRow);
            await RefreshDetailAsync();
        }

        private async Task<TableDataRow?> LoadRevisionContractCardAsync(
            long contractId,
            CancellationToken cancellationToken = default)
        {
            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = "Contract",
                    Preset = "edit",
                    Filters = new Dictionary<string, object?>
                    {
                        ["id__eq"] = contractId
                    },
                    Limit = 1
                },
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private async Task<TableDataRow?> LoadRevisionContragentCardAsync(
            long contragentId,
            CancellationToken cancellationToken = default)
        {
            var rows = await _dataQueryService.GetDataAsync<TableDataRow>(
                new DataQueryRequest
                {
                    Model = "Contragent",
                    Preset = "card",
                    Filters = new Dictionary<string, object?>
                    {
                        ["id__eq"] = contragentId
                    },
                    Limit = 1
                },
                cancellationToken);

            return rows.FirstOrDefault(static row => !row.IsPlaceholder);
        }

        private string GetCurrentTableModel()
        {
            return CurrentDefinition?.Model
                ?? Store.CurrentTablePage?.Model
                ?? "Revision";
        }

        private static string BuildReferenceNotificationMessage(string title, long? id)
        {
            return id.HasValue
                ? $"{title}, ID {id.Value}"
                : title;
        }

        private static string AppendFooterDetail(string mainText, TableDataRow row)
        {
            var detailText = row.GetValue("description")?.ToString();
            return string.IsNullOrWhiteSpace(detailText)
                ? mainText
                : $"{mainText} | {detailText}";
        }

        private static long? TryGetLongValue(TableDataRow row, string fieldKey)
        {
            var value = row.GetValue(fieldKey);
            return value switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string stringValue when long.TryParse(stringValue, out var parsedValue) => parsedValue,
                _ => null
            };
        }
    }
}
