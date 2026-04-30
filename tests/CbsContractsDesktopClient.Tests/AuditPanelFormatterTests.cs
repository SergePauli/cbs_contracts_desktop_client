using CbsContractsDesktopClient.Services.Shell;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public class AuditPanelFormatterTests
{
    [Theory]
    [InlineData("added", "Добавлено:")]
    [InlineData("updated", "Изменено:")]
    [InlineData("removed", "Удалено:")]
    [InlineData("deleted", "Удалено:")]
    [InlineData("archived", "Архивировано:")]
    [InlineData("imported", "Импорт:")]
    [InlineData(null, "Событие:")]
    public void GetActionTitle_MapsApiConstantsToTimelineTitles(string? action, string expected)
    {
        Assert.Equal(expected, AuditPanelFormatter.GetActionTitle(action));
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("deleted")]
    [InlineData(":removed")]
    [InlineData(" DELETED ")]
    public void GetActionBrushKey_UsesRemovedBackgroundForDeletedAndRemovedDisplayOnly(string action)
    {
        Assert.Equal("ShellAuditRemovedBackgroundBrush", AuditPanelFormatter.GetActionBrushKey(action));
    }

    [Theory]
    [InlineData("added", 0)]
    [InlineData("updated", 1)]
    [InlineData("removed", 2)]
    [InlineData("archived", 3)]
    [InlineData("imported", 4)]
    [InlineData(":imported", 4)]
    public void GetActionFilterValue_MapsFilterConstantsToBackendSmallint(string action, int expected)
    {
        Assert.Equal(expected, AuditPanelFormatter.GetActionFilterValue(action));
    }

    [Theory]
    [InlineData("deleted")]
    [InlineData(":deleted")]
    [InlineData("")]
    [InlineData(null)]
    public void GetActionFilterValue_DoesNotSendDisplayOnlyDeletedAction(string? action)
    {
        Assert.Null(AuditPanelFormatter.GetActionFilterValue(action));
    }
}
