using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Shared.Dialogs;

public static class AppDialogLayout
{
    public static UIElement BuildLabeledControl(string label, UIElement control, double spacing = 6)
    {
        var stack = new StackPanel
        {
            Spacing = spacing
        };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        stack.Children.Add(control);
        return stack;
    }

    public static Border BuildEditorGroupPanel(UIElement content, DialogEditorGroupTone tone)
    {
        var backgroundKey = tone switch
        {
            DialogEditorGroupTone.Accent => "ShellAccentPanelBackgroundAltBrush",
            DialogEditorGroupTone.Muted => "ShellMutedPanelBackgroundBrush",
            _ => "ShellPanelBackgroundBrush"
        };

        return new Border
        {
            Padding = new Thickness(10, 8, 10, 10),
            Background = Application.Current.Resources[backgroundKey] as Brush,
            BorderBrush = Application.Current.Resources["ShellPanelBorderBrush"] as Brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = content
        };
    }

    public static UIElement BuildSectionTitle(string title)
    {
        return new Border
        {
            Margin = new Thickness(0, -4, 0, -2),
            Padding = new Thickness(4, 0, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = title,
                FontSize = 11,
                LineHeight = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Application.Current.Resources["ShellTableHeaderTextBrush"] as Brush,
                TextWrapping = TextWrapping.NoWrap
            }
        };
    }

    public static UIElement BuildSummaryLine(string label, string value)
    {
        return BuildSummaryElement(
            label,
            new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(value) ? "-" : value,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            });
    }

    public static UIElement BuildSummaryElement(string label, FrameworkElement valueElement)
    {
        var grid = new Grid
        {
            ColumnSpacing = 8
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(135) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Application.Current.Resources["ShellSecondaryTextBrush"] as Brush
        });

        Grid.SetColumn(valueElement, 1);
        grid.Children.Add(valueElement);
        return grid;
    }

    public static Grid BuildFieldsGrid(double labelWidth = 160, double columnSpacing = 8, double rowSpacing = 8)
    {
        var grid = new Grid
        {
            Padding = new Thickness(0, 4, 0, 0),
            ColumnSpacing = columnSpacing,
            RowSpacing = rowSpacing
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return grid;
    }

    public static void AddFormRow(Grid grid, string labelText, FrameworkElement editor, bool isRequired = false)
    {
        var rowIndex = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = BuildFormLabel(labelText, isRequired);
        Grid.SetRow(label, rowIndex);
        Grid.SetColumn(label, 0);
        grid.Children.Add(label);

        editor.HorizontalAlignment = HorizontalAlignment.Left;
        editor.VerticalAlignment = VerticalAlignment.Center;

        Grid.SetRow(editor, rowIndex);
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);
    }

    public static FrameworkElement BuildFormLabel(string labelText, bool isRequired = false)
    {
        if (!isRequired)
        {
            return new TextBlock
            {
                Text = labelText,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = Application.Current.Resources["ShellSecondaryTextBrush"] as Brush
            };
        }

        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Orientation = Orientation.Horizontal,
            Spacing = 2
        };
        panel.Children.Add(new TextBlock
        {
            Text = labelText,
            TextAlignment = TextAlignment.Right,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = Application.Current.Resources["ShellSecondaryTextBrush"] as Brush
        });
        panel.Children.Add(new TextBlock
        {
            Text = "*",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.Firebrick)
        });

        return panel;
    }
}

public enum DialogEditorGroupTone
{
    Neutral,
    Accent,
    Muted
}
