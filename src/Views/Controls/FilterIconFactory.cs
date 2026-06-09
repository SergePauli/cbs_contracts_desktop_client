using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.Controls
{
    internal static class FilterIconFactory
    {
        public static UIElement BuildFilterClearIcon()
        {
            var iconHost = new Grid
            {
                Width = 16,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            iconHost.Children.Add(new FontIcon
            {
                Glyph = "\uE71C",
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 16,
                Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"]
            });

            iconHost.Children.Add(new FontIcon
            {
                Glyph = "\uE733",
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 16,
                Foreground = (Brush)Application.Current.Resources["StageDeadlineTextBrush"]
            });

            return iconHost;
        }
    }
}
