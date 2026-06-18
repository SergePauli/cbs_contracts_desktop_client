using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reflection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Pauli.WinUiKit.Controls;

public sealed class Dropdown : UserControl
{
    private static readonly Brush DefaultHighlightBorderBrush =
        new SolidColorBrush(ColorHelper.FromArgb(255, 209, 213, 219));

    private readonly Border _editorBorder = new();
    private readonly TextBox _textBox = new();
    private readonly Button _dropButton = new();
    private readonly FontIcon _chevron = new();
    private readonly ContentControl _selectedContentHost = new();
    private readonly ListView _listView = new();
    private readonly Flyout _flyout;
    private bool _isSyncingText;
    private bool _isSyncingListSelection;
    private bool _isPointerOverEditor;
    private string _lastAcceptedText = string.Empty;

    public Dropdown()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        _textBox.VerticalContentAlignment = VerticalAlignment.Center;
        _textBox.TextAlignment = TextAlignment.Center;
        _textBox.MinWidth = 0;
        _textBox.Padding = new Thickness(6, 1, 22, 0);
        _textBox.GotFocus += OnTextBoxGotFocus;
        _textBox.LostFocus += OnTextBoxLostFocus;
        _textBox.PreviewKeyDown += OnTextBoxPreviewKeyDown;
        _textBox.KeyDown += OnTextBoxKeyDown;
        _textBox.TextChanged += OnTextChanged;

        _selectedContentHost.HorizontalAlignment = HorizontalAlignment.Stretch;
        _selectedContentHost.VerticalAlignment = VerticalAlignment.Center;
        _selectedContentHost.Margin = new Thickness(4, 0, 22, 0);
        _selectedContentHost.HorizontalContentAlignment = HorizontalAlignment.Center;
        _selectedContentHost.VerticalContentAlignment = VerticalAlignment.Center;
        _selectedContentHost.IsHitTestVisible = false;
        _selectedContentHost.Visibility = Visibility.Collapsed;

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

        PointerEntered += (_, _) =>
        {
            _isPointerOverEditor = true;
            ApplyHighlightBrushes();
        };
        PointerExited += (_, _) =>
        {
            _isPointerOverEditor = false;
            ApplyHighlightBrushes();
        };

        _listView.SelectionMode = ListViewSelectionMode.Single;
        _listView.IsItemClickEnabled = true;
        _listView.PreviewKeyDown += OnDropDownPreviewKeyDown;
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
        ApplyHoverBorderBrush();
        ApplyClearButtonMode();
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

    public static readonly DependencyProperty IsClearButtonEnabledProperty = DependencyProperty.Register(
        nameof(IsClearButtonEnabled),
        typeof(bool),
        typeof(Dropdown),
        new PropertyMetadata(true, OnClearButtonModeChanged));

    public bool IsClearButtonEnabled
    {
        get => (bool)GetValue(IsClearButtonEnabledProperty);
        set => SetValue(IsClearButtonEnabledProperty, value);
    }

    public static readonly DependencyProperty AllowCustomOptionsProperty = DependencyProperty.Register(
        nameof(AllowCustomOptions),
        typeof(bool),
        typeof(Dropdown),
        new PropertyMetadata(false));

    public bool AllowCustomOptions
    {
        get => (bool)GetValue(AllowCustomOptionsProperty);
        set => SetValue(AllowCustomOptionsProperty, value);
    }

    public static readonly DependencyProperty HoverBorderBrushProperty = DependencyProperty.Register(
        nameof(HoverBorderBrush),
        typeof(Brush),
        typeof(Dropdown),
        new PropertyMetadata(null, OnHoverBorderBrushChanged));

    public Brush? HoverBorderBrush
    {
        get => (Brush?)GetValue(HoverBorderBrushProperty);
        set => SetValue(HoverBorderBrushProperty, value);
    }

    public static readonly DependencyProperty HighlightBackgroundProperty = DependencyProperty.Register(
        nameof(HighlightBackground),
        typeof(Brush),
        typeof(Dropdown),
        new PropertyMetadata(null, OnHighlightBrushChanged));

    public Brush? HighlightBackground
    {
        get => (Brush?)GetValue(HighlightBackgroundProperty);
        set => SetValue(HighlightBackgroundProperty, value);
    }

    public event EventHandler? DropDownOpened;

    public event EventHandler? DropDownClosed;

    public event EventHandler? SelectionChanged;

    public event EventHandler? SelectionCommitted;

    public Func<object?, UIElement?>? SelectedContentBuilder { get; set; }

    public Func<object, UIElement>? ItemContentBuilder { get; set; }

    public Action<Dropdown, RoutedEventArgs>? OnFocus { get; set; }

    public Action<Dropdown, KeyRoutedEventArgs>? OnTab { get; set; }

    public FrameworkElement? TabTarget { get; set; }

    public bool IsDropDownOpen => _flyout.IsOpen;

    public void OpenDropDown()
    {
        if (_flyout.IsOpen)
        {
            return;
        }

        DropDownOpened?.Invoke(this, EventArgs.Empty);
        RebuildListItems();
        SyncListSelection();
        SetFlyoutWidth();
        _flyout.ShowAt(_textBox);
    }

    public void CloseDropDown()
    {
        if (_flyout.IsOpen)
        {
            _flyout.Hide();
        }
    }

    public bool CommitText()
    {
        return CommitEditorText();
    }

    public bool FocusInput(FocusState focusState = FocusState.Programmatic)
    {
        return _textBox.Focus(focusState);
    }

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

    private static void OnHoverBorderBrushChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((Dropdown)dependencyObject).ApplyHoverBorderBrush();
    }

    private static void OnHighlightBrushChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((Dropdown)dependencyObject).ApplyHighlightBrushes();
    }

    private static void OnClearButtonModeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((Dropdown)dependencyObject).ApplyClearButtonMode();
    }

    private UIElement BuildEditor()
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _editorBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
        _editorBorder.VerticalAlignment = VerticalAlignment.Stretch;
        _editorBorder.BorderThickness = new Thickness(1);
        _editorBorder.CornerRadius = new CornerRadius(3);
        var editorContent = new Grid();
        editorContent.Children.Add(_textBox);
        editorContent.Children.Add(_selectedContentHost);
        _editorBorder.Child = editorContent;
        grid.Children.Add(_editorBorder);
        grid.Children.Add(_dropButton);
        ApplyChevronPadding();
        ApplyHighlightBrushes();
        return grid;
    }

    private void ToggleDropDown()
    {
        if (_flyout.IsOpen)
        {
            _flyout.Hide();
            return;
        }

        OpenDropDown();
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
        if (OnFocus is not null)
        {
            OnFocus(this, args);
            return;
        }

        _lastAcceptedText = _textBox.Text;
        _textBox.Select(_textBox.Text.Length, 0);
        OpenDropDown();
    }

    private void OnTextBoxLostFocus(object sender, RoutedEventArgs args)
    {
        CommitEditorText();
    }

    private void OnTextBoxPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        OnDropDownPreviewKeyDown(sender, args);
    }

    private void OnDropDownPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != Windows.System.VirtualKey.Tab)
        {
            return;
        }

        if (OnTab is not null)
        {
            OnTab(this, args);
        }
        else if (TabTarget is not null)
        {
            CloseDropDown();
            CommitEditorText();
            FocusTabTarget(TabTarget);
        }
        else
        {
            return;
        }

        args.Handled = true;
    }

    private void FocusTabTarget(FrameworkElement target)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (target is Dropdown dropdown)
            {
                dropdown.FocusInput();
                return;
            }

            if (target is CalendarInput calendarInput)
            {
                calendarInput.FocusInput();
                return;
            }

            if (target is TextBox textBox)
            {
                textBox.Focus(FocusState.Programmatic);
                textBox.SelectAll();
                return;
            }

            target.Focus(FocusState.Programmatic);
        });
    }

    private void OnTextBoxKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != Windows.System.VirtualKey.Enter)
        {
            return;
        }

        CommitEditorText();
        args.Handled = true;
    }

    private void OnItemClick(object sender, ItemClickEventArgs args)
    {
        if (!TryGetListItemValue(args.ClickedItem, out var item))
        {
            return;
        }

        SelectedItem = item;
        SelectionCommitted?.Invoke(this, EventArgs.Empty);
        _flyout.Hide();
    }

    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_isSyncingListSelection || !TryGetListItemValue(_listView.SelectedItem, out var item))
        {
            return;
        }

        SelectedItem = item;
        SelectionCommitted?.Invoke(this, EventArgs.Empty);
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
            var listViewItem = new ListViewItem
            {
                Tag = item,
                MinHeight = 24,
                Padding = new Thickness(6, 2, 6, 2),
                Content = ItemContentBuilder?.Invoke(item) ?? new TextBlock
                {
                    Text = GetItemDisplayText(item),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap
                }
            };
            listViewItem.PreviewKeyDown += OnDropDownPreviewKeyDown;
            listViewItem.KeyDown += OnDropDownPreviewKeyDown;
            _listView.Items.Add(listViewItem);
        }
    }

    private void TrySelectItemByText()
    {
        var text = Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            if (!IsClearButtonEnabled)
            {
                return;
            }

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
        _lastAcceptedText = _textBox.Text;
        SyncSelectedContent();
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

    private bool CommitEditorText()
    {
        var previousSelectedItem = SelectedItem;
        var text = Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            if (IsClearButtonEnabled)
            {
                SelectedItem = null;
                _lastAcceptedText = string.Empty;
                return !ReferenceEquals(previousSelectedItem, SelectedItem);
            }

            SyncTextFromSelection();
            return false;
        }

        var item = GetItems().FirstOrDefault(item => MatchesItem(item, text));
        if (item is not null)
        {
            SelectedItem = item;
            _lastAcceptedText = _textBox.Text;
            return !ReferenceEquals(previousSelectedItem, SelectedItem);
        }

        if (AllowCustomOptions && ItemsSource is null)
        {
            Items.Add(text);
            SelectedItem = text;
            _lastAcceptedText = text;
            return !ReferenceEquals(previousSelectedItem, SelectedItem);
        }

        SyncTextFromSelection();
        return false;
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

    private void SyncSelectedContent()
    {
        var selectedContent = SelectedContentBuilder?.Invoke(SelectedItem);
        _selectedContentHost.Content = selectedContent;
        _selectedContentHost.Visibility = selectedContent is null ? Visibility.Collapsed : Visibility.Visible;
        _textBox.Opacity = selectedContent is null ? 1 : 0;
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

    private void ApplyHoverBorderBrush()
    {
        if (HoverBorderBrush is null)
        {
            _textBox.Resources.Remove("TextControlBorderBrushPointerOver");
            _textBox.Resources.Remove("TextControlBorderBrushFocused");
            _textBox.Resources.Remove("TextControlBackgroundPointerOver");
            _textBox.Resources.Remove("TextControlBackgroundFocused");
            return;
        }

        _textBox.Resources["TextControlBorderBrushPointerOver"] = HoverBorderBrush;
        _textBox.Resources["TextControlBorderBrushFocused"] = HoverBorderBrush;
        _textBox.Resources["TextControlBackgroundPointerOver"] = new SolidColorBrush(Colors.Transparent);
        _textBox.Resources["TextControlBackgroundFocused"] = new SolidColorBrush(Colors.Transparent);
    }

    private void ApplyClearButtonMode()
    {
        _textBox.Resources["TextBoxInnerButtonMargin"] = new Thickness(0, 4, IsClearButtonEnabled ? 16 : 22, 4);

        if (IsClearButtonEnabled)
        {
            _textBox.Resources.Remove("TextControlButtonForeground");
            _textBox.Resources.Remove("TextControlButtonForegroundPointerOver");
            _textBox.Resources.Remove("TextControlButtonForegroundPressed");
            _textBox.Resources.Remove("TextControlButtonBackground");
            _textBox.Resources.Remove("TextControlButtonBackgroundPointerOver");
            _textBox.Resources.Remove("TextControlButtonBackgroundPressed");
            return;
        }

        var transparent = new SolidColorBrush(Colors.Transparent);
        _textBox.Resources["TextControlButtonForeground"] = transparent;
        _textBox.Resources["TextControlButtonForegroundPointerOver"] = transparent;
        _textBox.Resources["TextControlButtonForegroundPressed"] = transparent;
        _textBox.Resources["TextControlButtonBackground"] = transparent;
        _textBox.Resources["TextControlButtonBackgroundPointerOver"] = transparent;
        _textBox.Resources["TextControlButtonBackgroundPressed"] = transparent;
    }

    private void ApplyHighlightBrushes()
    {
        var hasHighlightMode = HighlightBackground is not null || HoverBorderBrush is not null;
        _editorBorder.Background = _isPointerOverEditor ? HighlightBackground : null;
        _editorBorder.BorderBrush = hasHighlightMode
            ? _isPointerOverEditor && HoverBorderBrush is not null
                ? HoverBorderBrush
                : DefaultHighlightBorderBrush
            : null;
        _editorBorder.BorderThickness = new Thickness(1);

        if (hasHighlightMode)
        {
            _textBox.Background = new SolidColorBrush(Colors.Transparent);
            _textBox.BorderThickness = new Thickness(0);
            return;
        }

        _textBox.ClearValue(Control.BackgroundProperty);
        _textBox.ClearValue(Control.BorderThicknessProperty);
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
