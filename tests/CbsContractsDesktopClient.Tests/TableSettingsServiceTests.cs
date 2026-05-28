using System.Text.Json;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Services.Settings;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class TableSettingsServiceTests : IDisposable
{
    private readonly string _temporaryDirectory;
    private readonly string _settingsFilePath;

    public TableSettingsServiceTests()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
        _settingsFilePath = Path.Combine(_temporaryDirectory, "settings.json");
    }

    [Fact]
    public async Task SaveColumnWidthAsync_PersistsWidthForSpecificTableAndColumn()
    {
        var service = CreateService();

        await service.SaveColumnWidthAsync(new TableColumnWidthSettings
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
    public async Task SaveSortAsync_PersistsSortForSpecificTable()
    {
        var service = CreateService();

        await service.SaveSortAsync(new TableSortSettings
        {
            Route = "/references/Ownership",
            FieldKey = "name",
            Direction = DataSortDirection.Descending
        });

        Assert.True(File.Exists(_settingsFilePath));

        var json = await File.ReadAllTextAsync(_settingsFilePath);
        using var document = JsonDocument.Parse(json);
        var sort = document.RootElement
            .GetProperty("tables")
            .GetProperty("/references/Ownership")
            .GetProperty("sort");

        Assert.Equal("name", sort.GetProperty("fieldKey").GetString());
        Assert.Equal("Descending", sort.GetProperty("direction").GetString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private TableSettingsService CreateService()
    {
        return new TableSettingsService(new LocalUserSettingsService(_settingsFilePath));
    }
}
