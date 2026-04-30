using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;

namespace CbsContractsDesktopClient.Views.References
{
    public sealed partial class ContragentDetailView : UserControl
    {
        public static readonly DependencyProperty RowProperty =
            DependencyProperty.Register(
                nameof(Row),
                typeof(ReferenceDataRow),
                typeof(ContragentDetailView),
                new PropertyMetadata(null, OnRowChanged));

        public event EventHandler<EmployeeBoxEditRequestedEventArgs>? EmployeeEditRequested;

        public ContragentDetailView()
        {
            InitializeComponent();
            EmployeesBox.EditRequested += (_, args) => EmployeeEditRequested?.Invoke(this, args);
            Refresh();
        }

        public ReferenceDataRow? Row
        {
            get => (ReferenceDataRow?)GetValue(RowProperty);
            set => SetValue(RowProperty, value);
        }

        private static void OnRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ContragentDetailView)d).Refresh();
        }

        private void Refresh()
        {
            var row = Row;
            if (row is null || row.IsPlaceholder)
            {
                ContragentNameTextBlock.Text = "Контрагент не выбран";
                ContragentIdTextBlock.Text = string.Empty;
                FullNameTextBlock.Text = string.Empty;
                RequisitesTextBlock.Text = string.Empty;
                DescriptionTextBlock.Text = string.Empty;
                AddressesTextBlock.Text = string.Empty;
                ContactsPanel.Children.Clear();
                EmployeesBox.Employees = [];
                return;
            }

            var name = GetText(row, "requisites.organization.name", "name");
            var fullName = GetText(row, "requisites.organization.full_name", "full_name");
            var id = GetText(row, "id");

            ContragentNameTextBlock.Text = string.IsNullOrWhiteSpace(name)
                ? "Контрагент"
                : name;
            ContragentIdTextBlock.Text = string.IsNullOrWhiteSpace(id)
                ? string.Empty
                : $"ID: {id}";
            FullNameTextBlock.Text = fullName ?? string.Empty;
            RequisitesTextBlock.Text = BuildRequisitesText(row);
            DescriptionTextBlock.Text = GetText(row, "description") ?? string.Empty;
            AddressesTextBlock.Text = BuildAddressesText(row);
            RenderContacts(BuildContactsText(row));
            EmployeesBox.Employees = ReadEmployees(row);
        }

        private void RenderContacts(string contactsText)
        {
            ContactsPanel.Children.Clear();
            ContactsPanel.ColumnDefinitions.Clear();
            ContactsPanel.RowDefinitions.Clear();

            ContactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ContactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ContactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ContactsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ContactsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var column = 0;
            var row = 0;
            foreach (var value in DialogContactsEditor.ParseContactValues(contactsText))
            {
                if (!ContactTypeClassifier.TryClassify(value, out var match))
                {
                    continue;
                }

                var element = (FrameworkElement)DialogContactsEditor.BuildContactElement(value, match, showRemoveButton: false);
                Grid.SetColumn(element, column);
                Grid.SetRow(element, row);
                ContactsPanel.Children.Add(element);

                column++;
                if (column == 3)
                {
                    column = 0;
                    row++;
                }

                if (row > 1)
                {
                    return;
                }
            }
        }

        private static string BuildRequisitesText(ReferenceDataRow row)
        {
            var parts = new[]
            {
                FormatPart("ИНН", GetText(row, "requisites.organization.inn", "inn")),
                FormatPart("КПП", GetText(row, "requisites.organization.kpp", "kpp")),
                FormatPart("Подразделение", GetText(row, "requisites.organization.division", "division", "contragent.division"))
            };

            return string.Join("; ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
        }

        private static string BuildAddressesText(ReferenceDataRow row)
        {
            var registered = GetText(
                row,
                "registred_addr.address.value",
                "registered_addr.address.value");
            var real = GetText(row, "real_addr.address.value", "address");

            if (!string.IsNullOrWhiteSpace(registered) && !string.IsNullOrWhiteSpace(real))
            {
                return string.Equals(registered, real, StringComparison.CurrentCultureIgnoreCase)
                    ? real
                    : $"Юр.: {registered}; факт.: {real}";
            }

            return real ?? registered ?? string.Empty;
        }

        private static string BuildContactsText(ReferenceDataRow row)
        {
            var directText = GetText(
                row,
                "contacts.contact_attributes.value",
                "contacts.contact_attributes.name",
                "contacts.contact.value",
                "contacts.contact.name",
                "contacts.value",
                "contacts.name");
            if (!string.IsNullOrWhiteSpace(directText))
            {
                return directText;
            }

            return row.Values.TryGetValue("contacts", out var contactsElement)
                ? string.Join(", ", ReadContacts(contactsElement))
                : string.Empty;
        }

        private static IReadOnlyList<string> ReadContacts(JsonElement contactsElement)
        {
            if (contactsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return contactsElement
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
                return item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : null;
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

        private static string? ReadStringProperty(JsonElement item, string propertyName)
        {
            return item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static IReadOnlyList<EmployeeBoxItem> ReadEmployees(ReferenceDataRow row)
        {
            if (!row.Values.TryGetValue("employees", out var employeesElement)
                || employeesElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return employeesElement
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
                FullName = ReadStringProperty(item, "full_name")
                    ?? ReadStringProperty(item, "name")
                    ?? ReadNestedStringProperty(item, "person", "full_name")
                    ?? ReadNestedStringProperty(item, "person", "name")
                    ?? string.Empty,
                Position = ReadStringProperty(item, "position")
                    ?? ReadNestedStringProperty(item, "position", "name")
                    ?? string.Empty,
                Contacts = ReadEmployeeContacts(item),
                Description = ReadStringProperty(item, "description") ?? string.Empty,
                IsActive = ReadBooleanProperty(item, "used") ?? ReadBooleanProperty(item, "activated") ?? true
            };
        }

        private static IReadOnlyList<string> ReadEmployeeContacts(JsonElement item)
        {
            if (item.TryGetProperty("contacts", out var contactsElement))
            {
                return ReadContacts(contactsElement);
            }

            if (item.TryGetProperty("person", out var personElement)
                && personElement.ValueKind == JsonValueKind.Object
                && personElement.TryGetProperty("contacts", out contactsElement))
            {
                return ReadContacts(contactsElement);
            }

            return [];
        }

        private static string? ReadNestedStringProperty(JsonElement item, string propertyName, string nestedPropertyName)
        {
            return item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty(propertyName, out var nested)
                ? ReadStringProperty(nested, nestedPropertyName)
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

        private static string? FormatPart(string label, string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : $"{label}: {value}";
        }

        private static string? GetText(ReferenceDataRow row, params string[] fieldKeys)
        {
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
    }
}
