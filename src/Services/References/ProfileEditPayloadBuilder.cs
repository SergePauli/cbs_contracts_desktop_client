using System.Text.RegularExpressions;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.ViewModels.References;

namespace CbsContractsDesktopClient.Services.References
{
    public static class ProfileEditPayloadBuilder
    {
        public static IReadOnlyDictionary<string, object?> BuildForCreate(ProfileEditViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var state = viewModel.State;
            var login = viewModel.Login.Trim();
            var email = viewModel.Email.Trim();
            var personName = CollapseWhitespace(viewModel.PersonName);
            var role = NormalizeRoleCsv(viewModel.RoleApiValue);
            var positionName = viewModel.PositionName.Trim();

            Validate(
                login,
                email,
                personName,
                role,
                positionName,
                viewModel.SelectedDepartmentId,
                viewModel.Password,
                isCreateMode: true);

            var userAttributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = login,
                ["role"] = role,
                ["activated"] = viewModel.IsActivated,
                ["password"] = viewModel.Password,
                ["person_attributes"] = BuildPersonAttributesForCreate(email, personName)
            };

            var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["list_key"] = Guid.NewGuid().ToString(),
                ["user_attributes"] = userAttributes,
                ["department_id"] = viewModel.SelectedDepartmentId
            };

            AppendPositionPayload(request, positionName, viewModel.SelectedPositionOption);
            return request;
        }

        public static IReadOnlyDictionary<string, object?> BuildForUpdate(ProfileEditViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            var state = viewModel.State;
            if (state.Id is null)
            {
                throw new InvalidOperationException("Profile id is missing for save.");
            }

            var login = viewModel.Login.Trim();
            var email = viewModel.Email.Trim();
            var personName = CollapseWhitespace(viewModel.PersonName);
            var role = NormalizeRoleCsv(viewModel.RoleApiValue);
            var positionName = viewModel.PositionName.Trim();

            Validate(
                login,
                email,
                personName,
                role,
                positionName,
                viewModel.SelectedDepartmentId,
                viewModel.Password,
                isCreateMode: false);

            var request = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = state.Id.Value
            };
            if (state.Definition.IsAuditEnabled)
            {
                request["list_key"] = Guid.NewGuid().ToString();
            }

            var changedUserAttributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (!string.Equals(login, state.Login.Trim(), StringComparison.CurrentCulture))
            {
                changedUserAttributes["name"] = login;
            }

            var initialRole = NormalizeRoleCsv(state.Role);
            if (!string.Equals(role, initialRole, StringComparison.OrdinalIgnoreCase))
            {
                changedUserAttributes["role"] = role;
            }

            if (viewModel.IsActivated != state.IsActive)
            {
                changedUserAttributes["activated"] = viewModel.IsActivated;
            }

            if (!string.IsNullOrWhiteSpace(viewModel.Password)
                && !string.Equals(viewModel.Password, state.Password, StringComparison.Ordinal))
            {
                changedUserAttributes["password"] = viewModel.Password;
            }

            var personAttributes = BuildPersonAttributesIfChanged(state, email, personName);
            if (personAttributes is not null)
            {
                changedUserAttributes["person_attributes"] = personAttributes;
            }

            if (changedUserAttributes.Count > 0)
            {
                if (state.UserId is null)
                {
                    throw new InvalidOperationException("User id is missing for profile update.");
                }

                changedUserAttributes["id"] = state.UserId.Value;
                request["user_attributes"] = changedUserAttributes;
            }

            if (viewModel.SelectedDepartmentId != state.DepartmentId)
            {
                request["department_id"] = viewModel.SelectedDepartmentId;
            }

            AppendPositionPayloadIfChanged(
                request,
                state.PositionName,
                positionName,
                viewModel.SelectedPositionOption);

            return request;
        }

        private static Dictionary<string, object?> BuildPersonAttributesForCreate(string email, string personName)
        {
            var names = personName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var namingAttributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["surname"] = CapitalizeFirst(names[0]),
                ["name"] = CapitalizeFirst(names[1])
            };
            if (names.Length > 2)
            {
                namingAttributes["patrname"] = CapitalizeFirst(names[2]);
            }

            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["person_names_attributes"] = new object?[]
                {
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["used"] = true,
                        ["list_key"] = Guid.NewGuid().ToString(),
                        ["naming_attributes"] = namingAttributes
                    }
                },
                ["person_contacts_attributes"] = new object?[]
                {
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["list_key"] = Guid.NewGuid().ToString(),
                        ["contact_attributes"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["value"] = email,
                            ["type"] = "Email"
                        }
                    }
                }
            };
        }

        private static Dictionary<string, object?>? BuildPersonAttributesIfChanged(
            ProfileEditDialogState state,
            string email,
            string personName)
        {
            var emailChanged = !string.Equals(email, state.Email.Trim(), StringComparison.CurrentCultureIgnoreCase);
            var personChanged = !string.Equals(personName, CollapseWhitespace(state.PersonName), StringComparison.CurrentCulture);
            if (!emailChanged && !personChanged)
            {
                return null;
            }

            var personAttributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (state.PersonId is long personId)
            {
                personAttributes["id"] = personId;
            }

            if (personChanged)
            {
                var names = personName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var namingAttributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["surname"] = CapitalizeFirst(names[0]),
                    ["name"] = CapitalizeFirst(names[1])
                };

                if (names.Length > 2)
                {
                    namingAttributes["patrname"] = CapitalizeFirst(names[2]);
                }

                personAttributes["person_names_attributes"] = new object?[]
                {
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["used"] = true,
                        ["list_key"] = Guid.NewGuid().ToString(),
                        ["naming_attributes"] = namingAttributes
                    }
                };
            }
            else
            {
                personAttributes["person_names_attributes"] = Array.Empty<object?>();
            }

            if (emailChanged)
            {
                personAttributes["person_contacts_attributes"] = new object?[]
                {
                    new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["list_key"] = Guid.NewGuid().ToString(),
                        ["contact_attributes"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["value"] = email,
                            ["type"] = "Email"
                        }
                    }
                };
            }
            else
            {
                personAttributes["person_contacts_attributes"] = Array.Empty<object?>();
            }

            return personAttributes;
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

            if (string.IsNullOrWhiteSpace(currentPositionName))
            {
                request["position_id"] = null;
                return;
            }

            AppendPositionPayload(request, currentPositionName, selectedPositionOption);
        }

        private static void AppendPositionPayload(
            Dictionary<string, object?> request,
            string positionName,
            CbsTableFilterOptionDefinition? selectedPositionOption)
        {
            if (selectedPositionOption?.Value is long int64Value)
            {
                request["position_id"] = int64Value;
                return;
            }

            if (selectedPositionOption?.Value is int int32Value)
            {
                request["position_id"] = int32Value;
                return;
            }

            if (selectedPositionOption?.Value is decimal decimalValue)
            {
                request["position_id"] = (long)decimalValue;
                return;
            }

            request["position_attributes"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = CapitalizeFirst(positionName)
            };
        }

        private static void Validate(
            string login,
            string email,
            string personName,
            string role,
            string positionName,
            long? departmentId,
            string password,
            bool isCreateMode)
        {
            if (login.Length < 2)
            {
                throw new InvalidOperationException("Поле 'Логин' обязательно.");
            }

            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                throw new InvalidOperationException("Поле 'Email' заполнено некорректно.");
            }

            var parts = personName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                throw new InvalidOperationException("Поле 'ФИО' должно содержать минимум фамилию и имя.");
            }

            if (string.IsNullOrWhiteSpace(role))
            {
                throw new InvalidOperationException("Поле 'Роли' обязательно.");
            }

            if (string.IsNullOrWhiteSpace(positionName))
            {
                throw new InvalidOperationException("Поле 'Должность' обязательно.");
            }

            if (!departmentId.HasValue)
            {
                throw new InvalidOperationException("Поле 'Отдел' обязательно.");
            }

            if (isCreateMode && string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Поле 'Пароль' обязательно при добавлении.");
            }
        }

        private static string CapitalizeFirst(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            if (trimmed.Length == 1)
            {
                return trimmed.ToUpperInvariant();
            }

            return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
        }

        private static string CollapseWhitespace(string value)
        {
            return string.Join(
                " ",
                value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private static string NormalizeRoleCsv(string value)
        {
            return string.Join(
                ",",
                value
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(static role => role.ToLowerInvariant()));
        }
    }
}
