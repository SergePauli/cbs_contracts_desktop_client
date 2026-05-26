using System.IO;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ReferenceEditDialogTests
{
    private static readonly string ReferenceEditDialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Views",
        "References",
        "ReferenceEditDialog.cs");

    [Fact]
    public void ReferenceEditDialog_UpdatesPrimaryButtonFromTextChanged()
    {
        var code = File.ReadAllText(ReferenceEditDialogPath);

        Assert.Contains("public sealed class ReferenceEditDialog : AppEditDialog", code);
        Assert.Contains("Content = BuildEditContent(BuildContent());", code);
        Assert.Contains("public override bool Validate()", code);
        Assert.Contains("textBox.TextChanged += (_, _) =>", code);
        Assert.Contains("datePicker.DateChanged += (_, _) => UpdatePrimaryButtonState();", code);
        Assert.Contains("UpdatePrimaryButtonState();", code);
        Assert.Contains("SyncTextEditorsToViewModel();", code);
        Assert.DoesNotContain("IsPrimaryButtonEnabled = ViewModel.CanSubmit;", code);
        Assert.DoesNotContain("LostFocus", code);
    }
}
