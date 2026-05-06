using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using KeyboardAcceleratorPlacementModeEnum = Microsoft.UI.Xaml.Input.KeyboardAcceleratorPlacementMode;

namespace CbsContractsDesktopClient.Views.References
{
    public sealed class ContragentEditDialog : ContentDialog
    {
        private ListView? _registrationsListView;
        private bool _isRegistrationSyncing;

        public ContragentEditDialog(ContragentEditViewModel viewModel)
        {
           
            
            FullSizeDesired = false;
            HorizontalAlignment = HorizontalAlignment.Center;
            ViewModel = viewModel;
            DataContext = viewModel;
            Title = BuildHeader();
            PrimaryButtonText = viewModel.PrimaryButtonText;
            CloseButtonText = "Закрыть";
            DefaultButton = ContentDialogButton.Close;
            SetBinding(IsPrimaryButtonEnabledProperty, new Binding
            {
                Path = new PropertyPath(nameof(ContragentEditViewModel.CanSubmit))
            });
            Resources["ContentDialogMinWidth"] = 650d;
            Resources["ContentDialogMinHeight"] = 600d;
            Resources["ContentDialogMaxWidth"] = 920d;
            Resources["ContentDialogPadding"] = new Thickness(8);
            Resources["ContentDialogTitleMargin"] = new Thickness(0);
            Resources["ContentDialogCommandSpaceMargin"] = new Thickness(4);
            Content = BuildContent();
            Loaded += OnLoaded;
        }

        public ContragentEditViewModel ViewModel { get; }

        private Grid BuildHeader()
        {
            var header = new Grid
            {
                Width = 900, 
                ColumnSpacing = 8,
                Padding = new Thickness(4, 4, 4, 0)
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = ViewModel.DialogTitle,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            Grid.SetColumn(title, 0);
            header.Children.Add(title);

            var closeButton = new Button
            {
                Content = "\uE711",
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 12,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementModeEnum.Hidden,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTipService.SetToolTip(closeButton, "Закрыть");
            closeButton.Click += (_, _) => Hide();
            Grid.SetColumn(closeButton, 1);
            header.Children.Add(closeButton);
            return header;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SuppressKeyboardAcceleratorTooltips(this);
        }

        private static void SuppressKeyboardAcceleratorTooltips(DependencyObject root)
        {
            if (root is UIElement element)
            {
                element.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementModeEnum.Hidden;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < childCount; index++)
            {
                SuppressKeyboardAcceleratorTooltips(VisualTreeHelper.GetChild(root, index));
            }
        }

        private UIElement BuildContent()
        {
            var root = new Grid
            {
                Height = 630,
                Width = 900,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var stack = new StackPanel
            {
                Spacing = 12
            };
            stack.Children.Add(BuildValidationInfoBar());
            stack.Children.Add(BuildTabs());

            root.Children.Add(new ScrollViewer
            {
               
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stack
            });

            return root;
        }

        private InfoBar BuildValidationInfoBar()
        {
            var infoBar = new InfoBar
            {
                Severity = InfoBarSeverity.Error,
                IsClosable = true,
                IsOpen = false,
                Margin = new Thickness(0, 0, 0, 4)
            };
            infoBar.SetBinding(InfoBar.MessageProperty, new Binding
            {
                Path = new PropertyPath(nameof(ContragentEditViewModel.ErrorInfoMessage))
            });
            infoBar.SetBinding(InfoBar.IsOpenProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                Path = new PropertyPath(nameof(ContragentEditViewModel.IsErrorInfoVisible))
            });
            return infoBar;
        }

        private TabView BuildTabs()
        {
            var tabView = new TabView
            {
                IsAddTabButtonVisible = false,
                TabWidthMode = TabViewWidthMode.Equal
            };
            tabView.TabItems.Add(new TabViewItem
            {
                Header = "Основное",
                Content = BuildMainGrid()
            });
            tabView.TabItems.Add(new TabViewItem
            {
                Header = "Коды и счета",
                Content = BuildCodesGrid()
            });
            tabView.TabItems.Add(new TabViewItem
            {
                Header = "Регистрации",
                Content = BuildRegistrationsView()
            });
            return tabView;
        }

        private Grid BuildMainGrid()
        {
            var grid = BuildFieldsGrid();
            if (!ViewModel.State.IsCreateMode)
            {
                AddRow(grid, "ID", BuildReadOnlyTextBox(nameof(ContragentEditViewModel.Id)));
            }

            AddRow(grid, "ИНН", BuildTextBoxEditor(nameof(ContragentEditViewModel.Inn), minWidth: 240), isRequired: true);
            AddRow(grid, "КПП", BuildTextBoxEditor(nameof(ContragentEditViewModel.Kpp), minWidth: 240));
            AddRow(grid, "КодПодр.", BuildTextBoxEditor(nameof(ContragentEditViewModel.Division), minWidth: 160));
            AddRow(grid, "Форма", BuildOwnershipEditor(), isRequired: true);
            AddRow(grid, "Наименование", BuildTextBoxEditor(nameof(ContragentEditViewModel.Name)), isRequired: true);
            AddRow(grid, "Регион", BuildRegionEditor());
            AddRow(grid, "Адрес фактический", BuildAddressEditor());
            AddRow(grid, "Полное наименование", BuildTextBoxEditor(nameof(ContragentEditViewModel.FullName)));
            AddRow(grid, "Контакты", BuildContactsEditor());
            AddRow(grid, "Описание", BuildMultilineTextBox(nameof(ContragentEditViewModel.Description), minHeight: 74));
            return grid;
        }

        private Grid BuildCodesGrid()
        {
            var grid = BuildFieldsGrid();
            AddRow(grid, "ОГРН", BuildTextBoxEditor(nameof(ContragentEditViewModel.Ogrn), minWidth: 240));
            AddRow(grid, "ОКФС", BuildTextBoxEditor(nameof(ContragentEditViewModel.Okfc), minWidth: 160));
            AddRow(grid, "ОКОПФ", BuildTextBoxEditor(nameof(ContragentEditViewModel.Okopf), minWidth: 160));
            AddRow(grid, "ОКПО", BuildTextBoxEditor(nameof(ContragentEditViewModel.Okpo), minWidth: 180));
            AddRow(grid, "ОКОГУ", BuildTextBoxEditor(nameof(ContragentEditViewModel.Okogu), minWidth: 180));
            AddRow(grid, "ОКВЭД", BuildTextBoxEditor(nameof(ContragentEditViewModel.Okved), minWidth: 180));
            AddRow(grid, "ОКТМО", BuildTextBoxEditor(nameof(ContragentEditViewModel.Oktmo), minWidth: 180));
            AddRow(grid, "Наименование банка", BuildTextBoxEditor(nameof(ContragentEditViewModel.BankName)));
            AddRow(grid, "БИК банка", BuildTextBoxEditor(nameof(ContragentEditViewModel.BankBik), minWidth: 180));
            AddRow(grid, "Номер счета", BuildTextBoxEditor(nameof(ContragentEditViewModel.BankAccount)));
            AddRow(grid, "Номер кор.счета", BuildTextBoxEditor(nameof(ContragentEditViewModel.BankCorAccount)));
            return grid;
        }

        private FrameworkElement BuildRegistrationsView()
        {
            var root = new Grid
            {
                Padding = new Thickness(0, 12, 0, 0),
                MinHeight = 420
            };

            if (ViewModel.OrganizationHistory.Count == 0)
            {
                root.Children.Add(new TextBlock
                {
                    Text = "История регистраций пока не загружена",
                    Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                return root;
            }

            var listView = new ListView
            {
                SelectionMode = ListViewSelectionMode.None,
                IsItemClickEnabled = false
            };
            _registrationsListView = listView;
            RenderRegistrationTimeline();

            root.Children.Add(listView);
            return root;
        }

        private void RenderRegistrationTimeline()
        {
            if (_registrationsListView is null)
            {
                return;
            }

            _isRegistrationSyncing = true;
            try
            {
                _registrationsListView.Items.Clear();
                var organizations = ViewModel.VisibleOrganizationHistory;
                for (var index = 0; index < organizations.Count; index++)
                {
                    _registrationsListView.Items.Add(BuildRegistrationHistoryItem(organizations[index], index, organizations.Count));
                }
            }
            finally
            {
                _isRegistrationSyncing = false;
            }
        }

        private UIElement BuildRegistrationHistoryItem(
            ContragentOrganizationHistoryItem organization,
            int index,
            int count)
        {
            var root = new Grid
            {
                ColumnSpacing = 10,
                Margin = new Thickness(0, 0, 0, index == count - 1 ? 0 : 8)
            };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var markerHost = new Grid();
            markerHost.Children.Add(new Rectangle
            {
                Width = 2,
                Fill = (Brush)Application.Current.Resources["ShellPanelBorderBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch
            });
            markerHost.Children.Add(new Ellipse
            {
                Width = 10,
                Height = 10,
                Margin = new Thickness(0, 18, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Fill = organization.IsActive
                    ? (Brush)Application.Current.Resources["ShellAccentBrush"]
                    : (Brush)Application.Current.Resources["ShellAccentMutedBrush"]
            });
            Grid.SetColumn(markerHost, 0);
            root.Children.Add(markerHost);

            var border = new Border
            {
                Padding = new Thickness(10, 8, 10, 8),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = (Brush)Application.Current.Resources["ShellPanelBorderBrush"],
                Background = organization.IsActive
                    ? (Brush)Application.Current.Resources["ShellAccentPanelBackgroundBrush"]
                    : (Brush)Application.Current.Resources["ShellPanelBackgroundBrush"]
            };

            var grid = new Grid
            {
                ColumnSpacing = 10
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel
            {
                Spacing = 3
            };
            var header = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8
            };
            var usedCheckBox = new CheckBox
            {
                Tag = organization,
                IsChecked = organization.IsActive,
                IsEnabled = !ViewModel.State.IsCreateMode,
                MinWidth = 24,
                VerticalAlignment = VerticalAlignment.Center
            };
            usedCheckBox.Checked += RegistrationUsedCheckBox_Checked;
            usedCheckBox.Unchecked += RegistrationUsedCheckBox_Unchecked;
            header.Children.Add(usedCheckBox);
            header.Children.Add(new TextBlock
            {
                Text = organization.PrimaryText,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });
            textStack.Children.Add(header);
            textStack.Children.Add(new TextBlock
            {
                Text = organization.RequisitesText,
                Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"],
                TextWrapping = TextWrapping.Wrap
            });

            if (!string.IsNullOrWhiteSpace(organization.PeriodText))
            {
                textStack.Children.Add(new TextBlock
                {
                    Text = organization.PeriodText,
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"]
                });
            }

            Grid.SetColumn(textStack, 0);
            grid.Children.Add(textStack);

            var status = new TextBlock
            {
                Text = organization.StatusText,
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = organization.IsActive
                    ? new SolidColorBrush(Microsoft.UI.Colors.SeaGreen)
                    : (Brush)Application.Current.Resources["ShellSecondaryTextBrush"],
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(status, 1);

            if (!organization.IsActive && organization.Id is not null)
            {
                var actions = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    VerticalAlignment = VerticalAlignment.Top
                };
                actions.Children.Add(status);
                var deleteButton = new Button
                {
                    Content = "\uE74D",
                    Tag = organization,
                    Width = 28,
                    Height = 28,
                    Padding = new Thickness(0),
                    FontFamily = new FontFamily("Segoe Fluent Icons"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Firebrick),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(0),
                    KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementModeEnum.Hidden
                };
                ToolTipService.SetToolTip(deleteButton, "Удалить регистрацию");
                deleteButton.Click += DeleteRegistrationButton_Click;
                actions.Children.Add(deleteButton);
                Grid.SetColumn(actions, 1);
                grid.Children.Add(actions);
            }
            else
            {
                grid.Children.Add(status);
            }

            border.Child = grid;
            Grid.SetColumn(border, 1);
            root.Children.Add(border);
            return root;
        }

        private void RegistrationUsedCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isRegistrationSyncing || sender is not CheckBox { Tag: ContragentOrganizationHistoryItem registration })
            {
                return;
            }

            if (ViewModel.ActivateRegistration(registration))
            {
                RenderRegistrationTimeline();
            }
        }

        private void RegistrationUsedCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isRegistrationSyncing || sender is not CheckBox checkBox)
            {
                return;
            }

            checkBox.IsChecked = true;
        }

        private void DeleteRegistrationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: ContragentOrganizationHistoryItem registration })
            {
                return;
            }

            if (ViewModel.MarkRegistrationForDestroy(registration))
            {
                RenderRegistrationTimeline();
            }
        }

        private static Grid BuildFieldsGrid()
        {
            var grid = new Grid
            {
                Padding = new Thickness(0,12,0,0),
                ColumnSpacing = 8,
                RowSpacing = 8
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return grid;
        }

        private static void AddRow(Grid grid, string labelText, FrameworkElement editor, bool isRequired = false)
        {
            var rowIndex = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = BuildLabel(labelText, isRequired);
            Grid.SetRow(label, rowIndex);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            editor.HorizontalAlignment = HorizontalAlignment.Left;
            editor.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetRow(editor, rowIndex);
            Grid.SetColumn(editor, 1);
            grid.Children.Add(editor);
        }

        private static FrameworkElement BuildLabel(string labelText, bool isRequired)
        {
            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Orientation = Orientation.Horizontal,
                Spacing = 2
            };
            panel.Children.Add(new TextBlock
            {
                Text = labelText,
                TextAlignment = TextAlignment.Right,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"]
            });

            if (isRequired)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "*",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Microsoft.UI.Colors.Firebrick)
                });
            }

            return panel;
        }

        private static TextBox BuildReadOnlyTextBox(string bindingPath)
        {
            var textBox = new TextBox
            {
                IsReadOnly = true,
                IsTabStop = false,
                Width = 180,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            textBox.SetBinding(TextBox.TextProperty, new Binding
            {
                Path = new PropertyPath(bindingPath)
            });
            return textBox;
        }

        private static TextBox BuildTextBoxEditor(string bindingPath, double minWidth = 440)
        {
            var textBox = new TextBox
            {
                MinWidth = minWidth,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            textBox.SetBinding(TextBox.TextProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Path = new PropertyPath(bindingPath)
            });
            return textBox;
        }

        private FrameworkElement BuildOwnershipEditor()
        {
            return BuildLookupComboBox(
                nameof(ContragentEditViewModel.OwnershipOptions),
                nameof(ContragentEditViewModel.SelectedOwnershipId));
        }

        private FrameworkElement BuildRegionEditor()
        {
            return BuildLookupComboBox(
                nameof(ContragentEditViewModel.RegionOptions),
                nameof(ContragentEditViewModel.SelectedRegionId));
        }

        private FrameworkElement BuildAddressEditor()
        {
            return DialogLookupEditors.BuildAutoSuggestBox(
                nameof(ContragentEditViewModel.AddressSuggestionLabels),
                () => ViewModel.AddressReal,
                async text => await ViewModel.UpdateAddressOptionsAsync(text),
                TrySelectAddressSuggestion,
                ViewModel.CommitAddressInput,
                () => ViewModel.AddressReal,
                minWidth: 440,
                maxWidth: 520);
        }

        private bool TrySelectAddressSuggestion(string? label)
        {
            var option = ViewModel.FindAddressOption(label);
            if (option is null)
            {
                return false;
            }

            ViewModel.SelectAddressOption(option);
            return true;
        }

        private static ComboBox BuildLookupComboBox(string itemsSourcePath, string selectedValuePath)
        {
            var comboBox = new ComboBox
            {
                DisplayMemberPath = "Label",
                SelectedValuePath = "Value",
                MinWidth = 280,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            comboBox.SetBinding(ItemsControl.ItemsSourceProperty, new Binding
            {
                Path = new PropertyPath(itemsSourcePath)
            });
            comboBox.SetBinding(Selector.SelectedValueProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                Path = new PropertyPath(selectedValuePath)
            });
            return comboBox;
        }

        private static FrameworkElement BuildContactsEditor()
        {
            var editor = new DialogContactsEditor();
            editor.SetBinding(DialogContactsEditor.ContactsTextProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Path = new PropertyPath(nameof(ContragentEditViewModel.ContactsText))
            });
            return editor;
        }

        private static TextBox BuildMultilineTextBox(string bindingPath, double minHeight)
        {
            var textBox = new TextBox
            {
                MaxWidth = 520,
                MinWidth = 440,
                MinHeight = minHeight,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            textBox.SetBinding(TextBox.TextProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Path = new PropertyPath(bindingPath)
            });
            return textBox;
        }

    }
}
