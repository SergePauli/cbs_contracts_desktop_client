using System.Collections.ObjectModel;
using CbsContractsDesktopClient.Services.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.References
{
    public sealed partial class DialogContactsEditor : UserControl
    {
        private readonly ObservableCollection<ContactViewItem> _contacts = [];
        private bool _isInternalUpdate;

        public static readonly DependencyProperty ContactsTextProperty =
            DependencyProperty.Register(
                nameof(ContactsText),
                typeof(string),
                typeof(DialogContactsEditor),
                new PropertyMetadata(string.Empty, OnContactsTextChanged));

        public DialogContactsEditor()
        {
            InitializeComponent();
            RefreshFromText(ContactsText);
        }

        public ObservableCollection<ContactViewItem> Contacts => _contacts;

        private void Input_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs args)
        {
            if (args.Key != Windows.System.VirtualKey.Enter) return;
            args.Handled = true;
            TryAddInputContact();
        }

        private void Add_Click(object sender, RoutedEventArgs args) => TryAddInputContact();

        private void Remove_Click(object sender, RoutedEventArgs args)
        {
            _contacts.Remove((ContactViewItem)((FrameworkElement)sender).DataContext);
            PushContactsText();
        }

        private void Contact_Click(object sender, RoutedEventArgs args)
            => ContactLaunchService.Launch(((ContactViewItem)((FrameworkElement)sender).DataContext).NavigateUri);
        public string ContactsText
        {
            get => (string)GetValue(ContactsTextProperty);
            set => SetValue(ContactsTextProperty, value);
        }

        private static void OnContactsTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (DialogContactsEditor)d;
            if (editor._isInternalUpdate)
            {
                return;
            }

            editor.RefreshFromText(e.NewValue as string ?? string.Empty);
        }

        private void RefreshFromText(string value)
        {
            _contacts.Clear();
            foreach (var item in ParseContactValues(value))
            {
                if (ContactTypeClassifier.TryClassify(item, out var match))
                {
                    _contacts.Add(new ContactViewItem(item, match));
                }
            }


        }

        public static UIElement BuildContactElement(
            string value,
            ContactTypeMatch match,
            bool showRemoveButton,
            RoutedEventHandler? removeClick = null)
        {
            var grid = new Grid
            {
                Height = 18,
                MinWidth = 50,
                Padding = new Thickness(4, 0, 2, 0),
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var icon = new FontIcon
            {
                Glyph = match.Glyph,
                FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                FontSize = 10,
                Width = 16,
                Foreground = (Brush)Application.Current.Resources["ShellAccentBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };

            var hyperlink = new Microsoft.UI.Xaml.Documents.Hyperlink();
            hyperlink.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = value });
            hyperlink.Click += (_, _) => ContactLaunchService.Launch(ContactTypeClassifier.TryCreateLaunchUri(value, match));
            var link = new TextBlock
            {
                IsTextSelectionEnabled = true,
                Padding = new Thickness(2, 0, 2, 0),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            link.Inlines.Add(hyperlink);

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(link, 1);
            grid.Children.Add(icon);
            grid.Children.Add(link);

            if (showRemoveButton)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var removeButton = new Button
                {
                    Content = "\uE711",
                    Width = 16,
                    Height = 16,
                    Padding = new Thickness(0),
                    FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                    FontSize = 10,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0)
                };
                ToolTipService.SetToolTip(removeButton, "Удалить контакт");
                if (removeClick is not null)
                {
                    removeButton.Click += removeClick;
                }

                Grid.SetColumn(removeButton, 2);
                grid.Children.Add(removeButton);
            }

            return new Border
            {
                Margin = new Thickness(0, 0, 4, 2),
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Background = (Brush)Application.Current.Resources["ShellAccentPanelBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ShellPanelBorderBrush"],
                BorderThickness = new Thickness(1),
                Child = grid
            };
        }

        private void TryAddInputContact()
        {
            var value = _inputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!ContactTypeClassifier.TryClassify(value, out var match))
            {
                ShowMessage("Тип контакта не определен. Поддерживаются Email, Fax, Phone, SiteUrl, Telegram.");
                return;
            }

            if (_contacts.Any(contact => string.Equals(contact.Value, value, StringComparison.CurrentCultureIgnoreCase)))
            {
                ShowMessage("Такой контакт уже добавлен.");
                return;
            }

            _contacts.Add(new ContactViewItem(value, match));
            _inputBox.Text = string.Empty;
            HideMessage();
            PushContactsText();

        }

        private void PushContactsText()
        {
            _isInternalUpdate = true;
            try
            {
                ContactsText = string.Join(Environment.NewLine, _contacts.Select(static contact => contact.Value));
                var binding = GetBindingExpression(ContactsTextProperty);
                binding?.UpdateSource();
            }
            finally
            {
                _isInternalUpdate = false;
            }
        }

        private void ShowMessage(string message)
        {
            _messageBlock.Text = message;
            _messageBlock.Visibility = Visibility.Visible;
        }

        private void HideMessage()
        {
            _messageBlock.Text = string.Empty;
            _messageBlock.Visibility = Visibility.Collapsed;
        }

        public static IReadOnlyList<string> ParseContactValues(string value)
        {
            return value
                .Split([Environment.NewLine, "\n", ";", ","], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public sealed class ContactViewItem(string value, ContactTypeMatch match)
        {
            public string Value { get; } = value;

            public ContactTypeMatch Match { get; } = match;

            public string Glyph { get; } = match.Glyph;

            public Uri? NavigateUri { get; } = ContactTypeClassifier.TryCreateLaunchUri(value, match);
        }
    }
}
