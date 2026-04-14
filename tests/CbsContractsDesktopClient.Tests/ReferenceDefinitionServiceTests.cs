using CbsContractsDesktopClient.Services.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public class ReferenceDefinitionServiceTests
{
    [Fact]
    public void TryGetByRoute_ReturnsStatusDefinition()
    {
        var service = new ReferenceDefinitionService();

        var found = service.TryGetByRoute("/references/Status", out var definition);

        Assert.True(found);
        Assert.Equal("Status", definition.Model);
        Assert.Equal("card", definition.Preset);
        Assert.Equal(["id", "name", "order", "description"], definition.Columns.Select(static column => column.FieldKey));
    }

    [Fact]
    public void TryGetByRoute_ReturnsFalseForUnsupportedRoute()
    {
        var service = new ReferenceDefinitionService();

        var found = service.TryGetByRoute("/contracts", out _);

        Assert.False(found);
    }
}
