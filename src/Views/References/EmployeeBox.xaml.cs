using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models;
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
        private const int MaxVisiblePositionLength = 50;
        private const int OziDepartmentId = 1;
        private const int CommercialDepartmentId = 2;
        private const int FinanceDepartmentId = 3;
        private const double RegularScaleThreshold = 1.125;
        private static readonly EmployeeBoxTextMetrics RegularScaleTextMetrics = new(14, 12, 12, 10, 18);
        private static readonly EmployeeBoxTextMetrics HighScaleTextMetrics = new(12, 10, 10, 8, 16);
        private static readonly Brush EmployeePositionBrush = new SolidColorBrush(Microsoft.UI.Colors.DarkCyan);
        private static readonly Brush EmployeeEditButtonBrush = new SolidColorBrush(Microsoft.UI.Colors.MediumPurple);
        private static readonly Brush EmployeeCopyButtonBrush = new SolidColorBrush(Microsoft.UI.Colors.SeaGreen);
        private static readonly Brush EmployeeContactBrush = (Brush)Application.Current.Resources["ShellAccentBrush"];
        private EmployeeBoxTextMetrics _textMetrics = HighScaleTextMetrics;
        private XamlRoot? _subscribedXamlRoot;

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
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
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

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AttachXamlRootChanged();
            UpdateTextMetrics();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachXamlRootChanged();
        }

        private void AttachXamlRootChanged()
        {
            if (_subscribedXamlRoot == XamlRoot)
            {
                return;
            }

            DetachXamlRootChanged();
            _subscribedXamlRoot = XamlRoot;
            if (_subscribedXamlRoot is not null)
            {
                _subscribedXamlRoot.Changed += OnXamlRootChanged;
            }
        }

        private void DetachXamlRootChanged()
        {
            if (_subscribedXamlRoot is not null)
            {
                _subscribedXamlRoot.Changed -= OnXamlRootChanged;
                _subscribedXamlRoot = null;
            }
        }

        private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            UpdateTextMetrics();
        }

        private void UpdateTextMetrics()
        {
            var textMetrics = ResolveTextMetrics();
            if (textMetrics == _textMetrics)
            {
                return;
            }

            _textMetrics = textMetrics;
            Render();
        }

        private EmployeeBoxTextMetrics ResolveTextMetrics()
        {
            return XamlRoot?.RasterizationScale < RegularScaleThreshold
                ? RegularScaleTextMetrics
                : HighScaleTextMetrics;
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
                    Content = BuildEmployeeRow(employee, itemIndex, _textMetrics),
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Padding = new Thickness(0),
                    MinHeight = 32
                };
                DisableContainerHover(listViewItem);
                if (!string.IsNullOrWhiteSpace(employee.Description))
                {
                    ToolTipService.SetToolTip(listViewItem, employee.Description);
                }

                EmployeesListView.Items.Add(listViewItem);
            }
        }

        private Grid BuildEmployeeRow(EmployeeBoxItem employee, int itemIndex, EmployeeBoxTextMetrics textMetrics)
        {
            var row = new Grid
            {
                ColumnSpacing = 6,
                MinWidth = 300,
                Padding = new Thickness(2, 2, 4, 2),
                Background = itemIndex % 2 == 1
                    ? (Brush)Application.Current.Resources["ShellTableRowPressedBackgroundBrush"]
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star), MinWidth = 120 });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star), MinWidth = 82 });

            var buttons = BuildButtons(employee);
            Grid.SetColumn(buttons, 0);
            row.Children.Add(buttons);

            var info = BuildInfo(employee, textMetrics);
            Grid.SetColumn(info, 1);
            row.Children.Add(info);

            var contacts = BuildContacts(employee, textMetrics);
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
                Orientation = Orientation.Vertical,
                Spacing = 0,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (CanEdit)
            {
                var editButton = BuildIconButton("\uE70F", "Редактировать", EmployeeEditButtonBrush);
                editButton.Click += (_, _) => EditRequested?.Invoke(this, new EmployeeBoxEditRequestedEventArgs(employee));
                panel.Children.Add(editButton);
            }

            var copyButton = BuildIconButton("\uE8C8", "Скопировать в буфер обмена", EmployeeCopyButtonBrush);
            copyButton.Click += (_, _) => CopyEmployee(employee);
            panel.Children.Add(copyButton);

            return panel;
        }

        private static Button BuildIconButton(string glyph, string tooltip, Brush foreground)
        {
            var button = new Button
            {
                Content = glyph,
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 10,
                Foreground = foreground,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0)
            };
            ToolTipService.SetToolTip(button, tooltip);
            return button;
        }

        private static Grid BuildInfo(EmployeeBoxItem employee, EmployeeBoxTextMetrics textMetrics)
        {
            var info = new Grid
            {
                RowSpacing = 1,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var name = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(employee.FullName) ? "Сотрудник" : employee.FullName,
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["ShellPrimaryTextBrush"],
                FontSize = textMetrics.NameFontSize,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };

            var meta = new TextBlock
            {
                Text = BuildMetaText(employee),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = employee.IsActive
                    ? EmployeePositionBrush
                    : new SolidColorBrush(Microsoft.UI.Colors.Firebrick),
                FontSize = textMetrics.MetaFontSize,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = HorizontalAlignment.Right
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
            var visiblePosition = TruncatePosition(position);
            return employee.IsActive ? visiblePosition : $"{visiblePosition} | {employee.StatusText}";
        }

        private static string TruncatePosition(string position)
        {
            return position.Length <= MaxVisiblePositionLength
                ? position
                : position[..MaxVisiblePositionLength];
        }

        private static Grid BuildContacts(EmployeeBoxItem employee, EmployeeBoxTextMetrics textMetrics)
        {
            var contactsPanel = new Grid
            {
                ColumnSpacing = 0,
                RowSpacing = 1,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            contactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

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

                var element = BuildContactLinkElement(contact, match, textMetrics);
                Grid.SetColumn(element, 0);
                Grid.SetRow(element, row);
                contactsPanel.Children.Add(element);

                row++;
            }

            return contactsPanel;
        }

        private static FrameworkElement BuildContactLinkElement(
            string value,
            ContactTypeMatch match,
            EmployeeBoxTextMetrics textMetrics)
        {
            var grid = new Grid
            {
                Height = textMetrics.ContactRowHeight,
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new FontIcon
            {
                Glyph = match.Glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = textMetrics.ContactIconFontSize,
                Width = 13,
                Foreground = EmployeeContactBrush,
                VerticalAlignment = VerticalAlignment.Center
            };

            var linkText = new TextBlock
            {
                Text = value,
                FontSize = textMetrics.ContactFontSize,
                FontWeight = Microsoft.UI.Text.FontWeights.Light,
                Foreground = EmployeeContactBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };

            var link = new HyperlinkButton
            {
                Content = linkText,
                Padding = new Thickness(0),
                MinWidth = 0,
                MinHeight = 0,
                HorizontalAlignment = HorizontalAlignment.Left,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            link.Click += (_, _) => ContactLaunchService.Launch(ContactTypeClassifier.TryCreateLaunchUri(value, match));

            Grid.SetColumn(icon, 0);
            Grid.SetColumn(link, 1);
            grid.Children.Add(icon);
            grid.Children.Add(link);
            return grid;
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
            return CanCurrentUserEditEmployees(userService?.CurrentUser);
        }

        private static bool CanCurrentUserEditEmployees(User? user)
        {
            return user is not null
                && !HasRole(user, "intern")
                && IsProfileDepartment(user);
        }

        private static bool HasRole(User user, string role)
        {
            return user.Role
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(currentRole => string.Equals(currentRole, role, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsProfileDepartment(User user)
        {
            return user.DepartmentId is OziDepartmentId or CommercialDepartmentId or FinanceDepartmentId
                || IsProfileDepartmentName(user.DepartmentName);
        }

        private static bool IsProfileDepartmentName(string departmentName)
        {
            return departmentName.Contains("ОЗИ", StringComparison.OrdinalIgnoreCase)
                || departmentName.Contains("Коммер", StringComparison.OrdinalIgnoreCase)
                || departmentName.Contains("Финанс", StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class EmployeeBoxEditRequestedEventArgs(EmployeeBoxItem employee) : EventArgs
    {
        public EmployeeBoxItem Employee { get; } = employee;
    }

    internal sealed record EmployeeBoxTextMetrics(
        double NameFontSize,
        double MetaFontSize,
        double ContactFontSize,
        double ContactIconFontSize,
        double ContactRowHeight);
}
