// Coordinates common shell dialogs for shell host views.
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
            ContentDialog? dialog = null;
            dialog = CreateDialog(title, BuildMessageContent(
                message,
                BuildFooterButton("ОК", "\uE73E", true, () => dialog!.Hide())));
            await dialog.ShowAsync();
        }

        public async Task ShowInfoAsync(string title, string message)
        {
            ContentDialog? dialog = null;
            dialog = CreateDialog(title, BuildMessageContent(
                message,
                BuildFooterButton("Закрыть", "\uE711", false, () => dialog!.Hide())));
            await dialog.ShowAsync();
        }

        public async Task<bool> ConfirmAsync(
            string title,
            string message,
            string primaryButtonText,
            string closeButtonText = "Отмена",
            ContentDialogButton defaultButton = ContentDialogButton.Primary)
        {
            var confirmed = false;
            ContentDialog? dialog = null;
            dialog = CreateDialog(title, BuildMessageContent(
                message,
                BuildFooterButton(primaryButtonText, "\uE73E", true, () =>
                {
                    confirmed = true;
                    dialog!.Hide();
                }),
                BuildFooterButton(closeButtonText, "\uE711", false, () => dialog!.Hide())));

            await dialog.ShowAsync();
            return confirmed;
        }

        private ContentDialog CreateDialog(string title, object content)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = _xamlRootAccessor()
                    ?? throw new InvalidOperationException("Shell host XamlRoot is required to show a dialog."),
                Title = title,
                PrimaryButtonText = string.Empty,
                SecondaryButtonText = string.Empty,
                CloseButtonText = string.Empty,
                DefaultButton = ContentDialogButton.None,
                Content = content
            };
            DialogChrome.Apply(dialog);
            return dialog;
        }

        private static FrameworkElement BuildMessageContent(string message, params Button[] buttons)
        {
            var root = new Grid
            {
                MinWidth = 360,
                RowSpacing = 10
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                Text = message,
                Margin = new Thickness(4, 2, 4, 0),
                TextWrapping = TextWrapping.WrapWholeWords
            });

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 4
            };
            foreach (var button in buttons)
            {
                footer.Children.Add(button);
            }
            Grid.SetRow(footer, 1);
            root.Children.Add(footer);
            return root;
        }

        private static Button BuildFooterButton(string text, string glyph, bool isPrimary, Action click)
        {
            var foreground = Application.Current.Resources[
                    isPrimary ? "ShellTableRowSelectedBorderBrush" : "ShellSecondaryTextBrush"] as Brush
                ?? new SolidColorBrush(isPrimary ? Microsoft.UI.Colors.SeaGreen : Microsoft.UI.Colors.DimGray);
            var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

            var button = new Button
            {
                Width = 112,
                MinHeight = 28,
                Padding = new Thickness(8, 2, 8, 2),
                Background = transparent,
                BorderBrush = transparent,
                BorderThickness = new Thickness(0),
                Foreground = foreground,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new FontIcon
                        {
                            Glyph = glyph,
                            FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                            FontSize = 12,
                            Foreground = foreground
                        },
                        new TextBlock
                        {
                            Text = text,
                            Foreground = foreground,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 232, 235, 239));
            button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 225, 231));
            button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 232, 235, 239));
            button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 225, 231));
            button.Click += (_, _) => click();
            return button;
        }
    }
}
