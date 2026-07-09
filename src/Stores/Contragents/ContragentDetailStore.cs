using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CommunityToolkit.Mvvm.ComponentModel;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.Stores.Contragents
{
    public partial class ContragentDetailStore : ObservableObject
    {
        [ObservableProperty]
        public partial TableDataRow? Contragent { get; private set; }

        [ObservableProperty]
        public partial string Name { get; private set; } = string.Empty;

        [ObservableProperty]
        public partial IReadOnlyList<string> Contacts { get; private set; } = [];

        [ObservableProperty]
        public partial IReadOnlyList<EmployeeBoxItem> Employees { get; private set; } = [];

        public void SetContragent(TableDataRow? row)
        {
            Contragent = row;
            Name = ReadName(row);
            Contacts = ReadContragentContacts(row);
            Employees = ReadEmployees(row);
        }

        private static string ReadName(TableDataRow? row)
        {
            return TryGetText(row, "name", "requisites.organization.name") ?? string.Empty;
        }

        private static IReadOnlyList<EmployeeBoxItem> ReadEmployees(TableDataRow? row)
        {
            var employees = TryGetFirstArray(row, "employees", "emploees");
            if (employees is null)
            {
                return [];
            }

            return EnumerateObjectArray(employees)
                .Select(ReadEmployee)
                .Where(static employee => !string.IsNullOrWhiteSpace(employee.FullName) || employee.Id is not null)
                .ToList();
        }

        private static EmployeeBoxItem ReadEmployee(JsonElement item)
        {
            return new EmployeeBoxItem
            {
                Id = TryGetLong(item, "id"),
                FullName = ReadFullEmployeeName(item) ?? string.Empty,
                Position = TryGetString(item, "position")
                    ?? ReadNestedString(item, "position", "name")
                    ?? string.Empty,
                Contacts = ReadEmployeeContacts(item),
                Description = TryGetString(item, "description") ?? string.Empty,
                IsActive = TryGetBool(item, "used") ?? TryGetBool(item, "activated") ?? true
            };
        }

        private static string? ReadFullEmployeeName(JsonElement item)
        {
            return TryGetString(item, "full_name")
                ?? ReadNestedString(item, "person", "full_name")
                ?? ReadNestedString(item, "employee", "full_name")
                ?? ReadNestedString(item, "employee", "person", "full_name")
                ?? TryGetString(item, "name")
                ?? ReadNestedString(item, "person", "name")
                ?? ReadNestedString(item, "employee", "name")
                ?? ReadNestedString(item, "employee", "person", "name")
                ?? TryGetString(item, "title");
        }

        private static IReadOnlyList<string> ReadContragentContacts(TableDataRow? row)
        {
            var contacts = TryGetArray(row, "contacts");
            return contacts is null ? [] : ReadContactValues(contacts.Value);
        }

        private static IReadOnlyList<string> ReadEmployeeContacts(JsonElement item)
        {
            var contacts = TryGetArray(item, "contacts")
                ?? ReadNestedArray(item, "person", "contacts");
            return contacts is null ? [] : ReadContactValues(contacts.Value);
        }

        private static string? ReadNestedString(JsonElement item, params string[] path)
        {
            var value = ReadNestedValue(item, path);
            return value is null ? null : TryGetString(value.Value, path[^1]);
        }

        private static JsonElement? ReadNestedArray(JsonElement item, params string[] path)
        {
            var value = ReadNestedValue(item, path);
            return value is null ? null : TryGetArray(value.Value, path[^1]);
        }

        private static JsonElement? ReadNestedValue(JsonElement item, IReadOnlyList<string> path)
        {
            if (path.Count < 2)
            {
                return null;
            }

            var current = item;
            for (var index = 0; index < path.Count - 1; index++)
            {
                var next = TryGetObject(current, path[index]);
                if (next is null)
                {
                    return null;
                }

                current = next.Value;
            }

            return current;
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
                ?? TryGetString(item, "value")
                ?? TryGetString(item, "name");
        }

        private static string? ReadNestedContactValue(JsonElement item, string propertyName)
        {
            var nested = TryGetObject(item, propertyName);
            return nested is null
                ? null
                : TryGetString(nested.Value, "value") ?? TryGetString(nested.Value, "name");
        }
    }
}
