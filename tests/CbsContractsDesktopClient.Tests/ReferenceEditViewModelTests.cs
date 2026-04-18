using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ReferenceEditViewModelTests
{
    [Fact]
    public void CreateForCreate_HidesIdFieldGeneratedByApi()
    {
        var definition = CreateDefinition();

        var viewModel = ReferenceEditViewModel.CreateForCreate(definition);

        Assert.Equal("Тестовый справочник: новая запись", viewModel.DialogTitle);
        Assert.Equal("Создать", viewModel.PrimaryButtonText);
        Assert.DoesNotContain(viewModel.Fields, static item => item.FieldKey == "id");

        var nameField = viewModel.Fields.Single(static item => item.FieldKey == "name");
        Assert.False(nameField.IsReadOnly);
        Assert.True(nameField.IsRequired);
        Assert.False(viewModel.CanSubmit);
    }

    [Fact]
    public void CreateForEdit_KeepsIdFieldReadOnly()
    {
        var definition = CreateDefinition();
        var row = new ReferenceDataRow
        {
            Values =
            {
                ["id"] = JsonValue(15),
                ["name"] = JsonValue("Исходное имя"),
                ["used"] = JsonValue(true)
            }
        };

        var viewModel = ReferenceEditViewModel.CreateForEdit(definition, row);

        var idField = viewModel.Fields.Single(static item => item.FieldKey == "id");

        Assert.True(idField.IsReadOnly);
        Assert.Equal(ReferenceFieldEditorType.Number, idField.EditorType);
    }

    [Fact]
    public void DirtyFields_ThenPayloadBuilder_ReturnsDirtyEditableFieldsAndId()
    {
        var definition = CreateDefinition();
        var row = new ReferenceDataRow
        {
            Values =
            {
                ["id"] = JsonValue(15),
                ["name"] = JsonValue("Исходное имя"),
                ["used"] = JsonValue(true)
            }
        };

        var viewModel = ReferenceEditViewModel.CreateForEdit(definition, row);

        viewModel.Fields.Single(static item => item.FieldKey == "name").TextValue = "Новое имя";
        viewModel.Fields.Single(static item => item.FieldKey == "used").BoolValue = false;

        var dirtyFields = viewModel.DirtyFields.ToArray();
        var payload = ReferenceEditPayloadBuilder.BuildForUpdate(viewModel);

        Assert.True(viewModel.HasChanges);
        Assert.True(viewModel.CanSubmit);
        Assert.Equal(["name", "used"], dirtyFields.Select(static item => item.FieldKey));
        Assert.Equal(3, payload.Count);
        Assert.Equal(15L, payload["id"]);
        Assert.Equal("Новое имя", payload["name"]);
        Assert.Equal(false, payload["used"]);
    }

    [Fact]
    public void CreatePayloadBuilder_UsesEditableFieldsWithValuesOnly()
    {
        var definition = CreateDefinition();
        var viewModel = ReferenceEditViewModel.CreateForCreate(definition);

        viewModel.Fields.Single(static item => item.FieldKey == "name").TextValue = "Новая запись";
        viewModel.Fields.Single(static item => item.FieldKey == "used").BoolValue = true;

        var payload = ReferenceEditPayloadBuilder.BuildForCreate(viewModel);

        Assert.Equal(2, payload.Count);
        Assert.Equal("Новая запись", payload["name"]);
        Assert.Equal(true, payload["used"]);
        Assert.DoesNotContain("id", payload.Keys);
    }

    [Fact]
    public void EditMode_SingleEditableField_BecomesDirtyImmediatelyAfterValueChange()
    {
        var definition = new ReferenceDefinition
        {
            Route = "/references/SingleField",
            Model = "SingleField",
            Title = "Single field",
            Fields =
            [
                new ReferenceFieldDefinition
                {
                    FieldKey = "id",
                    Label = "ID",
                    EditorType = ReferenceFieldEditorType.Number,
                    IsRequired = true,
                    IsReadOnlyOnCreate = true,
                    IsReadOnlyOnEdit = true
                },
                new ReferenceFieldDefinition
                {
                    FieldKey = "name",
                    Label = "Name",
                    EditorType = ReferenceFieldEditorType.Text,
                    IsRequired = true
                }
            ]
        };

        var row = new ReferenceDataRow
        {
            Values =
            {
                ["id"] = JsonValue(42),
                ["name"] = JsonValue("Old value")
            }
        };

        var viewModel = ReferenceEditViewModel.CreateForEdit(definition, row);
        var nameField = viewModel.Fields.Single(static item => item.FieldKey == "name");

        Assert.False(viewModel.HasChanges);
        Assert.False(viewModel.CanSubmit);

        nameField.TextValue = "New value";

        Assert.True(nameField.IsDirty);
        Assert.True(viewModel.HasChanges);
        Assert.True(viewModel.CanSubmit);
        Assert.Equal(["name"], viewModel.DirtyFields.Select(static item => item.FieldKey));
    }

    [Fact]
    public void CreateMode_UsesShortDescriptionText()
    {
        var definition = CreateDefinition();

        var viewModel = ReferenceEditViewModel.CreateForCreate(definition);

        Assert.Equal("Заполните поля новой записи", viewModel.DescriptionText);
    }

    [Fact]
    public void EditMode_UsesShortDescriptionText()
    {
        var definition = CreateDefinition();
        var row = new ReferenceDataRow
        {
            Values =
            {
                ["id"] = JsonValue(15),
                ["name"] = JsonValue("Исходное имя")
            }
        };

        var viewModel = ReferenceEditViewModel.CreateForEdit(definition, row);

        Assert.Equal("Измените только нужные поля", viewModel.DescriptionText);
    }

    [Fact]
    public void NumericField_InvalidValue_BlocksSubmit()
    {
        var definition = CreateDefinition();
        var viewModel = ReferenceEditViewModel.CreateForCreate(definition);

        viewModel.Fields.Single(static item => item.FieldKey == "name").TextValue = "Новая запись";

        var numericField = new ReferenceEditFieldViewModel(
            new ReferenceFieldDefinition
            {
                FieldKey = "priority",
                Label = "Приоритет",
                EditorType = ReferenceFieldEditorType.Number,
                IsRequired = true
            },
            isCreateMode: true);

        numericField.TextValue = "abc";

        Assert.True(numericField.HasValidationError);
        Assert.Equal("Введите корректное числовое значение.", numericField.ValidationMessage);
    }

    private static ReferenceDefinition CreateDefinition()
    {
        return new ReferenceDefinition
        {
            Route = "/references/Test",
            Model = "Test",
            Title = "Тестовый справочник",
            Fields =
            [
                new ReferenceFieldDefinition
                {
                    FieldKey = "id",
                    Label = "ID",
                    EditorType = ReferenceFieldEditorType.Number,
                    IsRequired = true,
                    IsReadOnlyOnCreate = true,
                    IsReadOnlyOnEdit = true
                },
                new ReferenceFieldDefinition
                {
                    FieldKey = "name",
                    Label = "Наименование",
                    EditorType = ReferenceFieldEditorType.Text,
                    IsRequired = true
                },
                new ReferenceFieldDefinition
                {
                    FieldKey = "used",
                    Label = "Исп.",
                    EditorType = ReferenceFieldEditorType.Boolean
                }
            ]
        };
    }

    private static System.Text.Json.JsonElement JsonValue<TValue>(TValue value)
    {
        return System.Text.Json.JsonSerializer.SerializeToElement(value);
    }
}
