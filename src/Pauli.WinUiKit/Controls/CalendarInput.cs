using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Pauli.WinUiKit.Controls;

public sealed class CalendarInput : Grid
{
    private const double CompactButtonSize = 16;
    private const double CompactIconSize = 12;

    private readonly TextBox _textBox;
    private readonly Button _clearButton;
    private readonly Button _calendarButton;
    private readonly CalendarView _calendarView;
    private bool _isSyncing;

    public CalendarInput()
    {
        Width = 106;

        _textBox = new TextBox
        {
            PlaceholderText = "ДД.ММ.ГГГГ",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            InputScope = new InputScope
            {
                Names =
                {
                    new InputScopeName(InputScopeNameValue.Number)
                }
            }
        };
        _textBox.LostFocus += (_, _) => CommitText();
        _textBox.PreviewKeyDown += OnTextBoxPreviewKeyDown;
        DisableTextBoxClearButton(_textBox);
        _textBox.Loaded += (_, _) =>
        {
            var padding = _textBox.Padding;
            _textBox.Padding = new Thickness(4, padding.Top, padding.Right, padding.Bottom);
        };

        _calendarView = new CalendarView
        {
            SelectionMode = CalendarViewSelectionMode.Single,
            FirstDayOfWeek = Windows.Globalization.DayOfWeek.Monday,
            MinWidth = 280,
            MinHeight = 300
        };

        var calendarFlyout = new Flyout
        {
            Content = _calendarView
        };
        _calendarView.SelectedDatesChanged += (_, _) =>
        {
            if (_isSyncing)
            {
                return;
            }

            var selectedDate = _calendarView.SelectedDates.FirstOrDefault();
            if (selectedDate == default)
            {
                return;
            }

            Date = selectedDate;
            calendarFlyout.Hide();
        };

        _calendarButton = BuildIconButton("\uE787");
        FlyoutBase.SetAttachedFlyout(_calendarButton, calendarFlyout);
        _calendarButton.Click += (_, _) =>
        {
            SyncCalendarViewSelection();
            FlyoutBase.ShowAttachedFlyout(_calendarButton);
        };

        _clearButton = BuildIconButton("\uE711");
        _clearButton.Click += (_, _) => Date = null;

        SuppressChrome(_clearButton);
        SuppressChrome(_calendarButton);

        var buttonsHost = new Grid
        {
            Width = CompactButtonSize * 2,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 0, 3, 0)
        };
        buttonsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CompactButtonSize) });
        buttonsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(CompactButtonSize) });
        buttonsHost.Children.Add(_clearButton);
        Grid.SetColumn(_calendarButton, 1);
        buttonsHost.Children.Add(_calendarButton);

        Children.Add(_textBox);
        Children.Add(buttonsHost);

        SyncEditorState();
    }

    public static readonly DependencyProperty DateProperty = DependencyProperty.Register(
        nameof(Date),
        typeof(DateTimeOffset?),
        typeof(CalendarInput),
        new PropertyMetadata(null, OnDatePropertyChanged));

    public DateTimeOffset? Date
    {
        get => (DateTimeOffset?)GetValue(DateProperty);
        set => SetValue(DateProperty, NormalizeDate(value));
    }

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly),
        typeof(bool),
        typeof(CalendarInput),
        new PropertyMetadata(false, OnIsReadOnlyPropertyChanged));

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public event EventHandler<CalendarInputDateChangedEventArgs>? DateChanged;

    public Action<CalendarInput, KeyRoutedEventArgs>? OnTab { get; set; }

    public bool FocusInput(FocusState focusState = FocusState.Programmatic)
    {
        return _textBox.Focus(focusState);
    }

    private static void DisableTextBoxClearButton(TextBox textBox)
    {
        var transparent = new SolidColorBrush(Colors.Transparent);
        textBox.Resources["TextControlButtonForeground"] = transparent;
        textBox.Resources["TextControlButtonForegroundPointerOver"] = transparent;
        textBox.Resources["TextControlButtonForegroundPressed"] = transparent;
        textBox.Resources["TextControlButtonBackground"] = transparent;
        textBox.Resources["TextControlButtonBackgroundPointerOver"] = transparent;
        textBox.Resources["TextControlButtonBackgroundPressed"] = transparent;
        textBox.Resources["TextBoxInnerButtonMargin"] = new Thickness(0, 4, 24, 4);
    }

    private static void OnDatePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var editor = (CalendarInput)dependencyObject;
        var oldDate = NormalizeDate(args.OldValue as DateTimeOffset?);
        var newDate = NormalizeDate(args.NewValue as DateTimeOffset?);

        editor.SyncEditorState();
        editor.DateChanged?.Invoke(editor, new CalendarInputDateChangedEventArgs(oldDate, newDate));
    }

    private static void OnIsReadOnlyPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((CalendarInput)dependencyObject).SyncReadOnlyState();
    }

    private void OnTextBoxPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Tab && OnTab is not null)
        {
            CommitText();
            OnTab(this, args);
            return;
        }

        if (args.Key != VirtualKey.Enter)
        {
            return;
        }

        args.Handled = true;
        CommitText();
    }

    private void CommitText()
    {
        if (_isSyncing || IsReadOnly)
        {
            return;
        }

        var text = _textBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            Date = null;
            return;
        }

        if (TryParseEditorDate(text, out var date))
        {
            Date = date;
            return;
        }

        SyncEditorState();
    }

    private void SyncEditorState()
    {
        _isSyncing = true;
        try
        {
            _textBox.Text = Date?.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture) ?? string.Empty;
            SyncCalendarViewSelection();
            _clearButton.Visibility = Date is null ? Visibility.Collapsed : Visibility.Visible;
            SyncReadOnlyState();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void SyncReadOnlyState()
    {
        _textBox.IsReadOnly = IsReadOnly;
        _clearButton.IsEnabled = !IsReadOnly;
        _calendarButton.IsEnabled = !IsReadOnly;
        Opacity = IsReadOnly ? 0.72 : 1;
    }

    private void SyncCalendarViewSelection()
    {
        _calendarView.SelectedDates.Clear();
        if (Date is not null)
        {
            _calendarView.SelectedDates.Add(Date.Value);
        }

        _calendarView.SetDisplayDate(Date ?? DateTimeOffset.Now);
    }

    private static DateTimeOffset? NormalizeDate(DateTimeOffset? value)
    {
        return value is null
            ? null
            : new DateTimeOffset(value.Value.Date);
    }

    private static bool TryParseEditorDate(string text, out DateTimeOffset? date)
    {
        var formats = new[]
        {
            "d.M.yyyy",
            "dd.MM.yyyy",
            "d.M.yy",
            "dd.MM.yy",
            "yyyy-MM-dd"
        };

        if (DateTime.TryParseExact(
            text,
            formats,
            CultureInfo.CurrentCulture,
            DateTimeStyles.None,
            out var exactDate))
        {
            date = new DateTimeOffset(exactDate.Date);
            return true;
        }

        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsedDate))
        {
            date = new DateTimeOffset(parsedDate.Date);
            return true;
        }

        date = null;
        return false;
    }

    private static Button BuildIconButton(string glyph)
    {
        return new Button
        {
            Width = CompactButtonSize,
            Height = CompactButtonSize,
            MinHeight = 0,
            IsTabStop = false,
            Padding = new Thickness(2, 0, 2, 0),
            Margin = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Content = new FontIcon
            {
                Glyph = glyph,
                Width = CompactIconSize,
                Height = CompactIconSize,
                FontSize = CompactIconSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private static void SuppressChrome(Button button)
    {
        var transparent = new SolidColorBrush(Colors.Transparent);
        button.Resources["ButtonBackgroundPointerOver"] = transparent;
        button.Resources["ButtonBorderBrushPointerOver"] = transparent;
        button.Resources["ButtonBackgroundPressed"] = transparent;
        button.Resources["ButtonBorderBrushPressed"] = transparent;
    }
}
