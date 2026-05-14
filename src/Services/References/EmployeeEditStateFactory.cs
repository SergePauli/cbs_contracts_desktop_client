using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.Services.References
{
    public static class EmployeeEditStateFactory
    {
        public static EmployeeEditDialogState Create(
            ReferenceDefinition definition,
            bool isCreateMode,
            ReferenceDataRow? sourceRow)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (isCreateMode || sourceRow is null)
            {
                return new EmployeeEditDialogState
                {
                    Definition = definition,
                    IsCreateMode = true,
                    IsUsed = true
                };
            }

            var contacts = ReadContacts(sourceRow);
            return new EmployeeEditDialogState
            {
                Definition = definition,
                IsCreateMode = false,
                Id = TryGetLong(sourceRow.GetValue("id")),
                ListKey = sourceRow.GetValue("list_key")?.ToString(),
                PersonId = TryGetLong(sourceRow.GetValue("person.id")),
                PersonName = sourceRow.GetValue("person.full_name")?.ToString() ?? string.Empty,
                PositionId = TryGetLong(sourceRow.GetValue("position.id")),
                PositionName = sourceRow.GetValue("position.name")?.ToString() ?? string.Empty,
                ContragentId = TryGetLong(sourceRow.GetValue("contragent.id")),
                ContragentName =
                    sourceRow.GetValue("contragent.full_name")?.ToString()
                    ?? sourceRow.GetValue("contragent.name")?.ToString()
                    ?? string.Empty,
                IsUsed = TryGetBool(sourceRow.GetValue("used")) ?? true,
                Priority = TryGetInt(sourceRow.GetValue("priority")),
                Description = sourceRow.GetValue("description")?.ToString() ?? string.Empty,
                Contacts = contacts,
                ContactsText = string.Join(Environment.NewLine, contacts.Select(static contact => contact.Value))
            };
        }

        private static IReadOnlyList<EmployeeContactEditItem> ReadContacts(ReferenceDataRow row)
        {
            var personElement = row.Values.TryGetValue("person", out var person)
                && person.ValueKind == JsonValueKind.Object
                ? person
                : (JsonElement?)null;
            if (personElement is null)
            {
                return [];
            }

            if (TryGetArray(personElement.Value, "contacts") is JsonElement contactsElement)
            {
                return EnumerateObjectArray(contactsElement)
                    .Select(ReadContact)
                    .Where(static contact => contact is not null)
                    .Cast<EmployeeContactEditItem>()
                    .ToList();
            }

            if (TryGetArray(personElement.Value, "person_contacts_attributes") is JsonElement attributesElement)
            {
                return EnumerateObjectArray(attributesElement)
                    .Select(ReadContact)
                    .Where(static contact => contact is not null)
                    .Cast<EmployeeContactEditItem>()
                    .ToList();
            }

            return [];
        }

        private static EmployeeContactEditItem? ReadContact(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var contactElement = default(JsonElement);
            var hasContact =
                item.TryGetProperty("contact_attributes", out contactElement)
                || item.TryGetProperty("contact", out contactElement);

            var value =
                TryGetString(contactElement, "value")
                ?? TryGetString(contactElement, "name")
                ?? TryGetString(item, "value")
                ?? TryGetString(item, "name");
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return new EmployeeContactEditItem
            {
                Id = TryGetLong(TryGetValue(item, "id")),
                ListKey = TryGetString(item, "list_key"),
                Value = value.Trim(),
                Type = TryGetString(contactElement, "type") ?? TryGetString(item, "type") ?? InferContactType(value)
            };
        }

        private static string InferContactType(string value)
        {
            var normalized = value.Trim();
            if (normalized.Contains('@') && normalized.Contains('.'))
            {
                return "Email";
            }

            if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "SiteUrl";
            }

            if (normalized.StartsWith('@') || normalized.Contains("t.me/", StringComparison.OrdinalIgnoreCase))
            {
                return "Telegram";
            }

            return "Phone";
        }
    }
}
