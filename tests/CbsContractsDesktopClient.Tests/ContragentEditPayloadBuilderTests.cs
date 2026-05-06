using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.References;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ContragentEditPayloadBuilderTests
{
    [Fact]
    public void BuildForCreate_BuildsNestedOrganizationAddressAndContacts()
    {
        var viewModel = new ContragentEditViewModel(new ContragentEditDialogState
        {
            Definition = Definition(),
            IsCreateMode = true,
            ObjUuid = "obj-1"
        })
        {
            Inn = "7707083893",
            Kpp = "770701001",
            Division = "123",
            SelectedOwnershipId = 5,
            Name = "ООО Ромашка",
            SelectedRegionId = 77,
            AddressReal = "Москва",
            FullName = "Общество с ограниченной ответственностью Ромашка",
            ContactsText = "info@example.com",
            Description = "Описание",
            Ogrn = "1027700132195",
            BankName = "Банк"
        };

        var payload = ContragentEditPayloadBuilder.BuildForCreate(viewModel);

        Assert.Equal("obj-1", payload["obj_uuid"]);
        Assert.Equal("Описание", payload["description"]);
        Assert.Equal("Банк", payload["bank_name"]);

        var requisites = Assert.IsAssignableFrom<IEnumerable<object?>>(payload["contragent_organizations_attributes"]);
        var requisitesItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(requisites));
        var organization = Assert.IsType<Dictionary<string, object?>>(requisitesItem["organization_attributes"]);
        Assert.Equal("ООО Ромашка", organization["name"]);
        Assert.Equal("7707083893", organization["inn"]);
        Assert.Equal("770701001", organization["kpp"]);
        Assert.Equal("123", organization["division"]);
        Assert.Equal(5L, organization["ownership_id"]);
        Assert.Equal("1027700132195", organization["ogrn"]);

        var addresses = Assert.IsAssignableFrom<IEnumerable<object?>>(payload["contragent_addresses_attributes"]);
        var addressItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(addresses));
        var address = Assert.IsType<Dictionary<string, object?>>(addressItem["address_attributes"]);
        Assert.Equal("real", addressItem["kind"]);
        Assert.Equal("Москва", address["value"]);
        Assert.Equal(77L, address["area_id"]);

        var contacts = Assert.IsAssignableFrom<IEnumerable<object?>>(payload["contragent_contacts_attributes"]);
        var contactItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(contacts));
        var contact = Assert.IsType<Dictionary<string, object?>>(contactItem["contact_attributes"]);
        Assert.Equal("info@example.com", contact["value"]);
        Assert.Equal("Email", contact["type"]);
    }

    [Fact]
    public void BuildForUpdate_WithChangedOrganization_UpdatesCurrentRequisites()
    {
        var viewModel = new ContragentEditViewModel(new ContragentEditDialogState
        {
            Definition = Definition(),
            IsCreateMode = false,
            Id = 10,
            RequisitesId = 20,
            RequisitesListKey = "old-list",
            OrganizationId = 30,
            Inn = "1",
            OwnershipId = 5,
            Name = "Old"
        })
        {
            Inn = "2",
            SelectedOwnershipId = 6,
            Name = "New"
        };

        var payload = ContragentEditPayloadBuilder.BuildForUpdate(viewModel);

        Assert.Equal(10L, payload["id"]);
        var requisites = Assert.IsAssignableFrom<object?[]>(payload["contragent_organizations_attributes"]);
        var currentRequisites = Assert.IsType<Dictionary<string, object?>>(Assert.Single(requisites));
        Assert.Equal(20L, currentRequisites["id"]);
        Assert.Equal("old-list", currentRequisites["list_key"]);
        var organization = Assert.IsType<Dictionary<string, object?>>(currentRequisites["organization_attributes"]);
        Assert.Equal(30L, organization["id"]);
        Assert.Equal("New", organization["name"]);
        Assert.Equal("2", organization["inn"]);
        Assert.Equal(6L, organization["ownership_id"]);
    }

    [Fact]
    public void BuildForUpdate_WithActivatedRegistration_SendsUsedDeltaForBothRegistrations()
    {
        var oldRegistration = new ContragentOrganizationHistoryItem
        {
            Id = 20,
            OrganizationId = 30,
            ListKey = "old-list",
            OriginalIsActive = true,
            IsActive = true,
            Inn = "1",
            OwnershipId = 5,
            Name = "Old"
        };
        var newRegistration = new ContragentOrganizationHistoryItem
        {
            Id = 21,
            OrganizationId = 31,
            ListKey = "new-list",
            OriginalIsActive = false,
            IsActive = false,
            Inn = "2",
            OwnershipId = 6,
            Name = "New"
        };
        var viewModel = new ContragentEditViewModel(new ContragentEditDialogState
        {
            Definition = Definition(),
            IsCreateMode = false,
            Id = 10,
            RequisitesId = 20,
            RequisitesListKey = "old-list",
            OrganizationId = 30,
            Inn = "1",
            OwnershipId = 5,
            Name = "Old",
            OrganizationHistory = [oldRegistration, newRegistration]
        });

        Assert.True(viewModel.ActivateRegistration(newRegistration));
        var payload = ContragentEditPayloadBuilder.BuildForUpdate(viewModel);

        var requisites = Assert.IsAssignableFrom<object?[]>(payload["contragent_organizations_attributes"]);
        Assert.Equal(2, requisites.Length);
        var oldRequisites = Assert.IsType<Dictionary<string, object?>>(requisites[0]);
        var newRequisites = Assert.IsType<Dictionary<string, object?>>(requisites[1]);
        Assert.Equal(20L, oldRequisites["id"]);
        Assert.False(Assert.IsType<bool>(oldRequisites["used"]));
        Assert.Equal(21L, newRequisites["id"]);
        Assert.True(Assert.IsType<bool>(newRequisites["used"]));
        Assert.Equal("2", viewModel.Inn);
        Assert.Equal("New", viewModel.Name);
        Assert.Equal(6L, viewModel.SelectedOwnershipId);
    }

    [Fact]
    public void BuildForUpdate_WithDestroyedArchivedRegistration_SendsDestroyAttribute()
    {
        var archivedRegistration = new ContragentOrganizationHistoryItem
        {
            Id = 21,
            OrganizationId = 31,
            ListKey = "archived-list",
            OriginalIsActive = false,
            IsActive = false,
            Inn = "2",
            OwnershipId = 6,
            Name = "Archived"
        };
        var viewModel = new ContragentEditViewModel(new ContragentEditDialogState
        {
            Definition = Definition(),
            IsCreateMode = false,
            Id = 10,
            Inn = "1",
            OwnershipId = 5,
            Name = "Active",
            OrganizationHistory =
            [
                new ContragentOrganizationHistoryItem
                {
                    Id = 20,
                    OriginalIsActive = true,
                    IsActive = true,
                    Inn = "1",
                    OwnershipId = 5,
                    Name = "Active"
                },
                archivedRegistration
            ]
        });

        Assert.True(viewModel.MarkRegistrationForDestroy(archivedRegistration));
        var payload = ContragentEditPayloadBuilder.BuildForUpdate(viewModel);

        var requisites = Assert.IsAssignableFrom<object?[]>(payload["contragent_organizations_attributes"]);
        var destroyed = Assert.IsType<Dictionary<string, object?>>(Assert.Single(requisites));
        Assert.Equal(21L, destroyed["id"]);
        Assert.Equal("archived-list", destroyed["list_key"]);
        Assert.Equal("1", destroyed["_destroy"]);
        Assert.DoesNotContain(archivedRegistration, viewModel.VisibleOrganizationHistory);
    }

    [Fact]
    public void BuildForLegalEntityChange_DeactivatesOldRegistrationAndKeepsUserContragentChanges()
    {
        var oldRegistration = new ContragentOrganizationHistoryItem
        {
            Id = 20,
            OrganizationId = 30,
            ListKey = "old-list",
            OriginalIsActive = true,
            IsActive = false,
            Inn = "1",
            OwnershipId = 5,
            Name = "Old"
        };
        var newRegistration = new ContragentOrganizationHistoryItem
        {
            ListKey = "new-list",
            OriginalIsActive = false,
            IsActive = true,
            Inn = "2",
            OwnershipId = 6,
            Name = "New"
        };
        var viewModel = new ContragentEditViewModel(new ContragentEditDialogState
        {
            Definition = Definition(),
            IsCreateMode = false,
            Id = 10,
            RequisitesId = 20,
            RequisitesListKey = "old-list",
            OrganizationId = 30,
            Inn = "2",
            OwnershipId = 6,
            Name = "New",
            Description = "Оставить как есть",
            AddressReal = "Оставить адрес",
            ContactsText = "keep@example.com",
            BankName = "Оставить банк",
            OrganizationHistory = [newRegistration, oldRegistration]
        })
        {
            FullName = "New Full",
            Ogrn = "1027700132195",
            Description = "Не должно уйти",
            AddressReal = "Не должно уйти",
            ContactsText = "drop@example.com",
            BankName = "Не должно уйти"
        };

        var payload = ContragentEditPayloadBuilder.BuildForLegalEntityChange(viewModel);

        Assert.Equal(10L, payload["id"]);
        Assert.Equal("Не должно уйти", payload["description"]);
        Assert.Equal("Не должно уйти", payload["bank_name"]);
        Assert.True(payload.ContainsKey("contragent_addresses_attributes"));
        Assert.True(payload.ContainsKey("contragent_contacts_attributes"));
        var requisites = Assert.IsAssignableFrom<object?[]>(payload["contragent_organizations_attributes"]);
        Assert.Equal(2, requisites.Length);
        var oldRequisites = Assert.IsType<Dictionary<string, object?>>(requisites[0]);
        Assert.Equal(20L, oldRequisites["id"]);
        Assert.Equal("old-list", oldRequisites["list_key"]);
        Assert.False(Assert.IsType<bool>(oldRequisites["used"]));
        var newRequisites = Assert.IsType<Dictionary<string, object?>>(requisites[1]);
        Assert.Equal("new-list", newRequisites["list_key"]);
        Assert.True(Assert.IsType<bool>(newRequisites["used"]));
        var organization = Assert.IsType<Dictionary<string, object?>>(newRequisites["organization_attributes"]);
        Assert.Equal("New", organization["name"]);
        Assert.Equal("2", organization["inn"]);
        Assert.Equal(6L, organization["ownership_id"]);
        Assert.Equal("New Full", organization["full_name"]);
        Assert.Equal("1027700132195", organization["ogrn"]);
    }

    [Fact]
    public void BuildForCreate_WithSelectedAddress_UsesAddressId()
    {
        var viewModel = new ContragentEditViewModel(new ContragentEditDialogState
        {
            Definition = Definition(),
            IsCreateMode = true,
            ObjUuid = "obj-1"
        })
        {
            Inn = "7707083893",
            SelectedOwnershipId = 5,
            Name = "ООО Ромашка",
            AddressReal = "Москва"
        };
        viewModel.SelectAddressOption(new CbsTableFilterOptionDefinition
        {
            Value = 77L,
            Label = "Москва"
        });

        var payload = ContragentEditPayloadBuilder.BuildForCreate(viewModel);

        var addresses = Assert.IsAssignableFrom<IEnumerable<object?>>(payload["contragent_addresses_attributes"]);
        var addressItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(addresses));
        Assert.Equal("real", addressItem["kind"]);
        Assert.Equal(77L, addressItem["address_id"]);
        Assert.False(addressItem.ContainsKey("address_attributes"));
    }

    [Fact]
    public void BuildForUpdate_WithSelectedAddress_UpdatesAddressIdOnly()
    {
        var viewModel = new ContragentEditViewModel(new ContragentEditDialogState
        {
            Definition = Definition(),
            IsCreateMode = false,
            Id = 10,
            Inn = "7707083893",
            OwnershipId = 5,
            Name = "ООО Ромашка",
            RealAddressId = 20,
            RealAddressListKey = "addr-link",
            AddressRealAddressId = 76,
            AddressReal = "Старый адрес"
        })
        {
            AddressReal = "Новый адрес"
        };
        viewModel.SelectAddressOption(new CbsTableFilterOptionDefinition
        {
            Value = 77L,
            Label = "Новый адрес"
        });

        var payload = ContragentEditPayloadBuilder.BuildForUpdate(viewModel);

        var addresses = Assert.IsAssignableFrom<object?[]>(payload["contragent_addresses_attributes"]);
        var addressItem = Assert.IsType<Dictionary<string, object?>>(Assert.Single(addresses));
        Assert.Equal(20L, addressItem["id"]);
        Assert.Equal("addr-link", addressItem["list_key"]);
        Assert.Equal(77L, addressItem["address_id"]);
        Assert.False(addressItem.ContainsKey("_destroy"));
        Assert.False(addressItem.ContainsKey("address_attributes"));
    }

    private static ReferenceDefinition Definition()
    {
        return new ReferenceDefinition
        {
            Route = "/contragents",
            Model = "Contragent",
            Title = "Контрагенты",
            Preset = "card",
            EditorKind = ReferenceEditorKind.Contragent
        };
    }
}
