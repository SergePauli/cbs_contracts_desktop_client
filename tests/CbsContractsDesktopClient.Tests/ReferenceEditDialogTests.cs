using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ReferenceEditDialogTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

    private static readonly string ReferenceEditDialogPath = Path.Combine(
        ProjectRoot,
        "src",
        "Views",
        "References",
        "ReferenceEditDialog.cs");

    [Fact]
    public void ReferenceEditDialog_UpdatesPrimaryButtonFromTextChanged()
    {
        var code = File.ReadAllText(ReferenceEditDialogPath);

        Assert.Contains("textBox.TextChanged += (_, _) =>", code);
        Assert.Contains("UpdatePrimaryButtonState();", code);
        Assert.Contains("IsPrimaryButtonEnabled = ViewModel.CanSubmit;", code);
        Assert.DoesNotContain("LostFocus", code);
    }
}
