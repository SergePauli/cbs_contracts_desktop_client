using System.ComponentModel;
using System.Globalization;
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

        public static readonly DependencyProperty RevisionRowProperty =
            DependencyProperty.Register(
                nameof(RevisionRow),
                typeof(TableDataRow),
                typeof(ContractDetailView),
                new PropertyMetadata(null, OnDetailChanged));

        public static readonly DependencyProperty ContractRowProperty =
            DependencyProperty.Register(
                nameof(ContractRow),
                typeof(TableDataRow),
                typeof(ContractDetailView),
                new PropertyMetadata(null, OnDetailChanged));

        public static readonly DependencyProperty ContragentRowProperty =
            DependencyProperty.Register(
                nameof(ContragentRow),
                typeof(TableDataRow),
                typeof(ContractDetailView),
                new PropertyMetadata(null, OnDetailChanged));

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

        public TableDataRow? RevisionRow
        {
            get => (TableDataRow?)GetValue(RevisionRowProperty);
            set => SetValue(RevisionRowProperty, value);
        }

        public TableDataRow? ContractRow
        {
            get => (TableDataRow?)GetValue(ContractRowProperty);
            set => SetValue(ContractRowProperty, value);
        }

        public TableDataRow? ContragentRow
        {
            get => (TableDataRow?)GetValue(ContragentRowProperty);
            set => SetValue(ContragentRowProperty, value);
        }

        public string BuildClipboardText()
        {
            var contract = ContractRow ?? _contractWorkflowStore.Contract;
            var contragent = ContragentRow ?? _contractWorkflowStore.Contragent;
            if (contract is null || contract.IsPlaceholder)
            {
                return string.Empty;
            }

            var stage = ReadUsedStage(contract);
            var startAt = stage is null ? null : ReadStringProperty(stage.Value, "start_at");
            var deadlineAt = stage is null ? null : ReadStringProperty(stage.Value, "deadline_at");
            var contragentName = TryGetText(contragent, "name", "requisites.organization.name") ?? string.Empty;
            var contractTitle = TryGetText(contract, "external_number") ?? TryGetText(contract, "name") ?? string.Empty;

            return $"{FormatClipboardDate(startAt)}-{FormatClipboardDate(deadlineAt)} | {contragentName} | {contractTitle}";
        }

        private static void OnDetailChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ContractDetailView)d).Refresh();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_isStoreSubscribed)
            {
                return;
            }

            _contractWorkflowStore.PropertyChanged += OnContractWorkflowStorePropertyChanged;
            _isStoreSubscribed = true;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (!_isStoreSubscribed)
            {
                return;
            }

            _contractWorkflowStore.PropertyChanged -= OnContractWorkflowStorePropertyChanged;
            _isStoreSubscribed = false;
        }

        private void OnContractWorkflowStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ContractWorkflowStore.Contract)
                || e.PropertyName == nameof(ContractWorkflowStore.Contragent)
                || e.PropertyName == nameof(ContractWorkflowStore.SelectedRevision)
                || e.PropertyName == nameof(ContractWorkflowStore.SelectedStage)
                || e.PropertyName == nameof(ContractWorkflowStore.SelectedRowHeader)
                || e.PropertyName == nameof(ContractWorkflowStore.Comments))
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            var revision = RevisionRow ?? _contractWorkflowStore.SelectedRevision ?? _contractWorkflowStore.SelectedStage;
            var contract = ContractRow ?? _contractWorkflowStore.Contract;
            var contragent = ContragentRow ?? _contractWorkflowStore.Contragent;
            var selectedRow = revision ?? contract;

            if (selectedRow is null || selectedRow.IsPlaceholder)
            {
                ContractNameTextBlock.Text = "Контракт не выбран";
                ContragentNameTextBlock.Text = string.Empty;
                ContactsPanel.Children.Clear();
                _contragentDetailStore.SetContragent(null);
                EmployeesBox.Employees = [];
                CommentsBox.Comments = [];
                return;
            }

            ContractNameTextBlock.Text = TryGetText(contract, "name")
                ?? TryGetText(selectedRow, "contract.name")
                ?? TryGetText(selectedRow, "name")
                ?? "Контракт не выбран";
            _contragentDetailStore.SetContragent(contragent);
            ContragentNameTextBlock.Text = !string.IsNullOrWhiteSpace(_contragentDetailStore.Name)
                ? _contragentDetailStore.Name
                : TryGetText(contragent, "name", "requisites.organization.name")
                ?? TryGetText(selectedRow, "contract.contragent.name")
                ?? TryGetText(selectedRow, "contragent.name")
                ?? string.Empty;

            RenderContacts(_contragentDetailStore.Contacts);
            EmployeesBox.Employees = _contragentDetailStore.Employees;
            CommentsBox.Comments = _contractWorkflowStore.Comments;
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

        private static string FormatClipboardDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return " ";
            }

            return DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out var invariantDate)
                || DateTime.TryParse(
                    value,
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                    out invariantDate)
                ? invariantDate.ToString("d", CultureInfo.GetCultureInfo("ru-RU"))
                : " ";
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
