using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace Pauli.WinUiKit.Controls;

public sealed class Dropdown : UserControl
{
    private readonly TextBox _textBox = new();
    private readonly Button _dropButton = new();
    private readonly FontIcon _chevron = new();
    private readonly ListView _listView = new();
    private readonly Flyout _flyout;
    private bool _isSyncingText;
    private bool _isSyncingListSelection;

    public Dropdown()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        _textBox.VerticalContentAlignment = VerticalAlignment.Center;
        _textBox.TextAlignment = TextAlignment.Right;
        _textBox.MinWidth = 0;
        _textBox.Padding = new Thickness(6, 1, 22, 0);
        _textBox.GotFocus += OnTextBoxGotFocus;
        _textBox.TextChanged += OnTextChanged;

        _chevron.Glyph = "\uE70D";
        _chevron.FontSize = 10;
        _chevron.VerticalAlignment = VerticalAlignment.Center;
        _chevron.HorizontalAlignment = HorizontalAlignment.Center;

        _dropButton.HorizontalAlignment = HorizontalAlignment.Right;
        _dropButton.VerticalAlignment = VerticalAlignment.Stretch;
        _dropButton.Width = 18;
        _dropButton.MinWidth = 18;
        _dropButton.MinHeight = 0;
        _dropButton.Padding = new Thickness(0);
        _dropButton.BorderThickness = new Thickness(0);
        _dropButton.Background = new SolidColorBrush(Colors.Transparent);
        _dropButton.BorderBrush = new SolidColorBrush(Colors.Transparent);
        _dropButton.Content = _chevron;
        _dropButton.Click += (_, _) => ToggleDropDown();
        SuppressButtonChrome();

        _listView.SelectionMode = ListViewSelectionMode.Single;
        _listView.IsItemClickEnabled = true;
        _listView.ItemClick += OnItemClick;
        _listView.SelectionChanged += OnListSelectionChanged;

        _flyout = new Flyout
        {
            Content = _listView,
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
        };
        _flyout.Closed += (_, _) => DropDownClosed?.Invoke(this, EventArgs.Empty);

        Items.CollectionChanged += OnItemsChanged;
        Content = BuildEditor();
    }

    public ObservableCollection<object> Items { get; } = [];

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(Dropdown),
        new PropertyMetadata(null, OnItemsSourceChanged));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(object),
        typeof(Dropdown),
        new PropertyMetadata(null, OnSelectedItemChanged));

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public int SelectedIndex
    {
        get
        {
            var items = GetItems();
            return SelectedItem is null ? -1 : items.IndexOf(SelectedItem);
        }
        set
        {
            var items = GetItems();
            SelectedItem = value >= 0 && value < items.Count
                ? items[value]
                : null;
        }
    }

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(Dropdown),
        new PropertyMetadata(string.Empty, OnTextPropertyChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty DisplayMemberPathProperty = DependencyProperty.Register(
        nameof(DisplayMemberPath),
        typeof(string),
        typeof(Dropdown),
        new PropertyMetadata(string.Empty, OnDisplayPropertyChanged));

    public string DisplayMemberPath
    {
        get => (string)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public static readonly DependencyProperty TextMemberPathProperty = DependencyProperty.Register(
        nameof(TextMemberPath),
        typeof(string),
        typeof(Dropdown),
        new PropertyMetadata(string.Empty, OnDisplayPropertyChanged));

    public string TextMemberPath
    {
        get => (string)GetValue(TextMemberPathProperty);
        set => SetValue(TextMemberPathProperty, value);
    }

    public static readonly DependencyProperty MatchMemberPathProperty = DependencyProperty.Register(
        nameof(MatchMemberPath),
        typeof(string),
        typeof(Dropdown),
        new PropertyMetadata(string.Empty));

    public string MatchMemberPath
    {
        get => (string)GetValue(MatchMemberPathProperty);
        set => SetValue(MatchMemberPathProperty, value);
    }

    public static readonly DependencyProperty CompactChevronHorizontalPaddingProperty = DependencyProperty.Register(
        nameof(CompactChevronHorizontalPadding),
        typeof(double),
        typeof(Dropdown),
        new PropertyMetadata(2d, OnChevronPaddingChanged));

    public double CompactChevronHorizontalPadding
    {
        get => (double)GetValue(CompactChevronHorizontalPaddingProperty);
        set => SetValue(CompactChevronHorizontalPaddingProperty, value);
    }

    public static readonly DependencyProperty NormalChevronHorizontalPaddingProperty = DependencyProperty.Register(
        nameof(NormalChevronHorizontalPadding),
        typeof(double),
        typeof(Dropdown),
        new PropertyMetadata(4d, OnChevronPaddingChanged));

    public double NormalChevronHorizontalPadding
    {
        get => (double)GetValue(NormalChevronHorizontalPaddingProperty);
        set => SetValue(NormalChevronHorizontalPaddingProperty, value);
    }

    public static readonly DependencyProperty UseCompactDensityProperty = DependencyProperty.Register(
        nameof(UseCompactDensity),
        typeof(bool?),
        typeof(Dropdown),
        new PropertyMetadata(null, OnChevronPaddingChanged));

    public bool? UseCompactDensity
    {
        get => (bool?)GetValue(UseCompactDensityProperty);
        set => SetValue(UseCompactDensityProperty, value);
    }

    public event EventHandler? DropDownOpened;

    public event EventHandler? DropDownClosed;

    public event EventHandler? SelectionChanged;

    private static void OnItemsSourceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var dropdown = (Dropdown)dependencyObject;
        dropdown.RebuildListItems();
        dropdown.TrySelectItemByText();
    }

    private static void OnSelectedItemChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var dropdown = (Dropdown)dependencyObject;
        dropdown.SyncTextFromSelection();
        dropdown.SyncListSelection();
        dropdown.SelectionChanged?.Invoke(dropdown, EventArgs.Empty);
    }

    private static void OnTextPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var dropdown = (Dropdown)dependencyObject;
        dropdown.SyncEditorText((string?)args.NewValue ?? string.Empty);
        dropdown.TrySelectItemByText();
    }

    private static void OnDisplayPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var dropdown = (Dropdown)dependencyObject;
        dropdown.RebuildListItems();
        dropdown.SyncTextFromSelection();
    }

    private static void OnChevronPaddingChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((Dropdown)dependencyObject).ApplyChevronPadding();
    }

    private UIElement BuildEditor()
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        grid.Children.Add(_textBox);
        grid.Children.Add(_dropButton);
        ApplyChevronPadding();
        return grid;
    }

    private void ToggleDropDown()
    {
        if (_flyout.IsOpen)
        {
            _flyout.Hide();
            return;
        }

        DropDownOpened?.Invoke(this, EventArgs.Empty);
        RebuildListItems();
        SyncListSelection();
        SetFlyoutWidth();
        _flyout.ShowAt(_textBox);
    }

    private void OnTextChanged(object sender, TextChangedEventArgs args)
    {
        if (_isSyncingText)
        {
            return;
        }

        SetValue(TextProperty, _textBox.Text);
        TrySelectItemByText();
    }

    private void OnTextBoxGotFocus(object sender, RoutedEventArgs args)
    {
        _textBox.Select(_textBox.Text.Length, 0);
    }

    private void OnItemClick(object sender, ItemClickEventArgs args)
    {
        if (!TryGetListItemValue(args.ClickedItem, out var item))
        {
            return;
        }

        SelectedItem = item;
        _flyout.Hide();
    }

    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_isSyncingListSelection || !TryGetListItemValue(_listView.SelectedItem, out var item))
        {
            return;
        }

        SelectedItem = item;
        _flyout.Hide();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        RebuildListItems();
        TrySelectItemByText();
    }

    private void RebuildListItems()
    {
        var items = GetItems();
        _listView.Items.Clear();

        foreach (var item in items)
        {
            _listView.Items.Add(new ListViewItem
            {
                Tag = item,
                MinHeight = 24,
                Padding = new Thickness(6, 2, 6, 2),
                Content = new TextBlock
                {
                    Text = GetItemDisplayText(item),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap
                }
            });
        }
    }

    private void TrySelectItemByText()
    {
        var text = Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            SelectedItem = null;
            return;
        }

        var item = GetItems().FirstOrDefault(item => MatchesItem(item, text));
        if (item is null)
        {
            return;
        }

        if (ReferenceEquals(item, SelectedItem))
        {
            return;
        }

        SelectedItem = item;
    }

    private void SyncTextFromSelection()
    {
        SyncEditorText(SelectedItem is null ? string.Empty : GetItemText(SelectedItem));
    }

    private void SyncEditorText(string text)
    {
        if (string.Equals(_textBox.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        _isSyncingText = true;
        try
        {
            _textBox.Text = text;
            SetValue(TextProperty, text);
        }
        finally
        {
            _isSyncingText = false;
        }
    }

    private void SyncListSelection()
    {
        _isSyncingListSelection = true;
        try
        {
            _listView.SelectedItem = _listView.Items
                .OfType<ListViewItem>()
                .FirstOrDefault(item => ReferenceEquals(item.Tag, SelectedItem));
        }
        finally
        {
            _isSyncingListSelection = false;
        }
    }

    private void SetFlyoutWidth()
    {
        if (ActualWidth > 0)
        {
            _listView.MinWidth = ActualWidth;
        }
    }

    private List<object> GetItems()
    {
        return ItemsSource?.Cast<object>().ToList() ?? Items.ToList();
    }

    private string GetItemDisplayText(object item)
    {
        return GetItemPropertyText(item, DisplayMemberPath);
    }

    private string GetItemText(object item)
    {
        return GetItemPropertyText(item, TextMemberPath);
    }

    private string GetItemMatchText(object item)
    {
        return GetItemPropertyText(item, string.IsNullOrWhiteSpace(MatchMemberPath) ? TextMemberPath : MatchMemberPath);
    }

    private bool MatchesItem(object item, string text)
    {
        var displayText = GetItemDisplayText(item);
        return string.Equals(GetItemText(item), text, StringComparison.OrdinalIgnoreCase)
            || string.Equals(displayText, text, StringComparison.OrdinalIgnoreCase)
            || string.Equals(GetTextAfterDash(displayText), text, StringComparison.OrdinalIgnoreCase)
            || string.Equals(GetItemMatchText(item), text, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetTextAfterDash(string text)
    {
        var separatorIndex = text.IndexOf(" - ", StringComparison.Ordinal);
        return separatorIndex < 0
            ? text
            : text[(separatorIndex + 3)..].Trim();
    }

    private static string GetItemPropertyText(object item, string propertyName)
    {
        if (item is string text)
        {
            return text;
        }

        if (item is ComboBoxItem comboBoxItem)
        {
            return comboBoxItem.Content?.ToString() ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(propertyName))
        {
            var property = item.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(property => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
            return property?.GetValue(item)?.ToString() ?? string.Empty;
        }

        return item.ToString() ?? string.Empty;
    }

    private static bool TryGetListItemValue(object? listItem, out object item)
    {
        if (listItem is ListViewItem { Tag: object tag })
        {
            item = tag;
            return true;
        }

        if (listItem is not null)
        {
            item = listItem;
            return true;
        }

        item = new object();
        return false;
    }

    private void ApplyChevronPadding()
    {
        var horizontalPadding = ShouldUseCompactPadding()
            ? CompactChevronHorizontalPadding
            : NormalChevronHorizontalPadding;
        _chevron.Margin = new Thickness(horizontalPadding, 0, horizontalPadding, 0);
    }

    private bool ShouldUseCompactPadding()
    {
        if (UseCompactDensity is { } explicitValue)
        {
            return explicitValue;
        }

        var measuredHeight = ActualHeight > 0
            ? ActualHeight
            : _textBox.ActualHeight;
        return measuredHeight > 0 && measuredHeight <= 28;
    }

    private void SuppressButtonChrome()
    {
        var transparent = new SolidColorBrush(Colors.Transparent);
        _dropButton.Resources["ButtonBackgroundPointerOver"] = transparent;
        _dropButton.Resources["ButtonBorderBrushPointerOver"] = transparent;
        _dropButton.Resources["ButtonBackgroundPressed"] = transparent;
        _dropButton.Resources["ButtonBorderBrushPressed"] = transparent;
    }
}
