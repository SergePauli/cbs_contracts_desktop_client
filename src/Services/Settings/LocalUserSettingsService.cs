using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Settings;

namespace CbsContractsDesktopClient.Services.Settings
{
    public sealed class LocalUserSettingsService : ILocalUserSettingsService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private readonly SemaphoreSlim _fileGate = new(1, 1);
        private readonly string _settingsFilePath;

        public LocalUserSettingsService()
            : this(GetDefaultSettingsFilePath())
        {
        }

        public LocalUserSettingsService(string settingsFilePath)
        {
            _settingsFilePath = settingsFilePath;
        }

        public LocalUserSettings Get()
        {
            _fileGate.Wait();

            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    return new LocalUserSettings();
                }

                using var stream = File.OpenRead(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<LocalUserSettings>(
                    stream,
                    SerializerOptions);

                return settings ?? new LocalUserSettings();
            }
            catch (JsonException)
            {
                return new LocalUserSettings();
            }
            finally
            {
                _fileGate.Release();
            }
        }

        public async Task<LocalUserSettings> GetAsync(CancellationToken cancellationToken = default)
        {
            await _fileGate.WaitAsync(cancellationToken);

            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    return new LocalUserSettings();
                }

                await using var stream = File.OpenRead(_settingsFilePath);
                var settings = await JsonSerializer.DeserializeAsync<LocalUserSettings>(
                    stream,
                    SerializerOptions,
                    cancellationToken);

                return settings ?? new LocalUserSettings();
            }
            catch (JsonException)
            {
                return new LocalUserSettings();
            }
            finally
            {
                _fileGate.Release();
            }
        }

        public async Task SaveAsync(LocalUserSettings settings, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(settings);

            await _fileGate.WaitAsync(cancellationToken);

            try
            {
                var directoryPath = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                await using var stream = File.Create(_settingsFilePath);
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    SerializerOptions,
                    cancellationToken);
            }
            finally
            {
                _fileGate.Release();
            }
        }

        private static string GetDefaultSettingsFilePath()
        {
            var rootDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CbsContractsDesktopClient");
            var settingsDirectory = Path.Combine(rootDirectory, "settings");
            return Path.Combine(settingsDirectory, "user-settings.json");
        }
    }
}
