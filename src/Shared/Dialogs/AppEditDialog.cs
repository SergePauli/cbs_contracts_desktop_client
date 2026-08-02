using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Shared.Dialogs;

public abstract class AppEditDialog : ContentDialog
{
    private static readonly Windows.UI.Color DialogBackgroundColor = ColorHelper.FromArgb(255, 246, 247, 248);
    private static readonly Windows.UI.Color FooterButtonHoverColor = ColorHelper.FromArgb(255, 232, 235, 239);
    private static readonly Windows.UI.Color FooterButtonPressedColor = ColorHelper.FromArgb(255, 220, 225, 231);

    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    protected AppEditDialog()
    {
        PrimaryButtonText = string.Empty;
        SecondaryButtonText = string.Empty;
        CloseButtonText = string.Empty;
        DefaultButton = ContentDialogButton.None;
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("ms-appx:///Microsoft.UI.Xaml/DensityStyles/Compact.xaml")
        });
        Background = new SolidColorBrush(DialogBackgroundColor);
        Resources["ContentDialogBackground"] = new SolidColorBrush(DialogBackgroundColor);
        Resources["ContentDialogTopOverlay"] = new SolidColorBrush(DialogBackgroundColor);

        ErrorText.Foreground = new SolidColorBrush(Colors.IndianRed);
        ErrorText.HorizontalAlignment = HorizontalAlignment.Center;
        ErrorText.TextAlignment = TextAlignment.Center;
        ErrorText.TextWrapping = TextWrapping.Wrap;
        ErrorText.Visibility = Visibility.Collapsed;

        _saveButton = BuildFooterButton("Сохранить", "\uE73E", true);
        _cancelButton = BuildFooterButton("Отмена", "\uE711", false);
        _saveButton.Click += SaveButton_Click;
        _cancelButton.Click += (_, _) =>
        {
            WasSaved = false;
            Hide();
        };
    }

    protected TextBlock ErrorText { get; } = new();

    public bool WasSaved { get; private set; }

    public event Func<AppEditDialogSaveRequestedEventArgs, Task>? SaveRequestedAsync;

    public abstract bool Validate();

    public void ShowErrorInfo(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    protected UIElement BuildEditContent(FrameworkElement body)
    {
        var root = new Grid
        {
            Background = new SolidColorBrush(DialogBackgroundColor),
            RowSpacing = 8
        };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(body, 0);
        root.Children.Add(body);

        Grid.SetRow(ErrorText, 1);
        root.Children.Add(ErrorText);

        var footer = BuildFooter();
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        return root;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Validate())
        {
            return;
        }

        var args = new AppEditDialogSaveRequestedEventArgs();
        SetFooterEnabled(false);
        try
        {
            if (SaveRequestedAsync is not null)
            {
                foreach (var handler in SaveRequestedAsync.GetInvocationList().Cast<Func<AppEditDialogSaveRequestedEventArgs, Task>>())
                {
                    await handler(args);
                    if (args.Cancel)
                    {
                        return;
                    }
                }
            }

            WasSaved = true;
            Hide();
        }
        finally
        {
            if (!WasSaved)
            {
                SetFooterEnabled(true);
            }
        }
    }

    private FrameworkElement BuildFooter()
    {
        return new Border
        {
            Padding = new Thickness(0, 6, 0, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 4,
                Children =
                {
                    _saveButton,
                    _cancelButton
                }
            }
        };
    }

    private static Button BuildFooterButton(string text, string glyph, bool isPrimary)
    {
        var foregroundKey = isPrimary
            ? "ShellTableRowSelectedBorderBrush"
            : "ShellSecondaryTextBrush";
        var foreground = Application.Current.Resources[foregroundKey] as Brush
            ?? new SolidColorBrush(isPrimary ? Colors.SeaGreen : Colors.DimGray);
        var transparent = new SolidColorBrush(Colors.Transparent);

        var content = new StackPanel
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
        };

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
            Content = content
        };
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(FooterButtonHoverColor);
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(FooterButtonPressedColor);
        button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(FooterButtonHoverColor);
        button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(FooterButtonPressedColor);
        return button;
    }

    private void SetFooterEnabled(bool isEnabled)
    {
        _saveButton.IsEnabled = isEnabled;
        _cancelButton.IsEnabled = isEnabled;
    }
}

public sealed class AppEditDialogSaveRequestedEventArgs : EventArgs
{
    public bool Cancel { get; set; }
}
