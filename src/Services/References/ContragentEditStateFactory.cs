using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.References
{
    public static class ContragentEditStateFactory
    {
        public static ContragentEditDialogState Create(
            ReferenceDefinition definition,
            bool isCreateMode,
            ReferenceDataRow? sourceRow,
            IReadOnlyList<CbsTableFilterOptionDefinition>? ownershipOptions = null,
            IReadOnlyList<CbsTableFilterOptionDefinition>? regionOptions = null)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (isCreateMode || sourceRow is null)
            {
                return new ContragentEditDialogState
                {
                    Definition = definition,
                    IsCreateMode = true,
                    ObjUuid = Guid.NewGuid().ToString(),
                    OwnershipOptions = ownershipOptions ?? [],
                    RegionOptions = regionOptions ?? []
                };
            }

            var contacts = ReadContacts(sourceRow);
            var organizationHistory = ReadOrganizationHistory(sourceRow);
            return new ContragentEditDialogState
            {
                Definition = definition,
                IsCreateMode = false,
                Id = TryGetLong(sourceRow.GetValue("id")),
                ObjUuid = GetText(sourceRow, "obj_uuid"),
                RequisitesId = TryGetLong(sourceRow.GetValue("requisites.id")),
                RequisitesListKey = GetText(sourceRow, "requisites.list_key"),
                OrganizationId = TryGetLong(sourceRow.GetValue("requisites.organization.id")),
                Inn = GetText(sourceRow, "requisites.organization.inn"),
                Kpp = GetText(sourceRow, "requisites.organization.kpp"),
                Division = GetText(sourceRow, "requisites.organization.division"),
                OwnershipId = TryGetLong(sourceRow.GetValue("requisites.organization.ownership.id")),
                OwnershipName = GetText(sourceRow, "requisites.organization.ownership.name"),
                Name = GetText(sourceRow, "requisites.organization.name"),
                RegionId = TryGetLong(sourceRow.GetValue("region.id") ?? sourceRow.GetValue("real_addr.address.area_id")),
                RegionName = GetText(sourceRow, "region.name"),
                RealAddressId = TryGetLong(sourceRow.GetValue("real_addr.id")),
                RealAddressListKey = GetText(sourceRow, "real_addr.list_key"),
                AddressRealAddressId = TryGetLong(sourceRow.GetValue("real_addr.address.id") ?? sourceRow.GetValue("real_addr.address_id")),
                AddressReal = GetText(sourceRow, "real_addr.address.value"),
                FullName = GetText(sourceRow, "requisites.organization.full_name"),
                Description = GetRawText(sourceRow, "description"),
                Ogrn = GetText(sourceRow, "requisites.organization.ogrn"),
                Okfc = GetText(sourceRow, "requisites.organization.okfc"),
                Okopf = GetText(sourceRow, "requisites.organization.okopf"),
                Okpo = GetText(sourceRow, "requisites.organization.okpo"),
                Okogu = GetText(sourceRow, "requisites.organization.okogu"),
                Okved = GetText(sourceRow, "requisites.organization.okved"),
                Oktmo = GetText(sourceRow, "requisites.organization.oktmo"),
                BankName = GetText(sourceRow, "bank_name"),
                BankBik = GetText(sourceRow, "bank_bik"),
                BankAccount = GetText(sourceRow, "bank_account"),
                BankCorAccount = GetText(sourceRow, "bank_cor_account"),
                Contacts = contacts,
                ContactsText = string.Join(Environment.NewLine, contacts.Select(static contact => contact.Value)),
                OrganizationHistory = organizationHistory,
                OwnershipOptions = ownershipOptions ?? [],
                RegionOptions = regionOptions ?? [],
                InitialAddressOption = CreateInitialAddressOption(
                    TryGetLong(sourceRow.GetValue("real_addr.address.id") ?? sourceRow.GetValue("real_addr.address_id")),
                    GetText(sourceRow, "real_addr.address.value"))
            };
        }

        private static CbsTableFilterOptionDefinition? CreateInitialAddressOption(long? id, string value)
        {
            return id is null || string.IsNullOrWhiteSpace(value)
                ? null
                : new CbsTableFilterOptionDefinition
                {
                    Value = id.Value,
                    Label = value
                };
        }

        private static IReadOnlyList<ContragentOrganizationHistoryItem> ReadOrganizationHistory(ReferenceDataRow row)
        {
            var organizationsElement = TryGetArray(row, "organizations")
                ?? TryGetArray(row, "contragent.organizations")
                ?? TryGetArray(row, "contragent_organizations")
                ?? TryGetArray(row, "contragent.contragent_organizations")
                ?? TryGetNestedArray(row, "contragent", "organizations")
                ?? TryGetNestedArray(row, "contragent", "contragent_organizations");
            if (organizationsElement is null)
            {
                var currentOrganization = ReadCurrentOrganizationHistoryItem(row);
                return currentOrganization is null
                    ? []
                    : [currentOrganization];
            }

            return organizationsElement.Value
                .EnumerateArray()
                .Where(static item => item.ValueKind == JsonValueKind.Object)
                .Select(ReadOrganizationHistoryItem)
                .OrderByDescending(static item => item.IsActive)
                .ThenByDescending(static item => item.UpdatedAt)
                .ThenByDescending(static item => item.CreatedAt)
                .ToList();
        }

        private static ContragentOrganizationHistoryItem? ReadCurrentOrganizationHistoryItem(ReferenceDataRow row)
        {
            var id = TryGetLong(row.GetValue("requisites.organization.id"));
            var name = GetText(row, "requisites.organization.name");
            var fullName = GetText(row, "requisites.organization.full_name");
            if (id is null && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            return new ContragentOrganizationHistoryItem
            {
                Id = TryGetLong(row.GetValue("requisites.id")),
                OrganizationId = id,
                Name = name,
                FullName = fullName,
                Inn = GetText(row, "requisites.organization.inn"),
                Kpp = GetText(row, "requisites.organization.kpp"),
                Division = GetText(row, "requisites.organization.division"),
                OwnershipId = TryGetLong(row.GetValue("requisites.organization.ownership.id")),
                OwnershipName = GetText(row, "requisites.organization.ownership.name"),
                OwnershipCode =
                    GetTextOrNull(row, "requisites.organization.ownership.code")
                    ?? GetTextOrNull(row, "requisites.organization.ownership.okopf")
                    ?? GetTextOrNull(row, "requisites.organization.okopf")
                    ?? string.Empty,
                Ogrn = GetText(row, "requisites.organization.ogrn"),
                Okfc = GetText(row, "requisites.organization.okfc"),
                Okopf = GetText(row, "requisites.organization.okopf"),
                Okpo = GetText(row, "requisites.organization.okpo"),
                Okogu = GetText(row, "requisites.organization.okogu"),
                Okved = GetText(row, "requisites.organization.okved"),
                Oktmo = GetText(row, "requisites.organization.oktmo"),
                ListKey = GetText(row, "requisites.list_key"),
                OriginalIsActive = true,
                IsActive = true
            };
        }

        private static ContragentOrganizationHistoryItem ReadOrganizationHistoryItem(JsonElement item)
        {
            var organization = GetObjectProperty(item, "organization")
                ?? GetObjectProperty(item, "organization_attributes")
                ?? item;
            var ownership = GetObjectProperty(organization, "ownership");

            return new ContragentOrganizationHistoryItem
            {
                Id = TryGetLong(TryGetValue(item, "id")),
                OrganizationId = TryGetLong(TryGetValue(organization, "id")) ?? TryGetLong(TryGetValue(item, "organization_id")),
                Name = TryGetString(organization, "name") ?? string.Empty,
                FullName = TryGetString(organization, "full_name") ?? string.Empty,
                Inn = TryGetString(organization, "inn") ?? string.Empty,
                Kpp = TryGetString(organization, "kpp") ?? string.Empty,
                Division = TryGetString(organization, "division") ?? string.Empty,
                OwnershipId = TryGetLong(TryGetValue(organization, "ownership_id")) ?? (ownership is null ? null : TryGetLong(TryGetValue(ownership.Value, "id"))),
                OwnershipName = ownership is null ? string.Empty : TryGetString(ownership.Value, "name") ?? TryGetString(ownership.Value, "full_name") ?? string.Empty,
                OwnershipCode = ownership is null ? TryGetString(organization, "okopf") ?? string.Empty : TryGetString(ownership.Value, "code") ?? TryGetString(ownership.Value, "okopf") ?? TryGetString(organization, "okopf") ?? string.Empty,
                Ogrn = TryGetString(organization, "ogrn") ?? string.Empty,
                Okfc = TryGetString(organization, "okfc") ?? string.Empty,
                Okopf = TryGetString(organization, "okopf") ?? string.Empty,
                Okpo = TryGetString(organization, "okpo") ?? string.Empty,
                Okogu = TryGetString(organization, "okogu") ?? string.Empty,
                Okved = TryGetString(organization, "okved") ?? string.Empty,
                Oktmo = TryGetString(organization, "oktmo") ?? string.Empty,
                ListKey = TryGetString(item, "list_key") ?? string.Empty,
                CreatedAt = TryGetString(item, "created_at") ?? TryGetString(organization, "created_at") ?? string.Empty,
                UpdatedAt = TryGetString(item, "updated_at") ?? TryGetString(organization, "updated_at") ?? string.Empty,
                OriginalIsActive = TryGetBool(item, "used") ?? TryGetBool(item, "active") ?? true,
                IsActive = TryGetBool(item, "used") ?? TryGetBool(item, "active") ?? true
            };
        }

        private static JsonElement? TryGetArray(ReferenceDataRow row, string propertyName)
        {
            return row.Values.TryGetValue(propertyName, out var value) && value.ValueKind == JsonValueKind.Array
                ? value
                : null;
        }

        private static JsonElement? TryGetNestedArray(ReferenceDataRow row, string propertyName, string nestedPropertyName)
        {
            if (!row.Values.TryGetValue(propertyName, out var value)
                || value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty(nestedPropertyName, out var nested)
                || nested.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return nested;
        }

        private static JsonElement? GetObjectProperty(JsonElement element, string propertyName)
        {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.Object
                ? value
                : null;
        }

        private static IReadOnlyList<EmployeeContactEditItem> ReadContacts(ReferenceDataRow row)
        {
            if (!row.Values.TryGetValue("contacts", out var contactsElement)
                || contactsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return contactsElement
                .EnumerateArray()
                .Select(ReadContact)
                .Where(static contact => contact is not null)
                .Cast<EmployeeContactEditItem>()
                .ToList();
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

            return NormalizeSingleLine(value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString());
        }

        private static string GetText(ReferenceDataRow row, string fieldKey)
        {
            return GetTextOrNull(row, fieldKey) ?? string.Empty;
        }

        private static string GetRawText(ReferenceDataRow row, string fieldKey)
        {
            return row.GetValue(fieldKey)?.ToString() ?? string.Empty;
        }

        private static string? GetTextOrNull(ReferenceDataRow row, string fieldKey)
        {
            return NormalizeSingleLine(row.GetValue(fieldKey)?.ToString());
        }

        private static string? NormalizeSingleLine(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return string.Join(
                " ",
                value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private static bool? TryGetBool(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
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
