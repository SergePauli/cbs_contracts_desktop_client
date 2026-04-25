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
    public void TryGetByRoute_ReturnsProfileDefinition()
    {
        var service = CreateService();

        var found = service.TryGetByRoute("/users", out var definition);

        Assert.True(found);
        Assert.Equal("Profile", definition.Model);
        Assert.Equal("edit", definition.Preset);
        Assert.Equal("Профили пользователей", definition.EffectiveNavigationDescription);
        Assert.Equal(ReferenceEditorKind.Profile, definition.EditorKind);
        Assert.Equal(
            ["id", "name", "email", "person", "role", "position", "department", "last_login", "used"],
            definition.Columns.Select(static column => column.FieldKey));
        Assert.Equal("user.name", definition.Columns.Single(static column => column.FieldKey == "name").DisplayField);
        var emailColumn = definition.Columns.Single(static column => column.FieldKey == "email");
        Assert.Equal("user.email.name", emailColumn.DisplayField);
        Assert.Equal("user.person.person_contacts.contact.value", emailColumn.FilterField);
        Assert.Equal("user.person.person_contacts.contact.value", emailColumn.SortField);
        var personColumn = definition.Columns.Single(static column => column.FieldKey == "person");
        Assert.Equal("user.person.full_name", personColumn.DisplayField);
        Assert.Equal("user.person.person_name.naming.fio", personColumn.FilterField);
        Assert.Equal("user.person.person_name.naming.surname", personColumn.SortField);
        var departmentColumn = definition.Columns.Single(static column => column.FieldKey == "department");
        Assert.Equal("department.name", departmentColumn.DisplayField);
        Assert.Equal("department_id", departmentColumn.FilterField);
        Assert.Equal("department.name", departmentColumn.SortField);
        Assert.Equal(CbsTableFilterEditorKind.MultiSelect, departmentColumn.Filter.EditorKind);
        Assert.Equal(DataFilterMatchMode.In, departmentColumn.Filter.MatchMode);
        Assert.Equal("Department", departmentColumn.Filter.OptionsSourceKey);
        Assert.Equal("Все", departmentColumn.Filter.EmptySelectionText);
        var lastLoginColumn = definition.Columns.Single(static column => column.FieldKey == "last_login");
        Assert.True(lastLoginColumn.IsFilterable);
        Assert.Equal(DataFilterMode.DateTime, lastLoginColumn.Filter.Mode);
        Assert.Equal(DataFilterMatchMode.GreaterThanOrEqual, lastLoginColumn.Filter.MatchMode);
        Assert.Equal("user.activated", definition.Columns.Single(static column => column.FieldKey == "used").DisplayField);
    }

    [Fact]
    public void TryGetByRoute_ReturnsEmployeesDefinition()
    {
        var service = CreateService();

        var found = service.TryGetByRoute("/employees", out var definition);

        Assert.True(found);
        Assert.Equal("Employee", definition.Model);
        Assert.Equal("card", definition.Preset);
        Assert.Equal("Сотрудники", definition.Title);
        Assert.Equal(ReferenceEditorKind.Employee, definition.EditorKind);
        Assert.Equal("id", definition.InitialSortField);
        Assert.Equal(DataSortDirection.Descending, definition.InitialSortDirection);
        Assert.Equal(
            ["id", "name", "contragent", "position", "contacts", "used", "priority", "description"],
            definition.Columns.Select(static column => column.FieldKey));

        var usedColumn = definition.Columns.Single(static column => column.FieldKey == "used");
        Assert.Equal(CbsTableBodyMode.BooleanIcon, usedColumn.BodyMode);
        Assert.Equal(CbsTableColumnAlignment.Center, usedColumn.Alignment);
        Assert.Equal("used", usedColumn.FilterField);
        Assert.Equal("used", usedColumn.SortField);

        var nameColumn = definition.Columns.Single(static column => column.FieldKey == "name");
        Assert.Equal("person.full_name", nameColumn.DisplayField);
        Assert.Equal("person.person_name.naming.fio", nameColumn.FilterField);
        Assert.Equal("person.person_name.naming.surname", nameColumn.SortField);

        var contragentColumn = definition.Columns.Single(static column => column.FieldKey == "contragent");
        Assert.Equal("contragent.name", contragentColumn.DisplayField);
        Assert.Equal("org.name_or_org.full_name", contragentColumn.FilterField);
        Assert.Equal("org.name_or_org.full_name", contragentColumn.SortField);

        var positionColumn = definition.Columns.Single(static column => column.FieldKey == "position");
        Assert.Equal("position.name", positionColumn.DisplayField);
        Assert.Equal("position.name", positionColumn.FilterField);
        Assert.Equal("position.name", positionColumn.SortField);

        var contactsColumn = definition.Columns.Single(static column => column.FieldKey == "contacts");
        Assert.Equal("person.contacts.name", contactsColumn.DisplayField);
        Assert.Equal("person.person_contacts.contact.value", contactsColumn.FilterField);
        Assert.False(contactsColumn.IsSortable);
        Assert.True(contactsColumn.IsFilterable);

        var priorityColumn = definition.Columns.Single(static column => column.FieldKey == "priority");
        Assert.Equal("priority", priorityColumn.DisplayField);
        Assert.Equal(DataFilterMode.Numeric, priorityColumn.Filter.Mode);
        Assert.Equal(CbsTableColumnAlignment.Right, priorityColumn.Alignment);

        var descriptionColumn = definition.Columns.Single(static column => column.FieldKey == "description");
        Assert.Equal("description", descriptionColumn.DisplayField);
        Assert.Equal("description", descriptionColumn.FilterField);
        Assert.Equal("description", descriptionColumn.SortField);
    }

    [Fact]
    public void TryGetByRoute_ReturnsHolidayDefinition()
    {
        var service = CreateService();

        var found = service.TryGetByRoute("/holidays", out var definition);

        Assert.True(found);
        Assert.Equal("Holiday", definition.Model);
        Assert.Equal("card", definition.Preset);
        Assert.Equal("Календарь выходных", definition.EffectiveNavigationDescription);
        Assert.Equal("begin_at", definition.InitialSortField);
        Assert.Equal(DataSortDirection.Descending, definition.InitialSortDirection);
        Assert.Equal(["id", "begin_at", "end_at", "name", "work"], definition.Columns.Select(static column => column.FieldKey));
        Assert.Equal(["id", "begin_at", "end_at", "name", "work"], definition.Fields.Select(static field => field.FieldKey));

        var beginAtColumn = definition.Columns.Single(static column => column.FieldKey == "begin_at");
        Assert.True(beginAtColumn.IsFilterable);
        Assert.Equal(DataFilterMode.Date, beginAtColumn.Filter.Mode);
        Assert.Equal(DataFilterMatchMode.GreaterThanOrEqual, beginAtColumn.Filter.MatchMode);
        Assert.Equal(DataFilterMode.Date, definition.Columns.Single(static column => column.FieldKey == "end_at").Filter.Mode);

        var workColumn = definition.Columns.Single(static column => column.FieldKey == "work");
        Assert.Equal(CbsTableBodyMode.BooleanIcon, workColumn.BodyMode);

        var beginAtField = definition.Fields.Single(static field => field.FieldKey == "begin_at");
        Assert.Equal(ReferenceFieldEditorType.Date, beginAtField.EditorType);
        Assert.True(beginAtField.IsRequired);
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
    public async Task TryGetByRoute_AppliesSavedWidthForProfileNestedColumns()
    {
        var settingsService = CreateSettingsService();
        await settingsService.SaveAsync(new()
        {
            Tables =
            {
                ["/users"] = new()
                {
                    Columns =
                    {
                        ["department"] = new() { Width = "18rem" },
                        ["person"] = new() { Width = "20rem" }
                    }
                }
            }
        });

        var service = new ReferenceDefinitionService(settingsService);

        var found = service.TryGetByRoute("/users", out var definition);

        Assert.True(found);
        Assert.Equal("18rem", definition.Columns.Single(static column => column.FieldKey == "department").Width);
        Assert.Equal("20rem", definition.Columns.Single(static column => column.FieldKey == "person").Width);
        Assert.Equal("department.name", definition.Columns.Single(static column => column.FieldKey == "department").DisplayField);
        Assert.Equal("user.person.full_name", definition.Columns.Single(static column => column.FieldKey == "person").DisplayField);
    }

    [Fact]
    public async Task SaveSortAsync_PersistsSortForSpecificTable()
    {
        var service = CreateService();

        await service.SaveSortAsync(new ReferenceTableSortSettings
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

    [Fact]
    public async Task TryGetByRoute_AppliesSavedSortFromSettingsFile()
    {
        var settingsService = CreateSettingsService();
        await settingsService.SaveAsync(new()
        {
            Tables =
            {
                ["/references/Status"] = new()
                {
                    Sort = new()
                    {
                        FieldKey = "order",
                        Direction = "Descending"
                    }
                }
            }
        });

        var service = new ReferenceDefinitionService(settingsService);

        var found = service.TryGetByRoute("/references/Status", out var definition);

        Assert.True(found);
        Assert.Equal("order", definition.InitialSortField);
        Assert.Equal(DataSortDirection.Descending, definition.InitialSortDirection);
    }

    [Fact]
    public async Task TryGetByRoute_AppliesSavedSortForProfileNestedColumns()
    {
        var settingsService = CreateSettingsService();
        await settingsService.SaveAsync(new()
        {
            Tables =
            {
                ["/users"] = new()
                {
                    Sort = new()
                    {
                        FieldKey = "department",
                        Direction = "Descending"
                    }
                }
            }
        });

        var service = new ReferenceDefinitionService(settingsService);

        var found = service.TryGetByRoute("/users", out var definition);

        Assert.True(found);
        Assert.Equal(ReferenceEditorKind.Profile, definition.EditorKind);
        Assert.Equal("department", definition.InitialSortField);
        Assert.Equal(DataSortDirection.Descending, definition.InitialSortDirection);
        Assert.Equal("department.name", definition.Columns.Single(static column => column.FieldKey == "department").SortField);
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
            InitialSortField = "amount",
            InitialSortDirection = DataSortDirection.Descending,
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
                    DisplayField = "amount.display",
                    FilterField = "amount_id",
                    SortField = "amount_sort",
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
                        Mode = DataFilterMode.Text,
                        EditorKind = CbsTableFilterEditorKind.MultiSelect,
                        MatchMode = DataFilterMatchMode.In,
                        OptionsSourceKey = "Department",
                        EmptySelectionText = "Все",
                        StaticOptions =
                        [
                            new CbsTableFilterOptionDefinition
                            {
                                Value = 1,
                                Label = "Отдел продаж"
                            }
                        ]
                    }
                }
            ]
        };

        var clone = definition.Clone();
        var amountColumn = clone.Columns.Single(static column => column.FieldKey == "amount");
        var flagColumn = clone.Columns.Single(static column => column.FieldKey == "flag");

        Assert.Equal(CbsTableColumnAlignment.Right, amountColumn.Alignment);
        Assert.Equal(CbsTableColumnAlignment.Center, flagColumn.Alignment);
        Assert.Equal("amount.display", amountColumn.DisplayField);
        Assert.Equal("amount_id", amountColumn.FilterField);
        Assert.Equal("amount_sort", amountColumn.SortField);
        Assert.Equal(DataFilterMode.Numeric, amountColumn.Filter.Mode);
        Assert.Equal(DataFilterMode.Text, flagColumn.Filter.Mode);
        Assert.Equal(CbsTableFilterEditorKind.MultiSelect, flagColumn.Filter.EditorKind);
        Assert.Equal(DataFilterMatchMode.In, flagColumn.Filter.MatchMode);
        Assert.Equal("Department", flagColumn.Filter.OptionsSourceKey);
        Assert.Equal("Все", flagColumn.Filter.EmptySelectionText);
        Assert.Equal("Отдел продаж", flagColumn.Filter.StaticOptions.Single().Label);
        Assert.Equal("amount", clone.InitialSortField);
        Assert.Equal(DataSortDirection.Descending, clone.InitialSortDirection);
        Assert.Equal(ReferenceFieldEditorType.Number, clone.Fields.Single(static field => field.FieldKey == "amount").EditorType);
        Assert.True(clone.Fields.Single(static field => field.FieldKey == "amount").IsRequired);
        Assert.True(clone.Fields.Single(static field => field.FieldKey == "amount").IsReadOnlyOnEdit);
        Assert.Equal(ReferenceEditorKind.Generic, clone.EditorKind);
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
