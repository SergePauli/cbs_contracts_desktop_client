using CbsContractsDesktopClient.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CbsContractsDesktopClient.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IUserService _userService;

        public AuthService(HttpClient httpClient, IUserService userService)
        {
            _httpClient = httpClient;
            _userService = userService;
        }

        public async Task<LoginResponse> LoginAsync(string username, string password)
        {
            var loginRequest = new AuthLoginRequest
            {
                Name = username,
                Password = password
            };

            try
            {
                var response = await _httpClient.PostAsJsonAsync("auth/login", loginRequest);

                if (!response.IsSuccessStatusCode)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = $"Ошибка HTTP: {response.StatusCode}"
                    };
                }

                var rawJson = await response.Content.ReadAsStringAsync();
                var authResponse = JsonSerializer.Deserialize<AuthApiResponse>(rawJson);
                if (authResponse?.User == null)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Ошибка десериализации",
                        DebugJson = rawJson
                    };
                }

                return new LoginResponse
                {
                    Success = true,
                    Token = authResponse.Tokens?.Access ?? string.Empty,
                    DebugJson = rawJson,
                    User = new User
                    {
                        Id = authResponse.User.Id,
                        Username = authResponse.User.Name ?? username,
                        FullName = authResponse.User.Name ?? username,
                        Role = authResponse.User.Role ?? string.Empty,
                        DepartmentId = authResponse.User.GetDepartmentId(),
                        DepartmentName = authResponse.User.GetDepartmentName(),
                        Token = authResponse.Tokens?.Access ?? string.Empty,
                        LoginTime = DateTime.Now
                    }
                };
            }
            catch (Exception ex)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Ошибка сети: {ex.Message}"
                };
            }
        }

        public void Logout()
        {
            _userService.ClearCurrentUser();
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public class AuthLoginRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthApiResponse
    {
        [JsonPropertyName("tokens")]
        public AuthTokens? Tokens { get; set; }

        [JsonPropertyName("user")]
        public AuthApiUser? User { get; set; }
    }

    public class AuthTokens
    {
        [JsonPropertyName("access")]
        public string Access { get; set; } = string.Empty;

        [JsonPropertyName("refresh")]
        public string Refresh { get; set; } = string.Empty;
    }

    public class AuthApiUser
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtraFields { get; set; }

        public int? GetDepartmentId()
        {
            if (TryGetInt32("department_id", out var departmentId))
            {
                return departmentId;
            }

            if (TryGetNestedProperty("department", "id", out departmentId))
            {
                return departmentId;
            }

            if (TryGetNestedProperty("profile", "department_id", out departmentId))
            {
                return departmentId;
            }

            if (TryGetNestedProperty("profile", "department", "id", out departmentId))
            {
                return departmentId;
            }

            if (TryGetFirstArrayNestedProperty("profiles", "department_id", out departmentId))
            {
                return departmentId;
            }

            if (TryGetFirstArrayNestedProperty("profiles", "department", "id", out departmentId))
            {
                return departmentId;
            }

            return null;
        }

        public string GetDepartmentName()
        {
            if (TryGetNestedString("department", "name", out var departmentName))
            {
                return departmentName;
            }

            if (TryGetNestedString("profile", "department", "name", out departmentName))
            {
                return departmentName;
            }

            if (TryGetFirstArrayNestedString("profiles", "department", "name", out departmentName))
            {
                return departmentName;
            }

            return string.Empty;
        }

        private bool TryGetInt32(string key, out int value)
        {
            value = default;
            if (ExtraFields == null || !ExtraFields.TryGetValue(key, out var element))
            {
                return false;
            }

            return TryReadInt32(element, out value);
        }

        private bool TryGetNestedProperty(string parentKey, string childKey, out int value)
        {
            value = default;
            if (!TryGetObject(parentKey, out var parent))
            {
                return false;
            }

            return parent.TryGetProperty(childKey, out var child) && TryReadInt32(child, out value);
        }

        private bool TryGetNestedProperty(string parentKey, string childKey, string grandChildKey, out int value)
        {
            value = default;
            if (!TryGetObject(parentKey, out var parent))
            {
                return false;
            }

            if (!parent.TryGetProperty(childKey, out var child) || child.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return child.TryGetProperty(grandChildKey, out var grandChild) && TryReadInt32(grandChild, out value);
        }

        private bool TryGetFirstArrayNestedProperty(string arrayKey, string childKey, out int value)
        {
            value = default;
            if (!TryGetFirstArrayItem(arrayKey, out var item))
            {
                return false;
            }

            return item.TryGetProperty(childKey, out var child) && TryReadInt32(child, out value);
        }

        private bool TryGetFirstArrayNestedProperty(string arrayKey, string childKey, string grandChildKey, out int value)
        {
            value = default;
            if (!TryGetFirstArrayItem(arrayKey, out var item))
            {
                return false;
            }

            if (!item.TryGetProperty(childKey, out var child) || child.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return child.TryGetProperty(grandChildKey, out var grandChild) && TryReadInt32(grandChild, out value);
        }

        private bool TryGetNestedString(string parentKey, string childKey, out string value)
        {
            value = string.Empty;
            if (!TryGetObject(parentKey, out var parent))
            {
                return false;
            }

            return parent.TryGetProperty(childKey, out var child) && TryReadString(child, out value);
        }

        private bool TryGetNestedString(string parentKey, string childKey, string grandChildKey, out string value)
        {
            value = string.Empty;
            if (!TryGetObject(parentKey, out var parent))
            {
                return false;
            }

            if (!parent.TryGetProperty(childKey, out var child) || child.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return child.TryGetProperty(grandChildKey, out var grandChild) && TryReadString(grandChild, out value);
        }

        private bool TryGetFirstArrayNestedString(string arrayKey, string childKey, string grandChildKey, out string value)
        {
            value = string.Empty;
            if (!TryGetFirstArrayItem(arrayKey, out var item))
            {
                return false;
            }

            if (!item.TryGetProperty(childKey, out var child) || child.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return child.TryGetProperty(grandChildKey, out var grandChild) && TryReadString(grandChild, out value);
        }

        private bool TryGetObject(string key, out JsonElement element)
        {
            element = default;
            return ExtraFields != null
                && ExtraFields.TryGetValue(key, out element)
                && element.ValueKind == JsonValueKind.Object;
        }

        private bool TryGetFirstArrayItem(string key, out JsonElement element)
        {
            element = default;
            if (ExtraFields == null
                || !ExtraFields.TryGetValue(key, out var arrayElement)
                || arrayElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in arrayElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    element = item;
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadInt32(JsonElement element, out int value)
        {
            value = default;
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.TryGetInt32(out value),
                JsonValueKind.String => int.TryParse(element.GetString(), out value),
                _ => false
            };
        }

        private static bool TryReadString(JsonElement element, out string value)
        {
            value = string.Empty;
            if (element.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = element.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
