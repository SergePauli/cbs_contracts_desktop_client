// Provides route-host UI primitives shared by concrete content host views.
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.References;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace CbsContractsDesktopClient.Views.Shell
{
    public abstract class ContentHostViewBase : UserControl
    {
        private readonly ContentHostDialogCoordinator _dialogCoordinator;

        protected ContentHostViewBase()
        {
            _dialogCoordinator = new ContentHostDialogCoordinator(() => XamlRoot);
        }

        protected async Task ShowErrorDialogAsync(string title, string message)
        {
            await _dialogCoordinator.ShowErrorAsync(title, message);
        }

        protected async Task ShowInfoDialogAsync(string title, string message)
        {
            await _dialogCoordinator.ShowInfoAsync(title, message);
        }

        protected async Task<bool> ConfirmDialogAsync(
            string title,
            string message,
            string primaryButtonText,
            string closeButtonText = "Отмена",
            ContentDialogButton defaultButton = ContentDialogButton.Primary,
            bool applyChrome = false)
        {
            return await _dialogCoordinator.ConfirmAsync(
                title,
                message,
                primaryButtonText,
                closeButtonText,
                defaultButton,
                applyChrome);
        }

        protected static void ShowSuccessNotification(string title, string message)
        {
            try
            {
                var notification = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(message)
                    .BuildNotification();

                AppNotificationManager.Default.Show(notification);
            }
            catch (COMException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        protected static long? TryGetSelectedRowId(TableDataRow row)
        {
            var rawId = row.GetValue("id");
            return rawId switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string stringValue when long.TryParse(stringValue, out var parsedValue) => parsedValue,
                _ => null
            };
        }
    }
}
