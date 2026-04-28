using System.Collections.ObjectModel;
using CbsContractsDesktopClient.Services.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.References
{
    public sealed class DialogContactsEditor : UserControl
    {
        private readonly ObservableCollection<ContactViewItem> _contacts = [];
        private readonly Grid _contactsPanel;
        private readonly TextBox _inputBox;
        private readonly TextBlock _messageBlock;
        private bool _isInternalUpdate;

        public static readonly DependencyProperty ContactsTextProperty =
            DependencyProperty.Register(
                nameof(ContactsText),
                typeof(string),
                typeof(DialogContactsEditor),
                new PropertyMetadata(string.Empty, OnContactsTextChanged));

        public DialogContactsEditor()
        {
            _contactsPanel = new Grid
            {
               RowSpacing = 0,  
               ColumnSpacing = 0,
               MaxHeight = 64,                
               Width = 500,
               MaxWidth = 650,    
                      
            };
            _contactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _contactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _contactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _contactsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _contactsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _contactsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            _inputBox = new TextBox
            {
                
                Width = 250,
                MaxWidth = 300,
                PlaceholderText = "Введите контакт и нажмите Enter",
                HorizontalAlignment = HorizontalAlignment.Left,
                Height = 28,
            };
            _messageBlock = new TextBlock
            {
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Firebrick),
                FontSize = 8,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };

            _inputBox.KeyDown += (_, args) =>
            {
                if (args.Key != Windows.System.VirtualKey.Enter)
                {
                    return;
                }

                args.Handled = true;
                TryAddInputContact();
            };

            var addButton = new Button
            {
                Content = "\uE710",
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTipService.SetToolTip(addButton, "Добавить контакт");
            addButton.Click += (_, _) => TryAddInputContact();

            var inputGrid = new Grid
            {
                ColumnSpacing = 4
            };
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(_inputBox, 0);
            Grid.SetColumn(addButton, 1);
            inputGrid.Children.Add(_inputBox);
            inputGrid.Children.Add(addButton);

            var root = new StackPanel
            {
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            root.Children.Add(_contactsPanel);
            root.Children.Add(inputGrid);
            root.Children.Add(_messageBlock);
            Content = root;

            RefreshFromText(ContactsText);
        }

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

            RenderContacts();
        }

        private void RenderContacts()
        {
            _contactsPanel.Children.Clear();
            if (_contacts.Count == 0)
            {
                return;
            }
            int i = 0;
            int j = 0;
            foreach (var contact in _contacts)
            {
                Microsoft.UI.Xaml.FrameworkElement element = (FrameworkElement)BuildContactElement(
                    contact.Value,
                    contact.Match,
                    showRemoveButton: true,
                    (_, _) =>
                    {
                        _contacts.Remove(contact);
                        PushContactsText();
                        RenderContacts();
                    });
                Grid.SetColumn(element, i);
                Grid.SetRow(element, j);
                _contactsPanel.Children.Add(element);

                i++;
                if (i == 3)
                {
                    i = 0;
                    j++;
                }

                if (j > 2)
                {
                    return;
                }
            }
        }

        private UIElement BuildContactElement(ContactViewItem contact)
        {
            var grid = new Grid
            {                
                Height = 18,
                MinWidth = 50,
                Padding = new Thickness(4, 0, 2, 0),
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new FontIcon
            {
                Glyph = contact.Glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 10,
                Width = 16,
                Foreground = (Brush)Application.Current.Resources["ShellAccentBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };

            var link = new HyperlinkButton
            {
                Content = contact.Value,
                NavigateUri = contact.NavigateUri,
                Padding = new Thickness(2, 0, 2, 0),
                MinWidth = 0,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

            var removeButton = new Button
            {
                Content = "\uE711",
                Width = 16,
                Height = 16,
                Padding = new Thickness(0),
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 10,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0)
            };
            ToolTipService.SetToolTip(removeButton, "Удалить контакт");
            removeButton.Click += (_, _) =>
            {
                _contacts.Remove(contact);
                PushContactsText();
                RenderContacts();
            };

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(link, 1);
            Grid.SetColumn(removeButton, 2);
            grid.Children.Add(icon);
            grid.Children.Add(link);
            grid.Children.Add(removeButton);
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
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 10,
                Width = 16,
                Foreground = (Brush)Application.Current.Resources["ShellAccentBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };

            var link = new HyperlinkButton
            {
                Content = value,
                NavigateUri = ContactTypeClassifier.TryCreateLaunchUri(value, match),
                Padding = new Thickness(2, 0, 2, 0),
                MinWidth = 0,
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

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
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
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
            RenderContacts();
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

        private sealed class ContactViewItem(string value, ContactTypeMatch match)
        {
            public string Value { get; } = value;

            public ContactTypeMatch Match { get; } = match;

            public string Glyph { get; } = match.Glyph;

            public Uri? NavigateUri { get; } = ContactTypeClassifier.TryCreateLaunchUri(value, match);
        }
    }
}
