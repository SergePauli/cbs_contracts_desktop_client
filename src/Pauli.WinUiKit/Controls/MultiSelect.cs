using System.Collections;
using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Pauli.WinUiKit.Controls;

public sealed class MultiSelect : Grid
{
    private readonly Button _button = new();
    private readonly TextBox _searchBox = new();
    private readonly StackPanel _optionsHost = new();
    private readonly List<object> _selectedItems = [];
    private readonly Flyout _flyout;
    private bool _isSyncingValue;

    public MultiSelect()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _button.HorizontalAlignment = HorizontalAlignment.Stretch;
        _button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _button.MinHeight = 32;

        _searchBox.PlaceholderText = "Поиск";
        _searchBox.TextChanged += (_, _) => RebuildOptions();

        _optionsHost.Spacing = 1;
        _flyout = BuildFlyout();
        _button.Flyout = _flyout;

        Children.Add(_button);
        RefreshButtonContent();
    }

    public static readonly DependencyProperty OptionsProperty = DependencyProperty.Register(
        nameof(Options),
        typeof(IEnumerable),
        typeof(MultiSelect),
        new PropertyMetadata(null, OnOptionsChanged));

    public IEnumerable? Options
    {
        get => (IEnumerable?)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(IEnumerable),
        typeof(MultiSelect),
        new PropertyMetadata(null, OnValueChanged));

    public IEnumerable? Value
    {
        get => (IEnumerable?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty OptionLabelProperty = DependencyProperty.Register(
        nameof(OptionLabel),
        typeof(string),
        typeof(MultiSelect),
        new PropertyMetadata("name", OnDisplayPropertyChanged));

    public string OptionLabel
    {
        get => (string)GetValue(OptionLabelProperty);
        set => SetValue(OptionLabelProperty, value);
    }

    public static readonly DependencyProperty DisplayProperty = DependencyProperty.Register(
        nameof(Display),
        typeof(string),
        typeof(MultiSelect),
        new PropertyMetadata("comma", OnDisplayPropertyChanged));

    public string Display
    {
        get => (string)GetValue(DisplayProperty);
        set => SetValue(DisplayProperty, value);
    }

    public static readonly DependencyProperty MaxSelectedLabelsProperty = DependencyProperty.Register(
        nameof(MaxSelectedLabels),
        typeof(int),
        typeof(MultiSelect),
        new PropertyMetadata(3, OnDisplayPropertyChanged));

    public int MaxSelectedLabels
    {
        get => (int)GetValue(MaxSelectedLabelsProperty);
        set => SetValue(MaxSelectedLabelsProperty, value);
    }

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder),
        typeof(string),
        typeof(MultiSelect),
        new PropertyMetadata("Выбрать", OnDisplayPropertyChanged));

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public static readonly DependencyProperty TooltipProperty = DependencyProperty.Register(
        nameof(Tooltip),
        typeof(string),
        typeof(MultiSelect),
        new PropertyMetadata(null, OnDisplayPropertyChanged));

    public string? Tooltip
    {
        get => (string?)GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    private Func<object, string>? _optionItemLabel;

    public Func<object, string>? OptionItemLabel
    {
        get => _optionItemLabel;
        set
        {
            _optionItemLabel = value;
            RebuildOptions();
            RefreshButtonContent();
        }
    }

    public event EventHandler<MultiSelectChangedEventArgs>? SelectionChanged;

    private static void OnOptionsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var multiSelect = (MultiSelect)dependencyObject;
        multiSelect.SyncSelectedItemsFromValue();
        multiSelect.RebuildOptions();
        multiSelect.RefreshButtonContent();
    }

    private static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var multiSelect = (MultiSelect)dependencyObject;
        if (multiSelect._isSyncingValue)
        {
            return;
        }

        multiSelect.SyncSelectedItemsFromValue();
        multiSelect.RebuildOptions();
        multiSelect.RefreshButtonContent();
    }

    private static void OnDisplayPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var multiSelect = (MultiSelect)dependencyObject;
        multiSelect.RebuildOptions();
        multiSelect.RefreshButtonContent();
    }

    private Flyout BuildFlyout()
    {
        var flyoutContent = new StackPanel
        {
            Width = 420,
            Spacing = 2
        };

        var header = new Grid
        {
            Margin = new Thickness(8, 8, 8, 4),
            ColumnSpacing = 6
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var closeButton = new Button
        {
            Width = 30,
            MinWidth = 30,
            Height = 30,
            MinHeight = 30,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new FontIcon
            {
                Glyph = "\uE711",
                FontSize = 10
            }
        };
        closeButton.Click += (_, _) => _flyout.Hide();

        header.Children.Add(_searchBox);
        Grid.SetColumn(closeButton, 1);
        header.Children.Add(closeButton);

        flyoutContent.Children.Add(header);
        flyoutContent.Children.Add(new ScrollViewer
        {
            MaxHeight = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _optionsHost
        });

        return new Flyout
        {
            Content = flyoutContent
        };
    }

    private void SyncSelectedItemsFromValue()
    {
        _selectedItems.Clear();
        if (Value is null)
        {
            return;
        }

        foreach (var item in Value.Cast<object>())
        {
            if (!_selectedItems.Contains(item))
            {
                _selectedItems.Add(item);
            }
        }
    }

    private void RebuildOptions()
    {
        _optionsHost.Children.Clear();

        var options = GetOptions()
            .Where(MatchesSearch)
            .ToList();

        if (options.Count == 0)
        {
            _optionsHost.Children.Add(new TextBlock
            {
                Text = GetOptions().Any() ? "Ничего не найдено" : "Нет доступных опций",
                Margin = new Thickness(8, 4, 8, 6),
                Foreground = Application.Current.Resources["ShellSecondaryTextBrush"] as Brush,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        foreach (var option in options)
        {
            var checkBox = new CheckBox
            {
                Tag = option,
                IsChecked = _selectedItems.Contains(option),
                MinHeight = 24,
                Padding = new Thickness(0),
                Margin = new Thickness(8, 1, 8, 1),
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = new TextBlock
                {
                    Text = GetOptionLabel(option),
                    Margin = new Thickness(2, 0, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            checkBox.Checked += OnOptionChanged;
            checkBox.Unchecked += OnOptionChanged;
            _optionsHost.Children.Add(checkBox);
        }
    }

    private bool MatchesSearch(object option)
    {
        var searchText = _searchBox.Text?.Trim();
        return string.IsNullOrWhiteSpace(searchText)
            || GetOptionLabel(option).Contains(searchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private void OnOptionChanged(object sender, RoutedEventArgs args)
    {
        if (sender is not CheckBox { Tag: object option } checkBox)
        {
            return;
        }

        if (checkBox.IsChecked == true)
        {
            if (!_selectedItems.Contains(option))
            {
                _selectedItems.Add(option);
            }
        }
        else
        {
            _selectedItems.Remove(option);
        }

        UpdateValueFromSelection();
    }

    private void UpdateValueFromSelection()
    {
        var value = _selectedItems.ToList();
        _isSyncingValue = true;
        try
        {
            SetValue(ValueProperty, value);
        }
        finally
        {
            _isSyncingValue = false;
        }

        RefreshButtonContent();
        SelectionChanged?.Invoke(this, new MultiSelectChangedEventArgs(value));
    }

    private void RefreshButtonContent()
    {
        var labels = _selectedItems
            .Select(GetOptionLabel)
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        var displayContent = string.Equals(Display, "chip", StringComparison.OrdinalIgnoreCase)
            ? BuildChipDisplay(labels)
            : BuildTextDisplay(labels);

        _button.Content = BuildButtonContent(displayContent);

        var tooltip = Tooltip;
        if (string.IsNullOrWhiteSpace(tooltip) && labels.Count > 0)
        {
            tooltip = string.Join("; ", labels);
        }

        ToolTipService.SetToolTip(_button, string.IsNullOrWhiteSpace(tooltip) ? null : tooltip);
    }

    private static UIElement BuildButtonContent(UIElement displayContent)
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnSpacing = 6
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var displayElement = displayContent as FrameworkElement
            ?? new ContentControl { Content = displayContent };
        Grid.SetColumn(displayElement, 0);
        grid.Children.Add(displayElement);

        var chevron = new FontIcon
        {
            Glyph = "\uE70D",
            FontSize = 10,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(chevron, 1);
        grid.Children.Add(chevron);

        return grid;
    }

    private UIElement BuildChipDisplay(IReadOnlyList<string> labels)
    {
        if (labels.Count == 0)
        {
            return BuildPlaceholderText();
        }

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var visibleCount = Math.Max(0, MaxSelectedLabels);
        foreach (var label in labels.Take(visibleCount))
        {
            panel.Children.Add(new Border
            {
                MaxWidth = 145,
                Padding = new Thickness(6, 1, 6, 2),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(255, 235, 238, 242)),
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap
                }
            });
        }

        if (labels.Count > visibleCount)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"+{labels.Count - visibleCount}",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Application.Current.Resources["ShellSecondaryTextBrush"] as Brush
            });
        }

        return panel;
    }

    private UIElement BuildTextDisplay(IReadOnlyList<string> labels)
    {
        return new TextBlock
        {
            Text = labels.Count == 0 ? Placeholder : string.Join("; ", labels),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    private UIElement BuildPlaceholderText()
    {
        return new TextBlock
        {
            Text = Placeholder,
            Foreground = Application.Current.Resources["ShellSecondaryTextBrush"] as Brush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    private IReadOnlyList<object> GetOptions()
    {
        return Options?.Cast<object>().ToList() ?? [];
    }

    private string GetOptionLabel(object option)
    {
        if (option is null)
        {
            return string.Empty;
        }

        if (OptionItemLabel is not null)
        {
            return OptionItemLabel(option);
        }

        if (option is string text)
        {
            return text;
        }

        var property = option.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(property => string.Equals(property.Name, OptionLabel, StringComparison.OrdinalIgnoreCase));

        return property?.GetValue(option)?.ToString()
            ?? option.ToString()
            ?? string.Empty;
    }
}
