using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class AppEditDialogTests
{
    private static readonly string DialogPath = TestProjectPaths.FromRepositoryRoot(
        "src",
        "Shared",
        "Dialogs",
        "AppEditDialog.cs");

    [Fact]
    public void AppEditDialog_OwnsCustomFooterInsteadOfContentDialogButtons()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("public abstract class AppEditDialog : ContentDialog", code);
        Assert.Contains("PrimaryButtonText = string.Empty;", code);
        Assert.Contains("SecondaryButtonText = string.Empty;", code);
        Assert.Contains("CloseButtonText = string.Empty;", code);
        Assert.Contains("DefaultButton = ContentDialogButton.None;", code);
        Assert.Contains("BuildEditContent(FrameworkElement body)", code);
        Assert.Contains("BuildFooterButton(\"Сохранить\"", code);
        Assert.Contains("BuildFooterButton(\"Отмена\"", code);
    }

    [Fact]
    public void AppEditDialog_ExposesSavePipelineAndInlineError()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("protected TextBlock ErrorText { get; } = new();", code);
        Assert.Contains("public abstract bool Validate();", code);
        Assert.Contains("public void ShowErrorInfo(string message)", code);
        Assert.Contains("ErrorText.HorizontalAlignment = HorizontalAlignment.Center;", code);
        Assert.Contains("ErrorText.TextAlignment = TextAlignment.Center;", code);
        Assert.Contains("public bool WasSaved { get; private set; }", code);
        Assert.Contains("public static readonly DependencyProperty DialogTitleProperty", code);
        Assert.Contains("public string DialogTitle", code);
        Assert.Contains("public event Func<AppEditDialogSaveRequestedEventArgs, Task>? SaveRequestedAsync;", code);
        Assert.Contains("if (!Validate())", code);
        Assert.Contains("WasSaved = true;", code);
        Assert.Contains("public sealed class AppEditDialogSaveRequestedEventArgs : EventArgs", code);
        Assert.Contains("public bool Cancel { get; set; }", code);
    }

    [Fact]
    public void AppEditDialog_CanSaveWithoutClosingAndReenableFooter()
    {
        var code = File.ReadAllText(DialogPath);

        Assert.Contains("protected void ConfigureSaveWithoutClose(string closeButtonText)", code);
        Assert.Contains("_closeAfterSave = false;", code);
        Assert.Contains("_resetSavedStateOnClose = false;", code);
        Assert.Contains("_cancelButtonLabel.Text = closeButtonText;", code);
        Assert.Contains("SetFooterEnabled(false);", code);
        Assert.Contains("if (_closeAfterSave)", code);
        Assert.Contains("if (!WasSaved || !_closeAfterSave)", code);
    }
}
