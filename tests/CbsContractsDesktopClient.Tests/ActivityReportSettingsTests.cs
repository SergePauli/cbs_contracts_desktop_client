using CbsContractsDesktopClient.Models.Settings;
using CbsContractsDesktopClient.Services.Settings;
using CbsContractsDesktopClient.Views.Reports;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ActivityReportSettingsTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LocalSettings_RoundTripsActivityReportSectionExpansion()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var service = new LocalUserSettingsService(Path.Combine(_temporaryDirectory, "settings.json"));
        var settings = new LocalUserSettings();
        settings.ActivityReportSectionExpansion["StatusChanges"] = false;
        settings.ActivityReportSectionExpansion["Comments"] = true;

        await service.SaveAsync(settings);
        var restored = await service.GetAsync();

        Assert.False(restored.ActivityReportSectionExpansion["StatusChanges"]);
        Assert.True(restored.ActivityReportSectionExpansion["Comments"]);
    }

    [Fact]
    public void TreeItem_RaisesExpansionChangedOnlyWhenValueChanges()
    {
        var item = new ActivityReportTreeItem(new object(), isExpanded: true);
        var changes = 0;
        item.ExpansionChanged += (_, _) => changes++;

        item.IsExpanded = true;
        item.IsExpanded = false;

        Assert.Equal(1, changes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
