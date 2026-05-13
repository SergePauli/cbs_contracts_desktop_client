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
    private readonly TextBox _textBox;
    private readonly Button _clearButton;
    private readonly Button _calendarButton;
    private readonly CalendarView _calendarView;
    private bool _isSyncing;

    public CalendarInput()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _textBox = new TextBox
        {
            PlaceholderText = "дд.мм.гггг",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(11, 5, 54, 5),
            AcceptsReturn = true,
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

        _calendarView = new CalendarView
        {
            SelectionMode = CalendarViewSelectionMode.Single,
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

        _calendarButton = BuildIconButton("\uE787", 10);
        FlyoutBase.SetAttachedFlyout(_calendarButton, calendarFlyout);
        _calendarButton.Click += (_, _) =>
        {
            SyncCalendarViewSelection();
            FlyoutBase.ShowAttachedFlyout(_calendarButton);
        };

        _clearButton = BuildIconButton("\uE711", 10);
        _clearButton.Click += (_, _) => Date = null;

        SuppressChrome(_clearButton);
        SuppressChrome(_calendarButton);

        var buttonsHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 2, 0),
            Spacing = 0
        };
        buttonsHost.Children.Add(_clearButton);
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

    public event EventHandler<CalendarInputDateChangedEventArgs>? DateChanged;

    private static void OnDatePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var editor = (CalendarInput)dependencyObject;
        var oldDate = NormalizeDate(args.OldValue as DateTimeOffset?);
        var newDate = NormalizeDate(args.NewValue as DateTimeOffset?);

        editor.SyncEditorState();
        editor.DateChanged?.Invoke(editor, new CalendarInputDateChangedEventArgs(oldDate, newDate));
    }

    private void OnTextBoxPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Enter)
        {
            return;
        }

        args.Handled = true;
        CommitText();
    }

    private void CommitText()
    {
        if (_isSyncing)
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
        }
        finally
        {
            _isSyncing = false;
        }
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

    private static Button BuildIconButton(string glyph, double fontSize = 14)
    {
        return new Button
        {
            Width = 16,
            Height = 28,
            MinHeight = 28,
            Padding = new Thickness(2, 0, 2, 0),
            Margin = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Content = new FontIcon
            {
                Glyph = glyph,
                FontSize = fontSize
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
