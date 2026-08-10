using System.Xml.Linq;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class PublishConfigurationTests
{
    private static readonly string ProjectPath = TestProjectPaths.FromRepositoryRoot(
        "CbsContractsDesktopClient.csproj");

    [Fact]
    public void Project_UsesLeanFrameworkDependentPublishConfiguration()
    {
        var project = XDocument.Load(ProjectPath);
        var root = Assert.IsType<XElement>(project.Root);

        Assert.Equal("false", root.Descendants("SelfContained").Single().Value);
        Assert.Equal("false", root.Descendants("WindowsAppSDKSelfContained").Single().Value);
        Assert.Equal("ru-RU;en-US", root.Descendants("SatelliteResourceLanguages").Single().Value);

        var excludedPackages = root.Descendants("PackageReference")
            .Where(reference => reference.Attribute("ExcludeAssets")?.Value == "all")
            .Select(reference => reference.Attribute("Include")?.Value)
            .ToHashSet();

        Assert.True(
            excludedPackages.SetEquals(
            new[]
            {
                "Microsoft.WindowsAppSDK.AI",
                "Microsoft.WindowsAppSDK.ML",
                "Microsoft.WindowsAppSDK.Widgets",
                "System.Numerics.Tensors"
            }));
    }
}
