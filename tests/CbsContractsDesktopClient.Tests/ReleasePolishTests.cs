using System.IO;
using System.Text.Json;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.Orders;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ReleasePolishTests
{
    [Fact]
    public void OrderCompositionPolicy_IgnoresInvoiceNumberWhenStatusIsZero()
    {
        var order = CreateRow(
            ("status", new { id = 0, name = "Запрос цены" }),
            ("order_number", "INV-42"));

        Assert.True(OrderCompositionPolicy.CanModifyPositions(order));
    }

    [Fact]
    public void OrderCompositionPolicy_UsesOnlyLinkedOrderStatusForPosition()
    {
        var editable = CreateRow(("order", new
        {
            id = 10,
            order_number = "INV-42",
            status = new { id = 0, name = "Запрос цены" }
        }));
        var locked = CreateRow(("order", new
        {
            id = 10,
            order_number = (string?)null,
            status = new { id = 1, name = "Следующий статус" }
        }));

        Assert.True(OrderCompositionPolicy.CanModifyPosition(editable));
        Assert.False(OrderCompositionPolicy.CanModifyPosition(locked));
    }

    [Fact]
    public void OrderEditPayloadBuilder_CreateAllowsMissingInvoiceNumber()
    {
        var state = OrderEditState.CreateNew();
        var status = new CbsTableFilterOptionDefinition { Value = 0L, Label = "Запрос цены" };
        var viewModel = new OrderEditViewModel(state, [status], (_, _) => Task.FromResult<IReadOnlyList<CbsTableFilterOptionDefinition>>([]))
        {
            SelectedStatus = status,
            SelectedContragent = new CbsTableFilterOptionDefinition { Value = 7L, Label = "Поставщик" },
            OrderNumber = string.Empty
        };

        var payload = OrderEditPayloadBuilder.BuildForCreate(viewModel);

        Assert.False(payload.ContainsKey("order_number"));
        Assert.Equal(7L, payload["contragent_id"]);
        Assert.Equal(0L, payload["order_status_id"]);
    }

    [Fact]
    public void NeedsOrderLookupFiltersOnlyByOrderStatus()
    {
        var path = TestProjectPaths.FromRepositoryRoot("src", "Stores", "Orders", "StageOrderNeedsStore.cs");
        var code = File.ReadAllText(path);

        Assert.Contains("[\"order_status_id\"] = 0L", code);
        Assert.DoesNotContain("order_number__null", code);
    }

    [Fact]
    public void AuditStoreBuildsOrOfAndGroupsForStageOrders()
    {
        var path = TestProjectPaths.FromRepositoryRoot("src", "Stores", "Table", "AuditStore.cs");
        var code = File.ReadAllText(path);

        Assert.Contains("filters[\"or\"] = new object[]", code);
        Assert.Contains("[\"and\"] = new Dictionary<string, object?>", code);
        Assert.Contains("[\"auditable_type__eq\"] = \"StageOrder\"", code);
        Assert.Contains("[\"auditable_id__in\"] = stageOrderIds", code);
    }

    [Fact]
    public void SupplyFlyoutUsesCompactAmountColumn()
    {
        var path = TestProjectPaths.FromRepositoryRoot("src", "Views", "Orders", "StageSupplyEditFlyout.cs");
        var code = File.ReadAllText(path);

        Assert.Contains("new ColumnDefinition { Width = new GridLength(96) }", code);
    }

    private static TableDataRow CreateRow(params (string Key, object? Value)[] values)
    {
        return new TableDataRow
        {
            Values = values.ToDictionary(
                static item => item.Key,
                static item => JsonSerializer.SerializeToElement(item.Value))
        };
    }
}
