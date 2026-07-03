using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;
using CbsContractsDesktopClient.Services.Workspace;
using System.Text.Json;
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
        Assert.True(definition.Capabilities.HasFlag(TablePageCapabilities.Edit));
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

    [Fact]
    public void TryGetByRoute_ReturnsStagesFunctionalTableDefinition()
    {
        var service = CreateService();

        var found = service.TryGetByRoute("/stages", out var definition);

        Assert.True(found);
        Assert.Equal(TablePageKind.Functional, definition.Kind);
        Assert.Equal("Stage", definition.Model);
        Assert.Equal("list", definition.Preset);
        Assert.Equal("Этапы контрактов", definition.Title);
        Assert.True(definition.Capabilities.HasFlag(TablePageCapabilities.ConfigureColumns));
        Assert.True(definition.Capabilities.HasFlag(TablePageCapabilities.PersistFilters));
        Assert.Equal(CbsTableRowStyleKey.StageDeadline, definition.RowStyleKey);
        Assert.Equal("id", definition.InitialSortField);
        Assert.Equal(DataSortDirection.Descending, definition.InitialSortDirection);
        Assert.Contains(definition.Columns, static column => column.FieldKey == "contragent"
            && column.FilterField == "contract.contragent.org.name_or_contract.contragent.org.full_name");
        Assert.Contains(definition.Columns, static column => column.FieldKey == "name"
            && column.DisplayField == "name"
            && column.FilterField == "contract.name"
            && column.SortField == "name");
        Assert.Contains(definition.Columns, static column => column.FieldKey == "region"
            && column.BodyTemplateKey == "StageRegion");
        Assert.Contains(definition.Columns, static column => column.FieldKey == "duration"
            && column.BodyTemplateKey == "StageDuration");
        Assert.Contains(definition.Columns, static column => column.FieldKey == "cost"
            && column.BodyTemplateKey == "StageCost");
        Assert.Contains(definition.Columns, static column => column.FieldKey == "register"
            && column.BodyTemplateKey == "StageRegister"
            && column.FilterField == "registry_quarter_or_registry_year"
            && column.SortField == "registry_year");
        Assert.Contains(definition.Columns, static column => column.FieldKey == "szi"
            && column.BodyTemplateKey == "StageSzi"
            && column.FilterField == "tasks.task_kind_id"
            && column.SortField == "tasks.task_kind_id");
        Assert.Contains(definition.Columns, static column => column.FieldKey == "status"
            && column.Filter.EditorKind == CbsTableFilterEditorKind.MultiSelect
            && column.Filter.OptionsSourceKey == "StageStatus"
            && column.BodyTemplateKey == "StatusBadge");
    }

    [Fact]
    public void TryGetByRoute_ReturnsContractsFunctionalTableDefinition()
    {
        var service = CreateService();

        var found = service.TryGetByRoute("/contracts", out var definition);

        Assert.True(found);
        Assert.Equal(TablePageKind.Functional, definition.Kind);
        Assert.Equal("Contract", definition.Model);
        Assert.Equal("list", definition.Preset);
        Assert.Equal("Полная информация по заключенным договорам", definition.Title);
        Assert.Equal("Контракты", definition.EffectiveNavigationDescription);
        Assert.True(definition.Capabilities.HasFlag(TablePageCapabilities.Create));
        Assert.True(definition.Capabilities.HasFlag(TablePageCapabilities.Edit));
        Assert.True(definition.Capabilities.HasFlag(TablePageCapabilities.ConfigureColumns));
        Assert.True(definition.Capabilities.HasFlag(TablePageCapabilities.PersistFilters));
        Assert.Equal(CbsTableRowStyleKey.ContractDeadline, definition.RowStyleKey);
    }

    [Fact]
    public async Task SaveColumnLayoutAsync_PersistsStagesOrderAndVisibility()
    {
        var service = CreateService();

        await service.SaveColumnLayoutAsync(new TableColumnLayoutSettings
        {
            Route = "/stages",
            OrderedFieldKeys = ["status", "id", "region", "contragent"],
            VisibleFieldKeys = ["status", "id", "contragent"]
        });

        var found = service.TryGetByRoute("/stages", out var definition);

        Assert.True(found);
        Assert.Equal(["status", "id", "region", "contragent"], definition.Columns.Take(4).Select(static column => column.FieldKey));
        Assert.True(definition.Columns.Single(static column => column.FieldKey == "status").IsVisible);
        Assert.True(definition.Columns.Single(static column => column.FieldKey == "id").IsVisible);
        Assert.False(definition.Columns.Single(static column => column.FieldKey == "region").IsVisible);
        Assert.True(definition.Columns.Single(static column => column.FieldKey == "contragent").IsVisible);
    }

    [Fact]
    public async Task SaveFiltersAsync_PersistsStagesFilters()
    {
        var service = CreateService();

        await service.SaveFiltersAsync(
            "/stages",
            [
                new DataFilterCriterion
                {
                    FieldKey = "status",
                    FilterMode = DataFilterMode.Numeric,
                    MatchMode = DataFilterMatchMode.In,
                    Value = new object?[] { 2L, 5L }
                },
                new DataFilterCriterion
                {
                    FieldKey = "contragent",
                    FilterMode = DataFilterMode.Text,
                    MatchMode = DataFilterMatchMode.Contains,
                    Value = "Romashka"
                }
            ]);

        var json = await File.ReadAllTextAsync(_settingsFilePath);
        using var document = JsonDocument.Parse(json);
        var filters = document.RootElement
            .GetProperty("tables")
            .GetProperty("/stages")
            .GetProperty("filters");

        Assert.Equal(2, filters.GetArrayLength());
        Assert.Equal("status", filters[0].GetProperty("fieldKey").GetString());
        Assert.Equal("Numeric", filters[0].GetProperty("filterMode").GetString());
        Assert.Equal("In", filters[0].GetProperty("matchMode").GetString());
        Assert.Equal([2, 5], filters[0].GetProperty("value").EnumerateArray().Select(static item => item.GetInt32()));
        Assert.Equal("contragent", filters[1].GetProperty("fieldKey").GetString());
    }

    [Fact]
    public async Task TryGetByRoute_AppliesSavedStagesFilters()
    {
        var service = CreateService();
        await service.SaveFiltersAsync(
            "/stages",
            [
                new DataFilterCriterion
                {
                    FieldKey = "status",
                    FilterMode = DataFilterMode.Numeric,
                    MatchMode = DataFilterMatchMode.In,
                    Value = new object?[] { 2L, 5L }
                }
            ]);

        var reloadedService = CreateService();
        var found = reloadedService.TryGetByRoute("/stages", out var definition);

        Assert.True(found);
        var filter = Assert.Single(definition.InitialFilters);
        Assert.Equal("status", filter.FieldKey);
        Assert.Equal(DataFilterMode.Numeric, filter.FilterMode);
        Assert.Equal(DataFilterMatchMode.In, filter.MatchMode);
        var values = Assert.IsAssignableFrom<IEnumerable<object?>>(filter.Value);
        Assert.Equal([2L, 5L], values.Cast<long>());
        var statusColumn = definition.Columns.Single(static column => column.FieldKey == "status");
        Assert.Equal(DataFilterMatchMode.In, statusColumn.Filter.MatchMode);
        Assert.Equal([2L, 5L], Assert.IsAssignableFrom<IEnumerable<object?>>(statusColumn.Filter.Value).Cast<long>());
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
        var referenceDefinitionService = new ReferenceDefinitionService(new TableSettingsService(settingsService));
        return new TablePageDefinitionService(referenceDefinitionService, new TableSettingsService(settingsService));
    }
}
