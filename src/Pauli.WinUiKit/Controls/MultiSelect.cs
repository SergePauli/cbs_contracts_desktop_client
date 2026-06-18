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
    private const double ChipTextFontSize = 14;
    private const double ChipTextLineHeight = 18;
    private const double ChevronColumnWidth = 12;
    private const double ChevronFontSize = 9;

    private readonly Grid _inputHost = new();
    private readonly Border _inputBorder = new();
    private readonly Border _inputBottomBorder = new();
    private readonly ContentControl _inputContent = new();
    private readonly TextBox _searchBox = new();
    private readonly StackPanel _optionsHost = new();
    private readonly List<object> _selectedItems = [];
    private readonly Flyout _flyout;
    private bool _isSyncingValue;

    public MultiSelect()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _inputHost.HorizontalAlignment = HorizontalAlignment.Stretch;
        _inputHost.Background = new SolidColorBrush(Colors.Transparent);
        _inputHost.PointerPressed += (_, _) => FlyoutBase.ShowAttachedFlyout(_inputHost);

        _inputBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
        _inputBorder.VerticalAlignment = VerticalAlignment.Stretch;
        _inputBorder.Background = Application.Current.Resources["TextControlBackground"] as Brush
            ?? new SolidColorBrush(Colors.White);
        _inputBorder.BorderBrush = Application.Current.Resources["TextControlBorderBrush"] as Brush
            ?? new SolidColorBrush(Color.FromArgb(255, 204, 204, 204));
        _inputBorder.BorderThickness = new Thickness(1);
        _inputBorder.CornerRadius = new CornerRadius(2);
        _inputBorder.Padding = new Thickness(6, 2, 4, 2);

        _inputContent.HorizontalAlignment = HorizontalAlignment.Stretch;
        _inputContent.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _inputContent.VerticalAlignment = VerticalAlignment.Center;
        _inputContent.VerticalContentAlignment = VerticalAlignment.Center;

        _inputBottomBorder.Height = 1;
        _inputBottomBorder.VerticalAlignment = VerticalAlignment.Bottom;
        _inputBottomBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
        _inputBottomBorder.Margin = new Thickness(1, 0, 1, 0);
        _inputBottomBorder.Background = Application.Current.Resources["TextControlBorderBrush"] as Brush
            ?? new SolidColorBrush(Color.FromArgb(255, 96, 96, 96));

        _searchBox.PlaceholderText = "Поиск";
        _searchBox.TextChanged += (_, _) => RebuildOptions();

        _optionsHost.Spacing = 1;
        _flyout = BuildFlyout();
        FlyoutBase.SetAttachedFlyout(_inputHost, _flyout);

        _inputHost.Children.Add(_inputBorder);
        _inputBorder.Child = _inputContent;
        _inputHost.Children.Add(_inputBottomBorder);
        Children.Add(_inputHost);
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

        _inputContent.Content = BuildInputContent(displayContent);

        var tooltip = Tooltip;
        if (string.IsNullOrWhiteSpace(tooltip) && labels.Count > 0)
        {
            tooltip = string.Join("; ", labels);
        }

        ToolTipService.SetToolTip(_inputHost, string.IsNullOrWhiteSpace(tooltip) ? null : tooltip);
    }

    private static UIElement BuildInputContent(UIElement displayContent)
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ColumnSpacing = 2
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ChevronColumnWidth) });

        var displayElement = displayContent as FrameworkElement
            ?? new ContentControl { Content = displayContent };
        Grid.SetColumn(displayElement, 0);
        grid.Children.Add(displayElement);

        var chevron = new FontIcon
        {
            Glyph = "\uE70D",
            Width = ChevronColumnWidth,
            FontSize = ChevronFontSize,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
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
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        var visibleCount = Math.Max(0, MaxSelectedLabels);
        foreach (var label in labels.Take(visibleCount))
        {
            panel.Children.Add(new Border
            {
                MaxWidth = 145,
                Padding = new Thickness(5, 0, 5, 0),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(255, 235, 238, 242)),
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = ChipTextFontSize,
                    LineHeight = ChipTextLineHeight,
                    VerticalAlignment = VerticalAlignment.Center,
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
                FontSize = ChipTextFontSize,
                LineHeight = ChipTextLineHeight,
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
            VerticalAlignment = VerticalAlignment.Center,
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
            VerticalAlignment = VerticalAlignment.Center,
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
