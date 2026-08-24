using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Stores.Contragents;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.Views.Functional
{
    public sealed partial class ContractDetailView : UserControl
    {
        private readonly ContractWorkflowStore _contractWorkflowStore;
        private readonly ContragentDetailStore _contragentDetailStore;
        private bool _isStoreSubscribed;

        public event EventHandler<EmployeeBoxEditRequestedEventArgs>? EmployeeEditRequested;

        public ContractDetailView()
        {
            _contractWorkflowStore = App.Services.GetRequiredService<ContractWorkflowStore>();
            _contragentDetailStore = App.Services.GetRequiredService<ContragentDetailStore>();
            InitializeComponent();
            EmployeesBox.EditRequested += (_, args) => EmployeeEditRequested?.Invoke(this, args);
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            Refresh();
        }

        public string BuildClipboardText()
        {
            var contract = _contractWorkflowStore.Contract;
            if (contract is null || contract.IsPlaceholder)
            {
                return string.Empty;
            }

            var contractState = _contractWorkflowStore.SelectedContractEditState
                ?? throw new InvalidOperationException("ContractDetailView.BuildClipboardText: SelectedContractEditState is not set.");
            return ContractClipboardFormatter.BuildForContract(
                contractState,
                _contractWorkflowStore.GetContractDocumentRevisionEditState());
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isStoreSubscribed)
            {
                return;
            }

            _contractWorkflowStore.SelectionApplied += OnContractWorkflowSelectionApplied;
            _isStoreSubscribed = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isStoreSubscribed)
            {
                return;
            }

            _contractWorkflowStore.SelectionApplied -= OnContractWorkflowSelectionApplied;
            _isStoreSubscribed = false;
        }

        private void OnContractWorkflowSelectionApplied(object? sender, EventArgs e)
        {
            Refresh();
        }

        private void Refresh()
        {
            var stage = "read-store";
            TableDataRow? contract = null;
            TableDataRow? contragent = null;
            try
            {
                var revision = _contractWorkflowStore.SelectedRevision ?? _contractWorkflowStore.SelectedStage;
                contract = _contractWorkflowStore.Contract;
                contragent = _contractWorkflowStore.Contragent;
                var selectedRow = revision ?? contract;

                if (selectedRow is null || selectedRow.IsPlaceholder)
                {
                    stage = "render-empty-state";
                    ContractNameTextBlock.Text = "Контракт не выбран";
                    ContragentNameTextBlock.Text = string.Empty;
                    ContactsPanel.Children.Clear();
                    _contragentDetailStore.SetContragent(null);
                    EmployeesBox.ResponsibleEmployeeIds = [];
                    EmployeesBox.Employees = [];
                    CommentsBox.Comments = [];
                    return;
                }

                stage = "render-contract-heading";
                ContractNameTextBlock.Text = TryGetText(contract, "name")
                    ?? TryGetText(selectedRow, "contract.name")
                    ?? TryGetText(selectedRow, "name")
                    ?? "Контракт не выбран";

                stage = "set-contragent";
                _contragentDetailStore.SetContragent(contragent);
                ContragentNameTextBlock.Text = !string.IsNullOrWhiteSpace(_contragentDetailStore.Name)
                    ? _contragentDetailStore.Name
                    : TryGetText(contragent, "name", "requisites.organization.name")
                    ?? TryGetText(selectedRow, "contract.contragent.name")
                    ?? TryGetText(selectedRow, "contragent.name")
                    ?? string.Empty;

                stage = "render-contacts";
                RenderContacts(_contragentDetailStore.Contacts);

                stage = "render-employees";
                var contractState = _contractWorkflowStore.SelectedContractEditState
                    ?? throw new InvalidOperationException("ContractDetailView.Refresh: SelectedContractEditState is not set.");
                EmployeesBox.ResponsibleEmployeeIds = contractState.ContractResponsibles
                    .Select(static responsible => responsible.EmployeeId)
                    .ToList();
                EmployeesBox.Employees = _contragentDetailStore.Employees;

                stage = "render-comments";
                CommentsBox.Comments = _contractWorkflowStore.Comments;
            }
            catch (Exception ex)
            {
                DiagnosticsFileLogger.AppendBlock(
                    "CONTRACT DETAIL RENDER FAILED",
                    $"stage={stage}{Environment.NewLine}"
                    + $"contractId={TryGetLong(contract?.GetValue("id"))?.ToString() ?? "<null>"}{Environment.NewLine}"
                    + $"contragentId={TryGetLong(contragent?.GetValue("id"))?.ToString() ?? "<null>"}{Environment.NewLine}"
                    + $"exception={ex}");
                throw new InvalidOperationException(
                    $"ContractDetailView.Refresh failed at '{stage}': {ex.Message}",
                    ex);
            }
        }

        private void RenderContacts(IReadOnlyList<string> contacts)
        {
            ContactsPanel.Children.Clear();
            ContactsPanel.ColumnDefinitions.Clear();
            ContactsPanel.RowDefinitions.Clear();
            ContactsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var column = 0;
            foreach (var contact in contacts.Take(4))
            {
                if (!ContactTypeClassifier.TryClassify(contact, out var match))
                {
                    continue;
                }

                ContactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var element = (FrameworkElement)DialogContactsEditor.BuildContactElement(contact, match, showRemoveButton: false);
                Grid.SetColumn(element, column);
                ContactsPanel.Children.Add(element);
                column++;
            }
        }

        private static string BuildPerformersText(TableDataRow? contract)
        {
            var stage = ReadUsedStage(contract);
            if (stage is null)
            {
                return "Исполнители: нет";
            }

            var performers = ReadNameList(stage.Value, "performers");
            var tasks = ReadNameList(stage.Value, "tasks");
            var text = $"Исполнители: {(performers.Count == 0 ? "нет" : string.Join(", ", performers))};";
            if (tasks.Count > 0)
            {
                text += $" Прочие задачи: {string.Join(", ", tasks)}";
            }

            return text;
        }

        private static JsonElement? ReadUsedStage(TableDataRow? contract)
        {
            var stages = TryGetArray(contract, "stages");
            if (stages is null)
            {
                return null;
            }

            JsonElement? firstStage = null;
            foreach (var stage in stages.Value.EnumerateArray())
            {
                if (stage.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                firstStage ??= stage;
                if (ReadBooleanProperty(stage, "used") == true)
                {
                    return stage;
                }
            }

            return firstStage;
        }

        private static IReadOnlyList<string> ReadNameList(JsonElement item, string propertyName)
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty(propertyName, out var array)
                || array.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return array
                .EnumerateArray()
                .Select(ReadDisplayName)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToList();
        }

        private static string? ReadDisplayName(JsonElement item)
        {
            return ReadStringProperty(item, "name")
                ?? ReadStringProperty(item, "full_name")
                ?? ReadStringProperty(item, "title")
                ?? ReadNestedStringProperty(item, "employee", "name")
                ?? ReadNestedStringProperty(item, "employee", "full_name")
                ?? ReadNestedStringProperty(item, "person", "full_name")
                ?? ReadNestedStringProperty(item, "person", "name")
                ?? ReadDoubleNestedStringProperty(item, "employee", "person", "full_name")
                ?? ReadDoubleNestedStringProperty(item, "employee", "person", "name");
        }

        private static string? ReadStringProperty(JsonElement item, string propertyName)
        {
            return item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static string? ReadNestedStringProperty(JsonElement item, string propertyName, string nestedPropertyName)
        {
            return item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty(propertyName, out var nested)
                ? ReadStringProperty(nested, nestedPropertyName)
                : null;
        }

        private static string? ReadDoubleNestedStringProperty(
            JsonElement item,
            string propertyName,
            string nestedPropertyName,
            string valuePropertyName)
        {
            return item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty(propertyName, out var nested)
                && nested.ValueKind == JsonValueKind.Object
                && nested.TryGetProperty(nestedPropertyName, out var doubleNested)
                ? ReadStringProperty(doubleNested, valuePropertyName)
                : null;
        }

        private static bool? ReadBooleanProperty(JsonElement item, string propertyName)
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(value.GetString(), out var parsedValue) => parsedValue,
                _ => null
            };
        }
    }
}
