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

        public AuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
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
            // TODO: Очистить токен, сбросить состояние пользователя
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
    }
}
