using System.Globalization;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

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
                Id = TryGetLong(sourceRow?.GetValue("id")),
                UserId = TryGetLong(sourceRow?.GetValue("user.id")),
                PersonId = TryGetLong(sourceRow?.GetValue("user.person.id")),
                Login = sourceRow?.GetValue("user.name")?.ToString() ?? string.Empty,
                Email = TryGetText(
                    sourceRow,
                    "user.email.name",
                    "user.person.person_contacts.contact.value") ?? string.Empty,
                PersonName = TryGetText(
                    sourceRow,
                    "user.person.full_name",
                    "user.person.person_name.naming.fio") ?? string.Empty,
                Role = sourceRow?.GetValue("user.role")?.ToString() ?? string.Empty,
                PositionId = TryGetLong(sourceRow?.GetValue("position.id") ?? sourceRow?.GetValue("position_id")),
                PositionName = sourceRow?.GetValue("position.name")?.ToString() ?? string.Empty,
                DepartmentId = TryGetLong(sourceRow?.GetValue("department.id") ?? sourceRow?.GetValue("department_id")),
                DepartmentName = sourceRow?.GetValue("department.name")?.ToString() ?? string.Empty,
                Password = sourceRow?.GetValue("user.password")?.ToString() ?? string.Empty,
                IsActive = TryGetBool(sourceRow?.GetValue("user.activated")) == true,
                LastLoginText = FormatDateTime(sourceRow?.GetValue("user.last_login")),
                DepartmentOptions = departmentOptions ?? []
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

    }
}
