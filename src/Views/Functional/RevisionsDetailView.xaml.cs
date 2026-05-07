using System.ComponentModel;
using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Functional
{
    public sealed partial class RevisionsDetailView : UserControl
    {
        private readonly ContractWorkflowStore _contractWorkflowStore;
        private bool _isStoreSubscribed;

        public static readonly DependencyProperty RevisionRowProperty =
            DependencyProperty.Register(
                nameof(RevisionRow),
                typeof(ReferenceDataRow),
                typeof(RevisionsDetailView),
                new PropertyMetadata(null, OnDetailChanged));

        public static readonly DependencyProperty ContractRowProperty =
            DependencyProperty.Register(
                nameof(ContractRow),
                typeof(ReferenceDataRow),
                typeof(RevisionsDetailView),
                new PropertyMetadata(null, OnDetailChanged));

        public static readonly DependencyProperty ContragentRowProperty =
            DependencyProperty.Register(
                nameof(ContragentRow),
                typeof(ReferenceDataRow),
                typeof(RevisionsDetailView),
                new PropertyMetadata(null, OnDetailChanged));

        public RevisionsDetailView()
        {
            _contractWorkflowStore = App.Services.GetRequiredService<ContractWorkflowStore>();
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            Refresh();
        }

        public ReferenceDataRow? RevisionRow
        {
            get => (ReferenceDataRow?)GetValue(RevisionRowProperty);
            set => SetValue(RevisionRowProperty, value);
        }

        public ReferenceDataRow? ContractRow
        {
            get => (ReferenceDataRow?)GetValue(ContractRowProperty);
            set => SetValue(ContractRowProperty, value);
        }

        public ReferenceDataRow? ContragentRow
        {
            get => (ReferenceDataRow?)GetValue(ContragentRowProperty);
            set => SetValue(ContragentRowProperty, value);
        }

        private static void OnDetailChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RevisionsDetailView)d).Refresh();
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
                || e.PropertyName == nameof(ContractWorkflowStore.SelectedRevision))
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            var revision = RevisionRow ?? _contractWorkflowStore.SelectedRevision;
            var contract = ContractRow ?? _contractWorkflowStore.Contract;
            var contragent = ContragentRow ?? _contractWorkflowStore.Contragent;

            if (revision is null || revision.IsPlaceholder)
            {
                ContractNameTextBlock.Text = "Контракт не выбран";
                ContragentNameTextBlock.Text = string.Empty;
                ContactsPanel.Children.Clear();
                PerformersTextBlock.Text = string.Empty;
                EmployeesBox.Employees = [];
                return;
            }

            ContractNameTextBlock.Text = GetText(contract, "name")
                ?? GetText(revision, "contract.name")
                ?? "Контракт не выбран";
            ContragentNameTextBlock.Text = GetText(contragent, "name", "requisites.organization.name")
                ?? GetText(revision, "contract.contragent.name")
                ?? string.Empty;

            RenderContacts(ReadContragentContacts(contragent));
            var performersText = BuildPerformersText(contract);
            var employees = ReadEmployees(contragent);
            PerformersTextBlock.Text = performersText;
            EmployeesBox.Employees = employees;
            LogRenderSnapshot(revision, contract, contragent, employees, performersText);
        }

        private static void LogRenderSnapshot(
            ReferenceDataRow revision,
            ReferenceDataRow? contract,
            ReferenceDataRow? contragent,
            IReadOnlyList<EmployeeBoxItem> employees,
            string performersText)
        {
            var hasContragentEmployees = TryGetArray(contragent, out var employeesArray, "employees", "emploees");
            var hasStages = TryGetArray(contract, out var stagesArray, "stages");
            DiagnosticsFileLogger.AppendBlock(
                "REVISION DETAIL RENDER SNAPSHOT",
                JsonSerializer.Serialize(new
                {
                    RevisionKeys = revision.Values.Keys.OrderBy(static key => key).ToArray(),
                    ContractKeys = contract?.Values.Keys.OrderBy(static key => key).ToArray() ?? Array.Empty<string>(),
                    ContragentKeys = contragent?.Values.Keys.OrderBy(static key => key).ToArray() ?? Array.Empty<string>(),
                    HasContragentEmployees = hasContragentEmployees,
                    ContragentEmployeesJsonCount = hasContragentEmployees && employeesArray.ValueKind == JsonValueKind.Array
                        ? employeesArray.GetArrayLength()
                        : 0,
                    ParsedEmployeesCount = employees.Count,
                    ParsedEmployeeNames = employees.Select(static employee => employee.FullName).Take(10).ToArray(),
                    PerformersText = performersText,
                    StagesJsonCount = hasStages && stagesArray.ValueKind == JsonValueKind.Array
                        ? stagesArray.GetArrayLength()
                        : 0
                }, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
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

        private static string BuildPerformersText(ReferenceDataRow? contract)
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

        private static JsonElement? ReadUsedStage(ReferenceDataRow? contract)
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

        private static IReadOnlyList<EmployeeBoxItem> ReadEmployees(ReferenceDataRow? row)
        {
            if (!TryGetArray(row, out var employees, "employees", "emploees"))
            {
                return [];
            }

            return employees
                .EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.Object)
                .Select(ReadEmployee)
                .Where(static employee => !string.IsNullOrWhiteSpace(employee.FullName) || employee.Id is not null)
                .ToList();
        }

        private static EmployeeBoxItem ReadEmployee(JsonElement item)
        {
            return new EmployeeBoxItem
            {
                Id = ReadLongProperty(item, "id"),
                FullName = ReadDisplayName(item)
                    ?? string.Empty,
                Position = ReadStringProperty(item, "position")
                    ?? ReadNestedStringProperty(item, "position", "name")
                    ?? string.Empty,
                Contacts = ReadContacts(item),
                Description = ReadStringProperty(item, "description") ?? string.Empty,
                IsActive = ReadBooleanProperty(item, "used") ?? ReadBooleanProperty(item, "activated") ?? true
            };
        }

        private static IReadOnlyList<string> ReadContragentContacts(ReferenceDataRow? row)
        {
            if (!TryGetArray(row, out var contacts, "contacts"))
            {
                return [];
            }

            return ReadContactValues(contacts);
        }

        private static IReadOnlyList<string> ReadContacts(JsonElement item)
        {
            if (item.TryGetProperty("contacts", out var contacts))
            {
                return ReadContactValues(contacts);
            }

            if (item.TryGetProperty("person", out var person)
                && person.ValueKind == JsonValueKind.Object
                && person.TryGetProperty("contacts", out contacts))
            {
                return ReadContactValues(contacts);
            }

            return [];
        }

        private static IReadOnlyList<string> ReadContactValues(JsonElement contacts)
        {
            if (contacts.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return contacts
                .EnumerateArray()
                .Select(ReadContactValue)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList()!;
        }

        private static string? ReadContactValue(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            }

            return ReadNestedContactValue(item, "contact_attributes")
                ?? ReadNestedContactValue(item, "contact")
                ?? ReadStringProperty(item, "value")
                ?? ReadStringProperty(item, "name");
        }

        private static string? ReadNestedContactValue(JsonElement item, string propertyName)
        {
            return item.TryGetProperty(propertyName, out var nested)
                ? ReadStringProperty(nested, "value") ?? ReadStringProperty(nested, "name")
                : null;
        }

        private static JsonElement? TryGetArray(ReferenceDataRow? row, string fieldKey)
        {
            return TryGetArray(row, out var array, fieldKey) ? array : null;
        }

        private static bool TryGetArray(ReferenceDataRow? row, out JsonElement array, params string[] fieldKeys)
        {
            if (row is null || row.IsPlaceholder)
            {
                array = default;
                return false;
            }

            foreach (var fieldKey in fieldKeys)
            {
                if (row.Values.TryGetValue(fieldKey, out array)
                    && array.ValueKind == JsonValueKind.Array)
                {
                    return true;
                }
            }

            array = default;
            return false;
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

        private static string? GetText(ReferenceDataRow? row, params string[] fieldKeys)
        {
            if (row is null || row.IsPlaceholder)
            {
                return null;
            }

            foreach (var fieldKey in fieldKeys)
            {
                var value = row.GetValue(fieldKey)?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
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

        private static long? ReadLongProperty(JsonElement item, string propertyName)
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt64(out var number) => number,
                JsonValueKind.String when long.TryParse(value.GetString(), out var number) => number,
                _ => null
            };
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
