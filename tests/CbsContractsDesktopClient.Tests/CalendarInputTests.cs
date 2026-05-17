using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class CalendarInputTests
{
    private static readonly string CalendarInputPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Pauli.WinUiKit",
        "Controls",
        "CalendarInput.cs");

    [Fact]
    public void CalendarInput_ExposesReadOnlyMode()
    {
        var code = File.ReadAllText(CalendarInputPath);

        Assert.Contains("public static readonly DependencyProperty IsReadOnlyProperty", code);
        Assert.Contains("_textBox.IsReadOnly = IsReadOnly;", code);
        Assert.Contains("_clearButton.IsEnabled = !IsReadOnly;", code);
        Assert.Contains("_calendarButton.IsEnabled = !IsReadOnly;", code);
        Assert.Contains("if (_isSyncing || IsReadOnly)", code);
    }
}
