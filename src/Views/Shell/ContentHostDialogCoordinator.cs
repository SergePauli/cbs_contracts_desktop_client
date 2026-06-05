// Coordinates common shell dialogs for shell host views.
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Shell
{
    internal sealed class ContentHostDialogCoordinator
    {
        private readonly Func<XamlRoot?> _xamlRootAccessor;

        public ContentHostDialogCoordinator(Func<XamlRoot?> xamlRootAccessor)
        {
            _xamlRootAccessor = xamlRootAccessor;
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            var dialog = CreateDialog(title, message);
            DialogChrome.Apply(dialog);

            await dialog.ShowAsync();
        }

        public async Task ShowInfoAsync(string title, string message)
        {
            await CreateDialog(title, message).ShowAsync();
        }

        public async Task<bool> ConfirmAsync(
            string title,
            string message,
            string primaryButtonText,
            string closeButtonText = "Отмена",
            ContentDialogButton defaultButton = ContentDialogButton.Primary,
            bool applyChrome = false)
        {
            var dialog = CreateDialog(title, message);
            dialog.PrimaryButtonText = primaryButtonText;
            dialog.CloseButtonText = closeButtonText;
            dialog.DefaultButton = defaultButton;

            if (applyChrome)
            {
                DialogChrome.Apply(dialog);
            }

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        private ContentDialog CreateDialog(string title, object content)
        {
            return new ContentDialog
            {
                XamlRoot = _xamlRootAccessor()
                    ?? throw new InvalidOperationException("Shell host XamlRoot is required to show a dialog."),
                Title = title,
                CloseButtonText = "Закрыть",
                DefaultButton = ContentDialogButton.Close,
                Content = content
            };
        }
    }
}
