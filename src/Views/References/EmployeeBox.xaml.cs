using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace CbsContractsDesktopClient.Views.References
{
    public sealed partial class EmployeeBox : UserControl
    {
        public static readonly DependencyProperty EmployeesProperty =
            DependencyProperty.Register(
                nameof(Employees),
                typeof(IReadOnlyList<EmployeeBoxItem>),
                typeof(EmployeeBox),
                new PropertyMetadata(Array.Empty<EmployeeBoxItem>(), OnEmployeesChanged));

        public static readonly DependencyProperty CanEditProperty =
            DependencyProperty.Register(
                nameof(CanEdit),
                typeof(bool),
                typeof(EmployeeBox),
                new PropertyMetadata(false, OnCanEditChanged));

        public event EventHandler<EmployeeBoxEditRequestedEventArgs>? EditRequested;

        public EmployeeBox()
        {
            InitializeComponent();
            CanEdit = ResolveDefaultCanEdit();
            Render();
        }

        public IReadOnlyList<EmployeeBoxItem> Employees
        {
            get => (IReadOnlyList<EmployeeBoxItem>)GetValue(EmployeesProperty);
            set => SetValue(EmployeesProperty, value);
        }

        public bool CanEdit
        {
            get => (bool)GetValue(CanEditProperty);
            set => SetValue(CanEditProperty, value);
        }

        private static void OnEmployeesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((EmployeeBox)d).Render();
        }

        private static void OnCanEditChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((EmployeeBox)d).Render();
        }

        private void Render()
        {
            EmployeesListView.Items.Clear();

            var employees = Employees ?? [];
            EmptyTextBlock.Visibility = employees.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmployeesListView.Visibility = employees.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            foreach (var employee in employees)
            {
                var itemIndex = EmployeesListView.Items.Count;
                var listViewItem = new ListViewItem
                {
                    Content = BuildEmployeeRow(employee, itemIndex),
                    Padding = new Thickness(2),
                    MinHeight = 42
                };
                DisableContainerHover(listViewItem);
                if (!string.IsNullOrWhiteSpace(employee.Description))
                {
                    ToolTipService.SetToolTip(listViewItem, employee.Description);
                }

                EmployeesListView.Items.Add(listViewItem);
            }
        }

        private Grid BuildEmployeeRow(EmployeeBoxItem employee, int itemIndex)
        {
            var row = new Grid
            {
                ColumnSpacing = 6,
                Padding = new Thickness(2, 4, 2, 4),
                Background = itemIndex % 2 == 1
                    ? (Brush)Application.Current.Resources["ShellAccentPanelBackgroundAltBrush"]
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var buttons = BuildButtons(employee);
            Grid.SetColumn(buttons, 0);
            row.Children.Add(buttons);

            var info = BuildInfo(employee);
            Grid.SetColumn(info, 1);
            row.Children.Add(info);

            var contacts = BuildContacts(employee);
            Grid.SetColumn(contacts, 2);
            row.Children.Add(contacts);

            return row;
        }

        private static void DisableContainerHover(ListViewItem item)
        {
            var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            item.Resources["ListViewItemBackgroundPointerOver"] = transparent;
            item.Resources["ListViewItemBackgroundPointerOverSelected"] = transparent;
            item.Resources["ListViewItemBackgroundPressed"] = transparent;
            item.Resources["ListViewItemBackgroundPressedSelected"] = transparent;
        }

        private StackPanel BuildButtons(EmployeeBoxItem employee)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (CanEdit)
            {
                var editButton = BuildIconButton("\uE70F", "Редактировать");
                editButton.Click += (_, _) => EditRequested?.Invoke(this, new EmployeeBoxEditRequestedEventArgs(employee));
                panel.Children.Add(editButton);
            }

            var copyButton = BuildIconButton("\uE8C8", "Скопировать в буфер обмена");
            copyButton.Click += (_, _) => CopyEmployee(employee);
            panel.Children.Add(copyButton);

            return panel;
        }

        private static Button BuildIconButton(string glyph, string tooltip)
        {
            var button = new Button
            {
                Content = glyph,
                Width = 24,
                Height = 24,
                Padding = new Thickness(0),
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 11,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0)
            };
            ToolTipService.SetToolTip(button, tooltip);
            return button;
        }

        private static Grid BuildInfo(EmployeeBoxItem employee)
        {
            var info = new Grid
            {
                RowSpacing = 1,
                VerticalAlignment = VerticalAlignment.Center
            };
            info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var name = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(employee.FullName) ? "Сотрудник" : employee.FullName,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["ShellPrimaryTextBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };

            var meta = new TextBlock
            {
                Text = BuildMetaText(employee),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = employee.IsActive
                    ? (Brush)Application.Current.Resources["ShellSecondaryTextBrush"]
                    : new SolidColorBrush(Microsoft.UI.Colors.Firebrick),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };

            Grid.SetRow(name, 0);
            Grid.SetRow(meta, 1);
            info.Children.Add(name);
            info.Children.Add(meta);
            return info;
        }

        private static string BuildMetaText(EmployeeBoxItem employee)
        {
            var position = string.IsNullOrWhiteSpace(employee.Position) ? "должность не указана" : employee.Position;
            return employee.IsActive ? position : $"{position} | {employee.StatusText}";
        }

        private static Grid BuildContacts(EmployeeBoxItem employee)
        {
            var contactsPanel = new Grid
            {
                ColumnSpacing = 0,
                RowSpacing = 0,
                VerticalAlignment = VerticalAlignment.Center
            };
            contactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contactsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contactsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var column = 0;
            var row = 0;
            foreach (var contact in employee.Contacts)
            {
                if (!ContactTypeClassifier.TryClassify(contact, out var match))
                {
                    continue;
                }

                while (contactsPanel.RowDefinitions.Count <= row)
                {
                    contactsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                var element = (FrameworkElement)DialogContactsEditor.BuildContactElement(contact, match, showRemoveButton: false);
                Grid.SetColumn(element, column);
                Grid.SetRow(element, row);
                contactsPanel.Children.Add(element);

                column++;
                if (column == 2)
                {
                    column = 0;
                    row++;
                }
            }

            return contactsPanel;
        }

        private static void CopyEmployee(EmployeeBoxItem employee)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(employee.CopyText);
            Clipboard.SetContent(dataPackage);
        }

        private static bool ResolveDefaultCanEdit()
        {
            var userService = App.Services.GetService<IUserService>();
            var role = userService?.CurrentUser?.Role ?? string.Empty;
            return !role
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(static role => string.Equals(role, "intern", StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class EmployeeBoxEditRequestedEventArgs(EmployeeBoxItem employee) : EventArgs
    {
        public EmployeeBoxItem Employee { get; } = employee;
    }
}
