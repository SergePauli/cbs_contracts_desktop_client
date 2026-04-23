using System.Globalization;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.References
{
    public static class ProfileEditStateFactory
    {
        public static ProfileEditDialogState Create(
            ReferenceDefinition definition,
            bool isCreateMode,
            ReferenceDataRow? sourceRow,
            IReadOnlyList<CbsTableFilterOptionDefinition>? departmentOptions = null)
        {
            ArgumentNullException.ThrowIfNull(definition);

            return new ProfileEditDialogState
            {
                Definition = definition,
                IsCreateMode = isCreateMode,
                Id = TryGetInt64(sourceRow?.GetValue("id")),
                UserId = TryGetInt64(sourceRow?.GetValue("user.id")),
                PersonId = TryGetInt64(sourceRow?.GetValue("user.person.id")),
                Login = sourceRow?.GetValue("user.name")?.ToString() ?? string.Empty,
                Email = GetFirstTextValue(
                    sourceRow,
                    "user.email.name",
                    "user.person.person_contacts.contact.value"),
                PersonName = GetFirstTextValue(
                    sourceRow,
                    "user.person.full_name",
                    "user.person.person_name.naming.fio"),
                Role = sourceRow?.GetValue("user.role")?.ToString() ?? string.Empty,
                PositionId = TryGetInt64(sourceRow?.GetValue("position.id") ?? sourceRow?.GetValue("position_id")),
                PositionName = sourceRow?.GetValue("position.name")?.ToString() ?? string.Empty,
                DepartmentId = TryGetInt64(sourceRow?.GetValue("department.id") ?? sourceRow?.GetValue("department_id")),
                DepartmentName = sourceRow?.GetValue("department.name")?.ToString() ?? string.Empty,
                Password = sourceRow?.GetValue("user.password")?.ToString() ?? string.Empty,
                IsActive = TryGetBoolean(sourceRow?.GetValue("user.activated")),
                LastLoginText = FormatDateTime(sourceRow?.GetValue("user.last_login")),
                DepartmentOptions = departmentOptions ?? []
            };
        }

        private static long? TryGetInt64(object? value)
        {
            return value switch
            {
                null => null,
                long longValue => longValue,
                int intValue => intValue,
                decimal decimalValue => (long)decimalValue,
                _ when long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    => parsed,
                _ => null
            };
        }

        private static bool TryGetBoolean(object? value)
        {
            return value switch
            {
                true => true,
                false => false,
                string text when bool.TryParse(text, out var parsed) => parsed,
                _ => false
            };
        }

        private static string FormatDateTime(object? value)
        {
            return value switch
            {
                null => string.Empty,
                DateTimeOffset offset => offset.ToString("g", CultureInfo.CurrentCulture),
                DateTime dateTime => dateTime.ToString("g", CultureInfo.CurrentCulture),
                _ => value.ToString() ?? string.Empty
            };
        }

        private static string GetFirstTextValue(ReferenceDataRow? sourceRow, params string[] fieldKeys)
        {
            if (sourceRow is null)
            {
                return string.Empty;
            }

            foreach (var fieldKey in fieldKeys)
            {
                var text = sourceRow.GetValue(fieldKey)?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }
    }
}
