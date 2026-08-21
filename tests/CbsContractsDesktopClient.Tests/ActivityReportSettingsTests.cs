using CbsContractsDesktopClient.Models.Settings;
using CbsContractsDesktopClient.Services.Settings;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ActivityReportSettingsTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private static readonly string ActivityReportViewPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "Shell",
        "ActivityReportHostView.xaml");

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
    public void ActivityReport_UsesStaticNodesWithSectionSpecificBindings()
    {
        var xaml = File.ReadAllText(ActivityReportViewPath);
        var sections = new[]
        {
            "StatusChanges",
            "PendingStages",
            "AddedContracts",
            "DeadlineChanges",
            "Comments",
            "Funding",
            "Payments"
        };

        Assert.Contains("<TreeView.RootNodes>", xaml);
        Assert.Contains("Content=\"{Binding Content}\"", xaml);

        foreach (var section in sections)
        {
            Assert.Contains($"x:Name=\"ActivityReport_{section}\"", xaml);
            Assert.Contains($"IsExpanded=\"{{x:Bind {section}IsExpanded, Mode=TwoWay}}\"", xaml);
            Assert.Contains($"Content=\"{{x:Bind {section}Table, Mode=OneWay}}\"", xaml);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
