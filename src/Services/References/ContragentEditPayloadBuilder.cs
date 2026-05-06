using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.References;

namespace CbsContractsDesktopClient.Services.References
{
    public static class ContragentEditPayloadBuilder
    {
        public static IReadOnlyDictionary<string, object?> BuildForCreate(ContragentEditViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            Validate(viewModel);

            var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["obj_uuid"] = string.IsNullOrWhiteSpace(viewModel.State.ObjUuid)
                    ? Guid.NewGuid().ToString()
                    : viewModel.State.ObjUuid,
                ["contragent_organizations_attributes"] = new object?[]
                {
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["list_key"] = Guid.NewGuid().ToString(),
                        ["used"] = true,
                        ["organization_attributes"] = BuildOrganizationAttributes(viewModel, includeAll: true)
                    }
                }
            };

            AppendText(request, "description", viewModel.Description);
            AppendBankFields(request, viewModel);
            AppendRealAddressForCreate(request, viewModel);
            AppendContactsForCreate(request, ParseContacts(viewModel.ContactsText));
            return request;
        }

        public static IReadOnlyDictionary<string, object?> BuildForUpdate(ContragentEditViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            Validate(viewModel);

            if (viewModel.State.Id is null)
            {
                throw new InvalidOperationException("Contragent id is missing for save.");
            }

            var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = viewModel.State.Id.Value
            };

            AppendChangedText(request, "description", viewModel.Description, viewModel.State.Description);
            AppendBankFieldsIfChanged(request, viewModel);
            AppendOrganizationPayloadIfChanged(request, viewModel);
            AppendRealAddressIfChanged(request, viewModel);
            AppendContactDeltaIfChanged(request, viewModel);
            return request;
        }

        private static void AppendOrganizationPayloadIfChanged(
            Dictionary<string, object?> request,
            ContragentEditViewModel viewModel)
        {
            var requisitesAttributes = BuildRegistrationUsageAttributes(viewModel).ToList();
            var organizationAttributes = BuildChangedOrganizationAttributes(viewModel);
            if (organizationAttributes.Count == 0 && requisitesAttributes.Count == 0)
            {
                return;
            }

            if (organizationAttributes.Count > 0)
            {
                var activeRegistration = viewModel.ActiveRegistration;
                var currentRequisites = requisitesAttributes.FirstOrDefault(attribute =>
                    SameIds(attribute.TryGetValue("id", out var id) ? id : null, activeRegistration?.Id ?? viewModel.State.RequisitesId));
                if (currentRequisites is null)
                {
                    currentRequisites = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    if (activeRegistration?.Id is long activeRequisitesId)
                    {
                        currentRequisites["id"] = activeRequisitesId;
                    }
                    else if (viewModel.State.RequisitesId is long requisitesIdForUpdate)
                    {
                        currentRequisites["id"] = requisitesIdForUpdate;
                    }

                    if (!string.IsNullOrWhiteSpace(activeRegistration?.ListKey))
                    {
                        currentRequisites["list_key"] = activeRegistration.ListKey;
                    }
                    else if (!string.IsNullOrWhiteSpace(viewModel.State.RequisitesListKey))
                    {
                        currentRequisites["list_key"] = viewModel.State.RequisitesListKey;
                    }

                    requisitesAttributes.Add(currentRequisites);
                }

                var organizationId = activeRegistration?.OrganizationId ?? viewModel.State.OrganizationId;
                if (organizationId is long value)
                {
                    organizationAttributes["id"] = value;
                }

                currentRequisites["organization_attributes"] = organizationAttributes;
            }

            request["contragent_organizations_attributes"] = requisitesAttributes.ToArray();
        }

        private static Dictionary<string, object?> BuildOrganizationAttributes(
            ContragentEditViewModel viewModel,
            bool includeAll)
        {
            var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            AppendText(attributes, "name", viewModel.Name);
            AppendText(attributes, "inn", viewModel.Inn);
            AppendText(attributes, "kpp", viewModel.Kpp);
            AppendText(attributes, "division", viewModel.Division);
            attributes["ownership_id"] = viewModel.SelectedOwnershipId;
            AppendText(attributes, "full_name", viewModel.FullName);
            AppendText(attributes, "ogrn", viewModel.Ogrn);
            AppendText(attributes, "okopf", viewModel.Okopf);
            AppendText(attributes, "okfc", viewModel.Okfc);
            AppendText(attributes, "okogu", viewModel.Okogu);
            AppendText(attributes, "okpo", viewModel.Okpo);
            AppendText(attributes, "oktmo", viewModel.Oktmo);
            AppendText(attributes, "okved", viewModel.Okved);

            return includeAll
                ? attributes
                : attributes.Where(static pair => pair.Value is not null).ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, object?> BuildChangedOrganizationAttributes(ContragentEditViewModel viewModel)
        {
            var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var baseline = viewModel.ActiveRegistration;
            AppendChangedText(attributes, "name", viewModel.Name, baseline?.Name ?? viewModel.State.Name);
            AppendChangedText(attributes, "inn", viewModel.Inn, baseline?.Inn ?? viewModel.State.Inn);
            AppendChangedText(attributes, "kpp", viewModel.Kpp, baseline?.Kpp ?? viewModel.State.Kpp);
            AppendChangedText(attributes, "division", viewModel.Division, baseline?.Division ?? viewModel.State.Division);
            if (viewModel.SelectedOwnershipId != (baseline?.OwnershipId ?? viewModel.State.OwnershipId))
            {
                attributes["ownership_id"] = viewModel.SelectedOwnershipId;
            }

            AppendChangedText(attributes, "full_name", viewModel.FullName, baseline?.FullName ?? viewModel.State.FullName);
            AppendChangedText(attributes, "ogrn", viewModel.Ogrn, baseline?.Ogrn ?? viewModel.State.Ogrn);
            AppendChangedText(attributes, "okopf", viewModel.Okopf, baseline?.Okopf ?? viewModel.State.Okopf);
            AppendChangedText(attributes, "okfc", viewModel.Okfc, baseline?.Okfc ?? viewModel.State.Okfc);
            AppendChangedText(attributes, "okogu", viewModel.Okogu, baseline?.Okogu ?? viewModel.State.Okogu);
            AppendChangedText(attributes, "okpo", viewModel.Okpo, baseline?.Okpo ?? viewModel.State.Okpo);
            AppendChangedText(attributes, "oktmo", viewModel.Oktmo, baseline?.Oktmo ?? viewModel.State.Oktmo);
            AppendChangedText(attributes, "okved", viewModel.Okved, baseline?.Okved ?? viewModel.State.Okved);
            return attributes;
        }

        private static IEnumerable<Dictionary<string, object?>> BuildRegistrationUsageAttributes(
            ContragentEditViewModel viewModel)
        {
            foreach (var registration in viewModel.OrganizationHistory.Where(static item => item.IsMarkedForDestroy))
            {
                var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                if (registration.Id is long id)
                {
                    attributes["id"] = id;
                }

                if (!string.IsNullOrWhiteSpace(registration.ListKey))
                {
                    attributes["list_key"] = registration.ListKey;
                }

                attributes["_destroy"] = "1";
                yield return attributes;
            }

            foreach (var registration in viewModel.OrganizationHistory.Where(static item => item.IsActive != item.OriginalIsActive))
            {
                if (registration.IsMarkedForDestroy)
                {
                    continue;
                }

                var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                if (registration.Id is long id)
                {
                    attributes["id"] = id;
                }

                if (!string.IsNullOrWhiteSpace(registration.ListKey))
                {
                    attributes["list_key"] = registration.ListKey;
                }

                attributes["used"] = registration.IsActive;
                yield return attributes;
            }
        }

        private static void AppendRealAddressForCreate(
            Dictionary<string, object?> request,
            ContragentEditViewModel viewModel)
        {
            if (string.IsNullOrWhiteSpace(viewModel.AddressReal) && viewModel.SelectedRegionId is null)
            {
                return;
            }

            request["contragent_addresses_attributes"] = new object?[]
            {
                BuildNewRealAddress(viewModel)
            };
        }

        private static void AppendRealAddressIfChanged(
            Dictionary<string, object?> request,
            ContragentEditViewModel viewModel)
        {
            var addressChanged = !Same(viewModel.AddressReal, viewModel.State.AddressReal);
            var regionChanged = viewModel.SelectedRegionId != viewModel.State.RegionId;
            var addressIdChanged = viewModel.SelectedAddressId != viewModel.State.AddressRealAddressId;
            if (!addressChanged && !regionChanged && !addressIdChanged)
            {
                return;
            }

            if (viewModel.SelectedAddressId is long selectedAddressId && viewModel.State.RealAddressId is long realAddressId)
            {
                request["contragent_addresses_attributes"] = new object?[]
                {
                    BuildExistingRealAddress(viewModel, realAddressId, selectedAddressId)
                };
                return;
            }

            var attributes = new List<object?>();
            if (viewModel.State.RealAddressId is long oldRealAddressId)
            {
                attributes.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["id"] = oldRealAddressId,
                    ["list_key"] = viewModel.State.RealAddressListKey,
                    ["_destroy"] = "1"
                });
            }

            if (!string.IsNullOrWhiteSpace(viewModel.AddressReal) || viewModel.SelectedRegionId is not null)
            {
                attributes.Add(BuildNewRealAddress(viewModel));
            }

            request["contragent_addresses_attributes"] = attributes.ToArray();
        }

        private static Dictionary<string, object?> BuildNewRealAddress(ContragentEditViewModel viewModel)
        {
            var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["list_key"] = Guid.NewGuid().ToString(),
                ["kind"] = "real",
                ["used"] = true
            };

            if (viewModel.SelectedAddressId is long addressId)
            {
                attributes["address_id"] = addressId;
                return attributes;
            }

            attributes["address_attributes"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["value"] = NullIfWhiteSpace(viewModel.AddressReal),
                ["area_id"] = viewModel.SelectedRegionId
            };

            return attributes;
        }

        private static Dictionary<string, object?> BuildExistingRealAddress(
            ContragentEditViewModel viewModel,
            long realAddressId,
            long addressId)
        {
            var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = realAddressId,
                ["list_key"] = viewModel.State.RealAddressListKey,
                ["kind"] = "real",
                ["used"] = true,
                ["address_id"] = addressId
            };

            if (viewModel.SelectedRegionId != viewModel.State.RegionId)
            {
                attributes["address_attributes"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["area_id"] = viewModel.SelectedRegionId
                };
            }

            return attributes;
        }

        private static void AppendContactsForCreate(
            Dictionary<string, object?> request,
            IReadOnlyList<ContactDraft> contacts)
        {
            if (contacts.Count == 0)
            {
                return;
            }

            request["contragent_contacts_attributes"] = contacts
                .Select(static (contact, index) => BuildContactCreateAttribute(contact, index))
                .ToArray();
        }

        private static void AppendContactDeltaIfChanged(
            Dictionary<string, object?> request,
            ContragentEditViewModel viewModel)
        {
            var delta = BuildContactDelta(viewModel.State.Contacts, ParseContacts(viewModel.ContactsText));
            if (delta.Length > 0)
            {
                request["contragent_contacts_attributes"] = delta;
            }
        }

        private static object?[] BuildContactDelta(
            IReadOnlyList<EmployeeContactEditItem> originalContacts,
            IReadOnlyList<ContactDraft> currentContacts)
        {
            var originalByValue = originalContacts
                .GroupBy(static contact => NormalizeContactValue(contact.Value), StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.CurrentCultureIgnoreCase);
            var currentByValue = currentContacts
                .GroupBy(static contact => NormalizeContactValue(contact.Value), StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.CurrentCultureIgnoreCase);
            var delta = new List<object?>();

            foreach (var current in currentByValue)
            {
                if (!originalByValue.ContainsKey(current.Key))
                {
                    delta.Add(BuildContactCreateAttribute(current.Value, delta.Count));
                }
            }

            foreach (var original in originalByValue)
            {
                if (!currentByValue.ContainsKey(original.Key) && original.Value.Id is long id)
                {
                    delta.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["id"] = id,
                        ["list_key"] = original.Value.ListKey,
                        ["_destroy"] = "1"
                    });
                }
            }

            return delta.ToArray();
        }

        private static Dictionary<string, object?> BuildContactCreateAttribute(ContactDraft contact, int index)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["used"] = true,
                ["priority"] = index,
                ["list_key"] = Guid.NewGuid().ToString(),
                ["contact_attributes"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["value"] = contact.Value,
                    ["type"] = contact.Type
                }
            };
        }

        private static void AppendBankFields(Dictionary<string, object?> request, ContragentEditViewModel viewModel)
        {
            AppendText(request, "bank_name", viewModel.BankName);
            AppendText(request, "bank_bik", viewModel.BankBik);
            AppendText(request, "bank_account", viewModel.BankAccount);
            AppendText(request, "bank_cor_account", viewModel.BankCorAccount);
        }

        private static void AppendBankFieldsIfChanged(Dictionary<string, object?> request, ContragentEditViewModel viewModel)
        {
            AppendChangedText(request, "bank_name", viewModel.BankName, viewModel.State.BankName);
            AppendChangedText(request, "bank_bik", viewModel.BankBik, viewModel.State.BankBik);
            AppendChangedText(request, "bank_account", viewModel.BankAccount, viewModel.State.BankAccount);
            AppendChangedText(request, "bank_cor_account", viewModel.BankCorAccount, viewModel.State.BankCorAccount);
        }

        private static IReadOnlyList<ContactDraft> ParseContacts(string contactsText)
        {
            var contacts = new List<ContactDraft>();
            foreach (var value in contactsText
                .Split([Environment.NewLine, "\n", ";", ","], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.CurrentCultureIgnoreCase))
            {
                if (!ContactTypeClassifier.TryClassify(value, out var match))
                {
                    throw new InvalidOperationException($"Тип контакта '{value}' не определен.");
                }

                contacts.Add(new ContactDraft(value.Trim(), match.Type));
            }

            return contacts;
        }

        private static void Validate(ContragentEditViewModel viewModel)
        {
            if (string.IsNullOrWhiteSpace(viewModel.Inn))
            {
                throw new InvalidOperationException("Поле 'ИНН' обязательно.");
            }

            if (viewModel.SelectedOwnershipId is null)
            {
                throw new InvalidOperationException("Поле 'Форма' обязательно.");
            }

            if (string.IsNullOrWhiteSpace(viewModel.Name))
            {
                throw new InvalidOperationException("Поле 'Наименование' обязательно.");
            }

            _ = ParseContacts(viewModel.ContactsText);
        }

        private static void AppendText(Dictionary<string, object?> request, string key, string value)
        {
            request[key] = NullIfWhiteSpace(value);
        }

        private static void AppendChangedText(
            Dictionary<string, object?> request,
            string key,
            string value,
            string originalValue)
        {
            if (!Same(value, originalValue))
            {
                request[key] = NullIfWhiteSpace(value);
            }
        }

        private static object? NullIfWhiteSpace(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.CurrentCulture);
        }

        private static bool SameIds(object? left, object? right)
        {
            return left?.ToString() == right?.ToString();
        }

        private static string NormalizeContactValue(string value)
        {
            return value.Trim();
        }

        private sealed record ContactDraft(string Value, string Type);
    }
}
