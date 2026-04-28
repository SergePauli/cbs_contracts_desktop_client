using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class EmployeeEditPayloadBuilderTests
{
    [Fact]
    public void BuildForCreate_BuildsNestedPersonPositionContragentAndContacts()
    {
        var viewModel = CreateViewModel(new EmployeeEditDialogState
        {
            Definition = CreateDefinition(),
            IsCreateMode = true,
            IsUsed = true
        });
        viewModel.PersonName = "Иванов Иван";
        viewModel.SelectPositionOption(new CbsTableFilterOptionDefinition { Value = 7L, Label = "Инженер" });
        viewModel.SelectContragentOption(new CbsTableFilterOptionDefinition { Value = 9L, Label = "ООО Ромашка" });
        viewModel.ContactsText = "ivan@example.com";
        viewModel.PriorityText = "3";
        viewModel.Description = "Описание";

        var payload = EmployeeEditPayloadBuilder.BuildForCreate(viewModel);

        Assert.False(payload.ContainsKey("id"));
        Assert.Equal(9L, payload["contragent_id"]);
        Assert.Equal(7L, payload["position_id"]);
        Assert.Equal(3, payload["priority"]);
        Assert.Equal("Описание", payload["description"]);
        Assert.True(payload.ContainsKey("list_key"));

        var personAttributes = Assert.IsType<Dictionary<string, object?>>(payload["person_attributes"]);
        Assert.True(personAttributes.ContainsKey("person_names_attributes"));
        var contacts = Assert.IsAssignableFrom<IEnumerable<object?>>(personAttributes["person_contacts_attributes"]);
        Assert.Single(contacts);
    }

    [Fact]
    public void BuildForUpdate_MapsChangedScalarFieldsAndContactDelta()
    {
        var viewModel = CreateViewModel(new EmployeeEditDialogState
        {
            Definition = CreateDefinition(),
            IsCreateMode = false,
            Id = 15,
            ListKey = "employee-list-key",
            PersonId = 20,
            PersonName = "Иванов Иван",
            PositionId = 7,
            PositionName = "Инженер",
            ContragentId = 9,
            ContragentName = "ООО Ромашка",
            IsUsed = true,
            Priority = 1,
            Description = "old",
            Contacts =
            [
                new EmployeeContactEditItem
                {
                    Id = 31,
                    ListKey = "contact-list-key",
                    Value = "old@example.com",
                    Type = "Email"
                }
            ],
            ContactsText = "old@example.com"
        });
        viewModel.PersonName = "Петров Петр";
        viewModel.PriorityText = "2";
        viewModel.Description = "new";
        viewModel.IsUsed = false;
        viewModel.ContactsText = "new@example.com";

        var payload = EmployeeEditPayloadBuilder.BuildForUpdate(viewModel);

        Assert.Equal(15L, payload["id"]);
        Assert.Equal("employee-list-key", payload["list_key"]);
        Assert.False((bool)payload["used"]!);
        Assert.Equal(2, payload["priority"]);
        Assert.Equal("new", payload["description"]);

        var personAttributes = Assert.IsType<Dictionary<string, object?>>(payload["person_attributes"]);
        Assert.Equal(20L, personAttributes["id"]);
        var contactDelta = Assert.IsType<object?[]>(personAttributes["person_contacts_attributes"]);
        Assert.Equal(2, contactDelta.Length);
        Assert.Contains(contactDelta, item =>
            item is Dictionary<string, object?> dict
            && dict.TryGetValue("_destroy", out var destroy)
            && Equals("1", destroy));
    }

    [Fact]
    public void BuildForUpdate_UsesNewPositionAttributes_WhenPositionTextDoesNotMatchLookup()
    {
        var viewModel = CreateViewModel(new EmployeeEditDialogState
        {
            Definition = CreateDefinition(),
            IsCreateMode = false,
            Id = 15,
            PersonName = "Иванов Иван",
            PositionId = 7,
            PositionName = "Инженер",
            ContragentId = 9,
            ContragentName = "ООО Ромашка",
            Contacts = [new EmployeeContactEditItem { Id = 31, Value = "old@example.com", Type = "Email" }],
            ContactsText = "old@example.com"
        });
        viewModel.PositionInput = "ведущий инженер";
        viewModel.SelectedPositionOption = null;

        var payload = EmployeeEditPayloadBuilder.BuildForUpdate(viewModel);

        var positionAttributes = Assert.IsType<Dictionary<string, object?>>(payload["position_attributes"]);
        Assert.Equal("Ведущий инженер", positionAttributes["name"]);
    }

    [Fact]
    public void CommitContragentInput_RejectsValueThatWasNotSelectedFromOptions()
    {
        var viewModel = CreateViewModel(new EmployeeEditDialogState
        {
            Definition = CreateDefinition(),
            IsCreateMode = true
        });
        viewModel.ContragentOptions =
        [
            new CbsTableFilterOptionDefinition
            {
                Value = 9L,
                Label = "ООО Ромашка"
            }
        ];

        viewModel.CommitContragentInput("Несуществующий контрагент");

        Assert.Null(viewModel.SelectedContragentOption);
        Assert.Equal(string.Empty, viewModel.ContragentInput);
    }

    [Fact]
    public void BuildForCreate_RejectsContactWithUnknownType()
    {
        var viewModel = CreateViewModel(new EmployeeEditDialogState
        {
            Definition = CreateDefinition(),
            IsCreateMode = true,
            IsUsed = true
        });
        viewModel.PersonName = "Иванов Иван";
        viewModel.SelectPositionOption(new CbsTableFilterOptionDefinition { Value = 7L, Label = "Инженер" });
        viewModel.SelectContragentOption(new CbsTableFilterOptionDefinition { Value = 9L, Label = "ООО Ромашка" });
        viewModel.ContactsText = "unknown contact value";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EmployeeEditPayloadBuilder.BuildForCreate(viewModel));

        Assert.Contains("Тип контакта", exception.Message);
    }

    private static EmployeeEditViewModel CreateViewModel(EmployeeEditDialogState state)
    {
        return new EmployeeEditViewModel(state);
    }

    private static ReferenceDefinition CreateDefinition()
    {
        return new ReferenceDefinition
        {
            Route = "/employees",
            Model = "Employee",
            Title = "Сотрудники",
            Preset = "card",
            EditorKind = ReferenceEditorKind.Employee
        };
    }
}
