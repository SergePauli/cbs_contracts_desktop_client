using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Shared.Dialogs;

public enum StageEditDialogNavigationDirection
{
    None,
    Previous,
    Next
}

public sealed record StageEditDialogNavigationState(
    bool CanPrevious,
    bool CanNext);

public sealed record StageEditDialogNavigationResult(
    ViewModels.Workflow.EditStates.StageEditState Stage,
    ViewModels.Workflow.EditStates.ContractEditState? Contract,
    StageEditDialogNavigationState NavigationState);

public static class StageEditDialogNavigationControls
{
    private static readonly Windows.UI.Color ButtonBackgroundColor = ColorHelper.FromArgb(255, 99, 102, 241);
    private static readonly Windows.UI.Color ButtonHoverColor = ColorHelper.FromArgb(255, 79, 70, 229);
    private static readonly Windows.UI.Color ButtonPressedColor = ColorHelper.FromArgb(255, 67, 56, 202);

    public static FrameworkElement BuildTitle(
        string title,
        string? accentText,
        StageEditDialogNavigationState? state,
        Action<StageEditDialogNavigationDirection> navigate)
    {
        ArgumentNullException.ThrowIfNull(navigate);

        state ??= new StageEditDialogNavigationState(false, false);
        var hasNavigation = state.CanPrevious || state.CanNext;
        if (!hasNavigation)
        {
            return (FrameworkElement)AppDialogLayout.BuildDialogSectionTitle(title, accentText);
        }

        var grid = new Grid
        {
            Margin = new Thickness(0, 2, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnSpacing = 6,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            }
        };

        var previous = BuildNavigationButton(
            "\uE76B",
            "Предыдущий этап",
            state.CanPrevious,
            () => navigate(StageEditDialogNavigationDirection.Previous));
        Grid.SetColumn(previous, 1);
        grid.Children.Add(previous);

        var titleBlock = BuildTitleText(title, accentText);
        Grid.SetColumn(titleBlock, 2);
        grid.Children.Add(titleBlock);

        var next = BuildNavigationButton(
            "\uE76C",
            "Следующий этап",
            state.CanNext,
            () => navigate(StageEditDialogNavigationDirection.Next));
        Grid.SetColumn(next, 3);
        grid.Children.Add(next);

        return grid;
    }

    private static FrameworkElement BuildTitleText(string title, string? accentText)
    {
        var textBlock = new TextBlock
        {
            FontSize = 14,
            LineHeight = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Application.Current.Resources["ShellPrimaryTextBrush"] as Brush,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (string.IsNullOrWhiteSpace(accentText))
        {
            textBlock.Text = title;
        }
        else
        {
            textBlock.Inlines.Add(new Run { Text = title + " " });
            textBlock.Inlines.Add(new Run
            {
                Text = accentText,
                Foreground = Application.Current.Resources["ShellAccentBrush"] as Brush
            });
        }

        return textBlock;
    }

    private static Button BuildNavigationButton(string glyph, string tooltip, bool isEnabled, Action click)
    {
        var foreground = new SolidColorBrush(Colors.White);
        var button = new Button
        {
            Width = 24,
            Height = 24,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            IsEnabled = isEnabled,
            Background = new SolidColorBrush(ButtonBackgroundColor),
            BorderBrush = new SolidColorBrush(ButtonBackgroundColor),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(5),
            Content = new FontIcon
            {
                Glyph = glyph,
                FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                FontSize = 12,
                Foreground = foreground
            }
        };
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(ButtonHoverColor);
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(ButtonPressedColor);
        button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(ButtonHoverColor);
        button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(ButtonPressedColor);
        ToolTipService.SetToolTip(button, tooltip);
        button.Click += (_, _) => click();
        return button;
    }
}
