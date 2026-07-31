using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace CbsContractsDesktopClient.Views.Orders;

public static class OrderConfirmationFlyout
{
    public static Task<bool> ShowAsync(
        FrameworkElement anchor,
        string title,
        string message,
        string confirmText)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        var completion = new TaskCompletionSource<bool>();
        var flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
        };
        var confirmButton = new Button
        {
            Content = confirmText,
            Padding = new Thickness(10, 3, 10, 3)
        };
        var cancelButton = new Button
        {
            Content = "Отмена",
            Padding = new Thickness(10, 3, 10, 3)
        };
        confirmButton.Click += (_, _) =>
        {
            completion.TrySetResult(true);
            flyout.Hide();
        };
        cancelButton.Click += (_, _) =>
        {
            completion.TrySetResult(false);
            flyout.Hide();
        };
        flyout.Closed += (_, _) => completion.TrySetResult(false);
        flyout.Content = new StackPanel
        {
            Width = 320,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeights.SemiBold
                },
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 6,
                    Children =
                    {
                        confirmButton,
                        cancelButton
                    }
                }
            }
        };
        flyout.ShowAt(anchor);
        return completion.Task;
    }
}
