using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

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
                ObjUuid = GetSingleLineText(sourceRow, "obj_uuid"),
                RequisitesId = TryGetLong(sourceRow.GetValue("requisites.id")),
                RequisitesListKey = GetSingleLineText(sourceRow, "requisites.list_key"),
                OrganizationId = TryGetLong(sourceRow.GetValue("requisites.organization.id")),
                Inn = GetSingleLineText(sourceRow, "requisites.organization.inn"),
                Kpp = GetSingleLineText(sourceRow, "requisites.organization.kpp"),
                Division = GetSingleLineText(sourceRow, "requisites.organization.division"),
                OwnershipId = TryGetLong(sourceRow.GetValue("requisites.organization.ownership.id")),
                OwnershipName = GetSingleLineText(sourceRow, "requisites.organization.ownership.name"),
                Name = GetSingleLineText(sourceRow, "requisites.organization.name"),
                RegionId = TryGetLong(sourceRow.GetValue("region.id") ?? sourceRow.GetValue("real_addr.address.area_id")),
                RegionName = GetSingleLineText(sourceRow, "region.name"),
                RealAddressId = TryGetLong(sourceRow.GetValue("real_addr.id")),
                RealAddressListKey = GetSingleLineText(sourceRow, "real_addr.list_key"),
                AddressRealAddressId = TryGetLong(sourceRow.GetValue("real_addr.address.id") ?? sourceRow.GetValue("real_addr.address_id")),
                AddressReal = GetSingleLineText(sourceRow, "real_addr.address.value"),
                FullName = GetSingleLineText(sourceRow, "requisites.organization.full_name"),
                Description = GetRawText(sourceRow, "description"),
                Ogrn = GetSingleLineText(sourceRow, "requisites.organization.ogrn"),
                Okfc = GetSingleLineText(sourceRow, "requisites.organization.okfc"),
                Okopf = GetSingleLineText(sourceRow, "requisites.organization.okopf"),
                Okpo = GetSingleLineText(sourceRow, "requisites.organization.okpo"),
                Okogu = GetSingleLineText(sourceRow, "requisites.organization.okogu"),
                Okved = GetSingleLineText(sourceRow, "requisites.organization.okved"),
                Oktmo = GetSingleLineText(sourceRow, "requisites.organization.oktmo"),
                BankName = GetSingleLineText(sourceRow, "bank_name"),
                BankBik = GetSingleLineText(sourceRow, "bank_bik"),
                BankAccount = GetSingleLineText(sourceRow, "bank_account"),
                BankCorAccount = GetSingleLineText(sourceRow, "bank_cor_account"),
                Contacts = contacts,
                ContactsText = string.Join(Environment.NewLine, contacts.Select(static contact => contact.Value)),
                OrganizationHistory = organizationHistory,
                OwnershipOptions = ownershipOptions ?? [],
                RegionOptions = regionOptions ?? [],
                InitialAddressOption = CreateInitialAddressOption(
                    TryGetLong(sourceRow.GetValue("real_addr.address.id") ?? sourceRow.GetValue("real_addr.address_id")),
                    GetSingleLineText(sourceRow, "real_addr.address.value"))
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

            return EnumerateObjectArray(organizationsElement)
                .Select(ReadOrganizationHistoryItem)
                .OrderByDescending(static item => item.IsActive)
                .ThenByDescending(static item => item.UpdatedAt)
                .ThenByDescending(static item => item.CreatedAt)
                .ToList();
        }

        private static ContragentOrganizationHistoryItem? ReadCurrentOrganizationHistoryItem(ReferenceDataRow row)
        {
            var id = TryGetLong(row.GetValue("requisites.organization.id"));
            var name = GetSingleLineText(row, "requisites.organization.name");
            var fullName = GetSingleLineText(row, "requisites.organization.full_name");
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
                Inn = GetSingleLineText(row, "requisites.organization.inn"),
                Kpp = GetSingleLineText(row, "requisites.organization.kpp"),
                Division = GetSingleLineText(row, "requisites.organization.division"),
                OwnershipId = TryGetLong(row.GetValue("requisites.organization.ownership.id")),
                OwnershipName = GetSingleLineText(row, "requisites.organization.ownership.name"),
                OwnershipCode =
                    TryGetSingleLineText(row, "requisites.organization.ownership.code")
                    ?? TryGetSingleLineText(row, "requisites.organization.ownership.okopf")
                    ?? TryGetSingleLineText(row, "requisites.organization.okopf")
                    ?? string.Empty,
                Ogrn = GetSingleLineText(row, "requisites.organization.ogrn"),
                Okfc = GetSingleLineText(row, "requisites.organization.okfc"),
                Okopf = GetSingleLineText(row, "requisites.organization.okopf"),
                Okpo = GetSingleLineText(row, "requisites.organization.okpo"),
                Okogu = GetSingleLineText(row, "requisites.organization.okogu"),
                Okved = GetSingleLineText(row, "requisites.organization.okved"),
                Oktmo = GetSingleLineText(row, "requisites.organization.oktmo"),
                ListKey = GetSingleLineText(row, "requisites.list_key"),
                OriginalIsActive = true,
                IsActive = true
            };
        }

        private static ContragentOrganizationHistoryItem ReadOrganizationHistoryItem(JsonElement item)
        {
            var organization = TryGetObject(item, "organization")
                ?? TryGetObject(item, "organization_attributes")
                ?? item;
            var ownership = TryGetObject(organization, "ownership");

            return new ContragentOrganizationHistoryItem
            {
                Id = TryGetLong(TryGetValue(item, "id")),
                OrganizationId = TryGetLong(TryGetValue(organization, "id")) ?? TryGetLong(TryGetValue(item, "organization_id")),
                Name = TryGetSingleLineString(organization, "name") ?? string.Empty,
                FullName = TryGetSingleLineString(organization, "full_name") ?? string.Empty,
                Inn = TryGetSingleLineString(organization, "inn") ?? string.Empty,
                Kpp = TryGetSingleLineString(organization, "kpp") ?? string.Empty,
                Division = TryGetSingleLineString(organization, "division") ?? string.Empty,
                OwnershipId = TryGetLong(TryGetValue(organization, "ownership_id")) ?? (ownership is null ? null : TryGetLong(TryGetValue(ownership.Value, "id"))),
                OwnershipName = ownership is null ? string.Empty : TryGetSingleLineString(ownership.Value, "name") ?? TryGetSingleLineString(ownership.Value, "full_name") ?? string.Empty,
                OwnershipCode = ownership is null ? TryGetSingleLineString(organization, "okopf") ?? string.Empty : TryGetSingleLineString(ownership.Value, "code") ?? TryGetSingleLineString(ownership.Value, "okopf") ?? TryGetSingleLineString(organization, "okopf") ?? string.Empty,
                Ogrn = TryGetSingleLineString(organization, "ogrn") ?? string.Empty,
                Okfc = TryGetSingleLineString(organization, "okfc") ?? string.Empty,
                Okopf = TryGetSingleLineString(organization, "okopf") ?? string.Empty,
                Okpo = TryGetSingleLineString(organization, "okpo") ?? string.Empty,
                Okogu = TryGetSingleLineString(organization, "okogu") ?? string.Empty,
                Okved = TryGetSingleLineString(organization, "okved") ?? string.Empty,
                Oktmo = TryGetSingleLineString(organization, "oktmo") ?? string.Empty,
                ListKey = TryGetSingleLineString(item, "list_key") ?? string.Empty,
                CreatedAt = TryGetSingleLineString(item, "created_at") ?? TryGetSingleLineString(organization, "created_at") ?? string.Empty,
                UpdatedAt = TryGetSingleLineString(item, "updated_at") ?? TryGetSingleLineString(organization, "updated_at") ?? string.Empty,
                OriginalIsActive = TryGetBool(item, "used") ?? TryGetBool(item, "active") ?? true,
                IsActive = TryGetBool(item, "used") ?? TryGetBool(item, "active") ?? true
            };
        }

        private static IReadOnlyList<EmployeeContactEditItem> ReadContacts(ReferenceDataRow row)
        {
            return EnumerateObjectArray(row, "contacts")
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

            var contactElement = TryGetObject(item, "contact_attributes")
                ?? TryGetObject(item, "contact")
                ?? default(JsonElement);

            var value =
                TryGetSingleLineString(contactElement, "value")
                ?? TryGetSingleLineString(contactElement, "name")
                ?? TryGetSingleLineString(item, "value")
                ?? TryGetSingleLineString(item, "name");
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return new EmployeeContactEditItem
            {
                Id = TryGetLong(TryGetValue(item, "id")),
                ListKey = TryGetSingleLineString(item, "list_key"),
                Value = value.Trim(),
                Type = TryGetSingleLineString(contactElement, "type") ?? TryGetSingleLineString(item, "type") ?? InferContactType(value)
            };
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
