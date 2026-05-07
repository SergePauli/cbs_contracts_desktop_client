using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;
using CbsContractsDesktopClient.Services.Workspace;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class TablePageDefinitionServiceTests : IDisposable
{
    private readonly string _temporaryDirectory;
    private readonly string _settingsFilePath;

    public TablePageDefinitionServiceTests()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "CbsContractsDesktopClient.Tests", Guid.NewGuid().ToString("N"));
        _settingsFilePath = Path.Combine(_temporaryDirectory, "user-settings.json");
    }

    [Fact]
    public void TryGetByRoute_ReturnsRevisionsFunctionalTableDefinition()
    {
        var service = CreateService();

        var found = service.TryGetByRoute("/revisions", out var definition);

        Assert.True(found);
        Assert.Equal(TablePageKind.Functional, definition.Kind);
        Assert.Equal("Revision", definition.Model);
        Assert.Equal("list", definition.Preset);
        Assert.Equal("Дополнительные соглашения", definition.Title);
        Assert.Equal("Дополнительные соглашения контрактов", definition.EffectiveNavigationDescription);
        Assert.False(definition.Capabilities.HasFlag(TablePageCapabilities.Create));
        Assert.False(definition.Capabilities.HasFlag(TablePageCapabilities.Edit));
        Assert.False(definition.Capabilities.HasFlag(TablePageCapabilities.Delete));
        Assert.True(definition.Capabilities.HasFlag(TablePageCapabilities.RowSelection));
        Assert.True(definition.Capabilities.HasFlag(TablePageCapabilities.DetailFooter));
        Assert.Equal("contract.id", definition.InitialSortField);
        Assert.Equal(DataSortDirection.Descending, definition.InitialSortDirection);
        Assert.Equal(
            [
                "contract.id",
                "contract.name",
                "priority",
                "contract.signed_at",
                "contract.contragent.name",
                "is_signed",
                "is_present",
                "contract.governmental"
            ],
            definition.Columns.Select(static column => column.FieldKey));

        var contractColumn = definition.Columns.Single(static column => column.FieldKey == "contract.id");
        Assert.Equal("contract_id", contractColumn.FilterField);
        Assert.Equal("contract_id", contractColumn.SortField);
        Assert.Equal(DataFilterMode.Numeric, contractColumn.Filter.Mode);

        var nameColumn = definition.Columns.Single(static column => column.FieldKey == "contract.name");
        Assert.Equal("contract.name", nameColumn.DisplayField);
        Assert.Equal("contract.name", nameColumn.FilterField);
        Assert.Equal("contract.name", nameColumn.SortField);

        var signedAtColumn = definition.Columns.Single(static column => column.FieldKey == "contract.signed_at");
        Assert.Equal(DataFilterMode.Date, signedAtColumn.Filter.Mode);
        Assert.Equal(DataFilterMatchMode.Equals, signedAtColumn.Filter.MatchMode);
        Assert.Equal("contract.signed_at", signedAtColumn.FilterField);

        var contragentColumn = definition.Columns.Single(static column => column.FieldKey == "contract.contragent.name");
        Assert.Equal("contract.contragent.name", contragentColumn.DisplayField);
        Assert.Equal("contract.contragent.org.name_or_contract.contragent.org.full_name", contragentColumn.FilterField);
        Assert.Equal("contract.contragent.org.name", contragentColumn.SortField);

        var priorityColumn = definition.Columns.Single(static column => column.FieldKey == "priority");
        Assert.Equal(DataFilterMatchMode.GreaterThan, priorityColumn.Filter.MatchMode);
        Assert.Equal(CbsTableColumnAlignment.Center, priorityColumn.Alignment);

        Assert.All(
            definition.Columns.Where(static column => column.FieldKey is "is_signed" or "is_present" or "contract.governmental"),
            static column =>
            {
                Assert.Equal(CbsTableBodyMode.BooleanIcon, column.BodyMode);
                Assert.Equal(CbsTableFilterEditorKind.Boolean, column.Filter.EditorKind);
                Assert.Equal(DataFilterMatchMode.Equals, column.Filter.MatchMode);
            });
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private TablePageDefinitionService CreateService()
    {
        var settingsService = new LocalUserSettingsService(_settingsFilePath);
        var referenceDefinitionService = new ReferenceDefinitionService(settingsService);
        return new TablePageDefinitionService(referenceDefinitionService, settingsService);
    }
}
