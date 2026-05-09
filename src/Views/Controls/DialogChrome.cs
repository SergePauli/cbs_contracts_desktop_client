using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.Controls
{
    public static class DialogChrome
    {
        private const string ContentHostTag = "DialogChrome.ContentHost";

        public static void Apply(ContentDialog dialog)
        {
            ArgumentNullException.ThrowIfNull(dialog);

            ApplyCompactResources(dialog);
            dialog.BorderThickness = new Thickness(0);
            dialog.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            ApplyButtonMargins(dialog);
            EnsureContentMargin(dialog);

            if (dialog.Title is string title)
            {
                dialog.Title = BuildTitle(dialog, title);
            }
        }

        public static void Apply(ContentDialog dialog, string title)
        {
            ArgumentNullException.ThrowIfNull(dialog);

            ApplyCompactResources(dialog);
            dialog.BorderThickness = new Thickness(0);
            dialog.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            ApplyButtonMargins(dialog);
            EnsureContentMargin(dialog);
            dialog.Title = BuildTitle(dialog, title);
        }

        private static void ApplyCompactResources(ContentDialog dialog)
        {
            dialog.Resources["ContentDialogPadding"] = new Thickness(0);
            dialog.Resources["ContentDialogTitleMargin"] = new Thickness(0);
            dialog.Resources["ContentDialogCommandSpaceMargin"] = new Thickness(0);
            dialog.Resources["ContentDialogContentMargin"] = new Thickness(0);
            dialog.Resources["ContentDialogContentScrollViewerMargin"] = new Thickness(0);
            dialog.Resources["ContentDialogBorderWidth"] = new Thickness(0);
            dialog.Resources["ContentDialogSeparatorThickness"] = new Thickness(0);
            dialog.Resources["ContentDialogBorderBrush"] = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        private static void ApplyButtonMargins(ContentDialog dialog)
        {
            var primaryStyle = CreateDialogButtonStyle(dialog.PrimaryButtonStyle);
            var secondaryStyle = CreateDialogButtonStyle(dialog.SecondaryButtonStyle);
            var closeStyle = CreateDialogButtonStyle(dialog.CloseButtonStyle);

            dialog.PrimaryButtonStyle = primaryStyle;
            dialog.SecondaryButtonStyle = secondaryStyle;
            dialog.CloseButtonStyle = closeStyle;
        }

        private static Style CreateDialogButtonStyle(Style? basedOn)
        {
            var style = new Style(typeof(Button));
            if (basedOn is not null)
            {
                style.BasedOn = basedOn;
            }

            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(4)));
            return style;
        }

        private static void EnsureContentMargin(ContentDialog dialog)
        {
            WrapContent(dialog);
        }

        private static void WrapContent(ContentDialog dialog)
        {
            if (dialog.Content is null)
            {
                return;
            }

            if (dialog.Content is Border { Tag: ContentHostTag })
            {
                return;
            }

            dialog.Content = new Border
            {
                Tag = ContentHostTag,
                Margin = new Thickness(4),
                Child = dialog.Content as UIElement
                    ?? new TextBlock
                    {
                        Text = dialog.Content.ToString(),
                        TextWrapping = TextWrapping.Wrap
                    }
            };
        }

        private static UIElement BuildTitle(ContentDialog dialog, string title)
        {
            var root = new Border
            {
                Background = Application.Current.Resources["ShellSidebarBackgroundBrush"] as Brush,
                Padding = new Thickness(4),
                MinWidth = ReadDialogWidth(dialog),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var grid = new Grid
            {
                ColumnSpacing = 4
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = title,
                TextWrapping = TextWrapping.WrapWholeWords,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Application.Current.Resources["ShellPrimaryTextBrush"] as Brush,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            grid.Children.Add(titleBlock);

            var closeButton = new Button
            {
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Content = new FontIcon
                {
                    Glyph = "\uE711",
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 11
                }
            };
            ToolTipService.SetToolTip(closeButton, "Закрыть");
            closeButton.Click += (_, _) => dialog.Hide();

            Grid.SetColumn(closeButton, 1);
            grid.Children.Add(closeButton);
            root.Child = grid;
            return root;
        }

        private static double ReadDialogWidth(ContentDialog dialog)
        {
            if (dialog.Resources.TryGetValue("ContentDialogMaxWidth", out var maxWidth)
                && maxWidth is double width
                && width > 0)
            {
                return width;
            }

            if (dialog.Resources.TryGetValue("ContentDialogMinWidth", out var minWidth)
                && minWidth is double min
                && min > 0)
            {
                return min;
            }

            return 548;
        }
    }
}
