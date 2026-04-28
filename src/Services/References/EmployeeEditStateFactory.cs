using System.Text.Json;
using CbsContractsDesktopClient.Models.References;

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
            if (!row.Values.TryGetValue("person", out var personElement)
                || personElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            if (personElement.TryGetProperty("contacts", out var contactsElement)
                && contactsElement.ValueKind == JsonValueKind.Array)
            {
                return contactsElement
                    .EnumerateArray()
                    .Select(ReadContact)
                    .Where(static contact => contact is not null)
                    .Cast<EmployeeContactEditItem>()
                    .ToList();
            }

            if (personElement.TryGetProperty("person_contacts_attributes", out var attributesElement)
                && attributesElement.ValueKind == JsonValueKind.Array)
            {
                return attributesElement
                    .EnumerateArray()
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

        private static JsonElement? TryGetValue(JsonElement element, string propertyName)
        {
            return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value)
                ? value
                : null;
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }

        private static long? TryGetLong(JsonElement? element)
        {
            if (element is null)
            {
                return null;
            }

            var value = element.Value;
            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt64(out var int64Value) => int64Value,
                JsonValueKind.String when long.TryParse(value.GetString(), out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static long? TryGetLong(object? value)
        {
            return value switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string text when long.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static int? TryGetInt(object? value)
        {
            return value switch
            {
                int int32Value => int32Value,
                long int64Value => (int)int64Value,
                decimal decimalValue => (int)decimalValue,
                string text when int.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static bool? TryGetBool(object? value)
        {
            return value switch
            {
                bool boolValue => boolValue,
                string text when bool.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
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
