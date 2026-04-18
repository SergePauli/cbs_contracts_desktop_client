using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;
using System.Text.Json;
using Xunit;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Data;

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
        Assert.Equal(["id", "name", "order", "description"], definition.Fields.Select(static field => field.FieldKey));
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

    [Fact]
    public void TryGetByRoute_AppliesExpectedDefaultAlignments()
    {
        var service = CreateService();

        var found = service.TryGetByRoute("/references/Status", out var definition);

        Assert.True(found);
        Assert.Equal(CbsTableColumnAlignment.Right, definition.Columns.Single(static column => column.FieldKey == "id").Alignment);
        Assert.Equal(CbsTableColumnAlignment.Left, definition.Columns.Single(static column => column.FieldKey == "name").Alignment);
        Assert.Equal(CbsTableColumnAlignment.Right, definition.Columns.Single(static column => column.FieldKey == "order").Alignment);
        Assert.Equal("⌕", definition.Columns.Single(static column => column.FieldKey == "order").Filter.PlaceholderText);

        var boolFound = service.TryGetByRoute("/references/IsecurityTool", out var boolDefinition);

        Assert.True(boolFound);
        Assert.Equal(CbsTableColumnAlignment.Center, boolDefinition.Columns.Single(static column => column.FieldKey == "used").Alignment);
        Assert.Equal(DataFilterMode.Text, definition.Columns.Single(static column => column.FieldKey == "name").Filter.Mode);
        Assert.Equal(DataFilterMode.Numeric, definition.Columns.Single(static column => column.FieldKey == "order").Filter.Mode);
    }

    [Fact]
    public void TryGetByRoute_ReturnsExpectedFieldDefinitions()
    {
        var service = CreateService();

        var found = service.TryGetByRoute("/references/IsecurityTool", out var definition);

        Assert.True(found);

        var idField = definition.Fields.Single(static field => field.FieldKey == "id");
        Assert.Equal(ReferenceFieldEditorType.Number, idField.EditorType);
        Assert.True(idField.IsRequired);
        Assert.True(idField.IsReadOnlyOnCreate);
        Assert.True(idField.IsReadOnlyOnEdit);

        var nameField = definition.Fields.Single(static field => field.FieldKey == "name");
        Assert.Equal(ReferenceFieldEditorType.Text, nameField.EditorType);
        Assert.True(nameField.IsRequired);
        Assert.False(nameField.IsReadOnlyOnCreate);
        Assert.False(nameField.IsReadOnlyOnEdit);

        var usedField = definition.Fields.Single(static field => field.FieldKey == "used");
        Assert.Equal(ReferenceFieldEditorType.Boolean, usedField.EditorType);
        Assert.False(usedField.IsRequired);
        Assert.False(usedField.IsReadOnlyOnCreate);
        Assert.False(usedField.IsReadOnlyOnEdit);
    }

    [Fact]
    public void ReferenceDefinition_Clone_PreservesColumnAlignmentAndFields()
    {
        var definition = new ReferenceDefinition
        {
            Route = "/references/Test",
            Model = "Test",
            Title = "Test",
            Fields =
            [
                new ReferenceFieldDefinition
                {
                    FieldKey = "amount",
                    Label = "Amount",
                    EditorType = ReferenceFieldEditorType.Number,
                    IsRequired = true,
                    IsReadOnlyOnEdit = true
                }
            ],
            Columns =
            [
                new CbsTableColumnDefinition
                {
                    FieldKey = "amount",
                    Header = "Amount",
                    Alignment = CbsTableColumnAlignment.Right,
                    Filter = new CbsTableColumnFilterDefinition
                    {
                        Mode = DataFilterMode.Numeric
                    }
                },
                new CbsTableColumnDefinition
                {
                    FieldKey = "flag",
                    Header = "Flag",
                    Alignment = CbsTableColumnAlignment.Center,
                    Filter = new CbsTableColumnFilterDefinition
                    {
                        Mode = DataFilterMode.Text
                    }
                }
            ]
        };

        var clone = definition.Clone();

        Assert.Equal(CbsTableColumnAlignment.Right, clone.Columns.Single(static column => column.FieldKey == "amount").Alignment);
        Assert.Equal(CbsTableColumnAlignment.Center, clone.Columns.Single(static column => column.FieldKey == "flag").Alignment);
        Assert.Equal(DataFilterMode.Numeric, clone.Columns.Single(static column => column.FieldKey == "amount").Filter.Mode);
        Assert.Equal(DataFilterMode.Text, clone.Columns.Single(static column => column.FieldKey == "flag").Filter.Mode);
        Assert.Equal(ReferenceFieldEditorType.Number, clone.Fields.Single(static field => field.FieldKey == "amount").EditorType);
        Assert.True(clone.Fields.Single(static field => field.FieldKey == "amount").IsRequired);
        Assert.True(clone.Fields.Single(static field => field.FieldKey == "amount").IsReadOnlyOnEdit);
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
