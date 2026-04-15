using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;
using System.Text.Json;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ReferenceDefinitionServiceTests : IDisposable
{
    private readonly string _temporaryDirectory;
    private readonly string _settingsFilePath;

    public ReferenceDefinitionServiceTests()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "CbsContractsDesktopClient.Tests", Guid.NewGuid().ToString("N"));
        _settingsFilePath = Path.Combine(_temporaryDirectory, "user-settings.json");
    }

    [Fact]
    public void TryGetByRoute_ReturnsStatusDefinition()
    {
        var service = CreateService();

        var found = service.TryGetByRoute("/references/Status", out var definition);

        Assert.True(found);
        Assert.Equal("Status", definition.Model);
        Assert.Equal("card", definition.Preset);
        Assert.Equal(["id", "name", "order", "description"], definition.Columns.Select(static column => column.FieldKey));
    }

    [Fact]
    public void TryGetByRoute_ReturnsFalseForUnsupportedRoute()
    {
        var service = CreateService();

        var found = service.TryGetByRoute("/contracts", out _);

        Assert.False(found);
    }

    [Fact]
    public async Task SaveColumnWidthAsync_PersistsWidthForSpecificTableAndColumn()
    {
        var service = CreateService();

        await service.SaveColumnWidthAsync(new ReferenceTableColumnWidthSettings
        {
            Route = "/references/Ownership",
            FieldKey = "name",
            Width = "24rem"
        });

        Assert.True(File.Exists(_settingsFilePath));

        var json = await File.ReadAllTextAsync(_settingsFilePath);
        using var document = JsonDocument.Parse(json);
        var width = document.RootElement
            .GetProperty("tables")
            .GetProperty("/references/Ownership")
            .GetProperty("columns")
            .GetProperty("name")
            .GetProperty("width")
            .GetString();

        Assert.Equal("24rem", width);
    }

    [Fact]
    public async Task TryGetByRoute_AppliesSavedWidthFromSettingsFile()
    {
        var settingsService = CreateSettingsService();
        await settingsService.SaveAsync(new()
        {
            Tables =
            {
                ["/references/Ownership"] = new()
                {
                    Columns =
                    {
                        ["name"] = new() { Width = "24rem" },
                        ["okopf"] = new() { Width = "9rem" }
                    }
                }
            }
        });

        var service = new ReferenceDefinitionService(settingsService);

        var found = service.TryGetByRoute("/references/Ownership", out var definition);

        Assert.True(found);
        Assert.Equal("24rem", definition.Columns.Single(static column => column.FieldKey == "name").Width);
        Assert.Equal("9rem", definition.Columns.Single(static column => column.FieldKey == "okopf").Width);
        Assert.Equal("5rem", definition.Columns.Single(static column => column.FieldKey == "id").EffectiveWidth);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private ReferenceDefinitionService CreateService()
    {
        return new ReferenceDefinitionService(CreateSettingsService());
    }

    private LocalUserSettingsService CreateSettingsService()
    {
        return new LocalUserSettingsService(_settingsFilePath);
    }
}
