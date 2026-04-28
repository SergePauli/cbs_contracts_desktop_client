using System.Globalization;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.ViewModels.References;

namespace CbsContractsDesktopClient.Services.References
{
    public static class EmployeeEditPayloadBuilder
    {
        public static IReadOnlyDictionary<string, object?> BuildForCreate(EmployeeEditViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var personName = CollapseWhitespace(viewModel.PersonName);
            var positionName = viewModel.PositionName.Trim();
            var contacts = ParseContacts(viewModel.ContactsText);
            var priority = ParsePriority(viewModel.PriorityText);

            Validate(personName, positionName, viewModel.SelectedContragentOption, viewModel.ContragentName, contacts);

            var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["list_key"] = Guid.NewGuid().ToString(),
                ["used"] = viewModel.IsUsed,
                ["contragent_id"] = ExtractOptionId(viewModel.SelectedContragentOption),
                ["person_attributes"] = BuildPersonAttributesForCreate(personName, contacts)
            };

            if (priority.HasValue)
            {
                request["priority"] = priority.Value;
            }

            if (!string.IsNullOrWhiteSpace(viewModel.Description))
            {
                request["description"] = viewModel.Description.Trim();
            }

            AppendPositionPayload(request, positionName, viewModel.SelectedPositionOption);
            return request;
        }

        public static IReadOnlyDictionary<string, object?> BuildForUpdate(EmployeeEditViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var state = viewModel.State;
            if (state.Id is null)
            {
                throw new InvalidOperationException("Employee id is missing for save.");
            }

            var personName = CollapseWhitespace(viewModel.PersonName);
            var positionName = viewModel.PositionName.Trim();
            var contacts = ParseContacts(viewModel.ContactsText);
            var priority = ParsePriority(viewModel.PriorityText);

            Validate(personName, positionName, viewModel.SelectedContragentOption, viewModel.ContragentName, contacts);

            var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = state.Id.Value
            };

            if (!string.IsNullOrWhiteSpace(state.ListKey))
            {
                request["list_key"] = state.ListKey;
            }

            if (viewModel.IsUsed != state.IsUsed)
            {
                request["used"] = viewModel.IsUsed;
            }

            if (priority != state.Priority)
            {
                request["priority"] = priority;
            }

            if (!string.Equals(viewModel.Description.Trim(), state.Description.Trim(), StringComparison.CurrentCulture))
            {
                request["description"] = string.IsNullOrWhiteSpace(viewModel.Description)
                    ? null
                    : viewModel.Description.Trim();
            }

            AppendContragentPayloadIfChanged(request, viewModel);
            AppendPositionPayloadIfChanged(request, state.PositionName, positionName, viewModel.SelectedPositionOption);

            var personAttributes = BuildPersonAttributesIfChanged(state, personName, contacts);
            if (personAttributes is not null)
            {
                request["person_attributes"] = personAttributes;
            }

            return request;
        }

        private static void AppendContragentPayloadIfChanged(
            Dictionary<string, object?> request,
            EmployeeEditViewModel viewModel)
        {
            var selectedContragentId = ExtractOptionId(viewModel.SelectedContragentOption);
            if (selectedContragentId is null
                && string.Equals(
                    viewModel.ContragentName.Trim(),
                    viewModel.State.ContragentName.Trim(),
                    StringComparison.CurrentCultureIgnoreCase))
            {
                return;
            }

            if (selectedContragentId is long id && id != viewModel.State.ContragentId)
            {
                request["contragent_id"] = id;
            }
        }

        private static void AppendPositionPayloadIfChanged(
            Dictionary<string, object?> request,
            string initialPositionName,
            string currentPositionName,
            CbsTableFilterOptionDefinition? selectedPositionOption)
        {
            if (string.Equals(
                    currentPositionName.Trim(),
                    initialPositionName.Trim(),
                    StringComparison.CurrentCultureIgnoreCase))
            {
                return;
            }

            AppendPositionPayload(request, currentPositionName, selectedPositionOption);
        }

        private static void AppendPositionPayload(
            Dictionary<string, object?> request,
            string positionName,
            CbsTableFilterOptionDefinition? selectedPositionOption)
        {
            var positionId = ExtractOptionId(selectedPositionOption);
            if (positionId is long id)
            {
                request["position_id"] = id;
                return;
            }

            request["position_attributes"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = CapitalizeFirst(positionName)
            };
        }

        private static Dictionary<string, object?> BuildPersonAttributesForCreate(
            string personName,
            IReadOnlyList<ContactDraft> contacts)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["person_names_attributes"] = new object?[]
                {
                    BuildNameAttribute(personName)
                },
                ["person_contacts_attributes"] = contacts
                    .Select(static (contact, index) => BuildContactCreateAttribute(contact, index))
                    .ToArray()
            };
        }

        private static Dictionary<string, object?>? BuildPersonAttributesIfChanged(
            EmployeeEditDialogState state,
            string personName,
            IReadOnlyList<ContactDraft> contacts)
        {
            var personNameChanged = !string.Equals(
                personName,
                CollapseWhitespace(state.PersonName),
                StringComparison.CurrentCulture);
            var contactAttributes = BuildContactDelta(state.Contacts, contacts);

            if (!personNameChanged && contactAttributes.Length == 0)
            {
                return null;
            }

            var personAttributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (state.PersonId is long personId)
            {
                personAttributes["id"] = personId;
            }

            personAttributes["person_names_attributes"] = personNameChanged
                ? new object?[] { BuildNameAttribute(personName) }
                : Array.Empty<object?>();
            personAttributes["person_contacts_attributes"] = contactAttributes;
            return personAttributes;
        }

        private static Dictionary<string, object?> BuildNameAttribute(string personName)
        {
            var parts = personName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var namingAttributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["surname"] = CapitalizeFirst(parts[0]),
                ["name"] = CapitalizeFirst(parts[1])
            };

            if (parts.Length > 2)
            {
                namingAttributes["patrname"] = CapitalizeFirst(parts[2]);
            }

            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["used"] = true,
                ["list_key"] = Guid.NewGuid().ToString(),
                ["naming_attributes"] = namingAttributes
            };
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

        private static int? ParsePriority(string priorityText)
        {
            var value = priorityText.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var priority)
                || int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out priority))
            {
                return priority;
            }

            throw new InvalidOperationException("Поле 'Пор.' должно быть целым числом.");
        }

        private static void Validate(
            string personName,
            string positionName,
            CbsTableFilterOptionDefinition? selectedContragentOption,
            string contragentName,
            IReadOnlyList<ContactDraft> contacts)
        {
            var personParts = personName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (personParts.Length < 2)
            {
                throw new InvalidOperationException("Поле 'ФИО' должно содержать минимум фамилию и имя.");
            }

            if (string.IsNullOrWhiteSpace(positionName))
            {
                throw new InvalidOperationException("Поле 'Должность' обязательно.");
            }

            if (ExtractOptionId(selectedContragentOption) is null && string.IsNullOrWhiteSpace(contragentName))
            {
                throw new InvalidOperationException("Поле 'Контрагент' обязательно.");
            }

            if (ExtractOptionId(selectedContragentOption) is null)
            {
                throw new InvalidOperationException("Выберите контрагента из списка.");
            }

            if (contacts.Count == 0)
            {
                throw new InvalidOperationException("Должен быть хотя бы один контакт.");
            }
        }

        private static long? ExtractOptionId(CbsTableFilterOptionDefinition? option)
        {
            return option?.Value switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string text when long.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static string CapitalizeFirst(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            return trimmed.Length == 1
                ? trimmed.ToUpper(CultureInfo.CurrentCulture)
                : char.ToUpper(trimmed[0], CultureInfo.CurrentCulture) + trimmed[1..];
        }

        private static string CollapseWhitespace(string value)
        {
            return string.Join(
                " ",
                value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private static string NormalizeContactValue(string value)
        {
            return value.Trim();
        }

        private sealed record ContactDraft(string Value, string Type);
    }
}
