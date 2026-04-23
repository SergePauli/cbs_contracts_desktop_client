using CbsContractsDesktopClient.ViewModels.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.References
{
    public sealed class ProfileEditDialog : ContentDialog
    {
        public ProfileEditDialog(ProfileEditViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            Title = viewModel.DialogTitle;
            PrimaryButtonText = viewModel.PrimaryButtonText;
            CloseButtonText = "Закрыть";
            DefaultButton = ContentDialogButton.Close;
            IsPrimaryButtonEnabled = viewModel.CanSubmit;
            Content = BuildContent();
        }

        public ProfileEditViewModel ViewModel { get; }

        private UIElement BuildContent()
        {
            var root = new Grid
            {
                MinWidth = 620,
                MaxWidth = 760
            };

            var stack = new StackPanel
            {
                Spacing = 16
            };

            stack.Children.Add(BuildValidationInfoBar());
            stack.Children.Add(BuildFieldsGrid());

            root.Children.Add(new ScrollViewer
            {
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stack
            });

            return root;
        }

        private Grid BuildFieldsGrid()
        {
            var grid = new Grid
            {
                ColumnSpacing = 12,
                RowSpacing = 10
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(180)
            });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            if (!ViewModel.State.IsCreateMode)
            {
                AddRow(grid, "ID", BuildReadOnlyTextBox(nameof(ProfileEditViewModel.Id)));
            }

            AddRow(grid, "Логин", BuildReadOnlyTextBox(nameof(ProfileEditViewModel.Login)));
            AddRow(grid, "Email", BuildReadOnlyTextBox(nameof(ProfileEditViewModel.Email)));
            AddRow(grid, "ФИО", BuildReadOnlyTextBox(nameof(ProfileEditViewModel.PersonName)));
            AddRow(grid, "Роль", BuildRoleEditor());
            AddRow(grid, "Должность", BuildPositionEditor());
            AddRow(grid, "Отдел", BuildDepartmentEditor());
            AddRow(grid, "Пароль", BuildPasswordEditor());
            AddRow(grid, "Активирован", BuildActivatedEditor());

            return grid;
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
                Path = new PropertyPath(nameof(ProfileEditViewModel.ErrorInfoMessage))
            });
            infoBar.SetBinding(InfoBar.IsOpenProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                Path = new PropertyPath(nameof(ProfileEditViewModel.IsErrorInfoVisible))
            });
            return infoBar;
        }

        private static void AddRow(Grid grid, string labelText, FrameworkElement editor)
        {
            var rowIndex = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto
            });

            var label = new TextBlock
            {
                Text = labelText,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"]
            };
            Grid.SetRow(label, rowIndex);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            editor.HorizontalAlignment = HorizontalAlignment.Left;
            editor.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetRow(editor, rowIndex);
            Grid.SetColumn(editor, 1);
            grid.Children.Add(editor);
        }

        private static TextBox BuildReadOnlyTextBox(string bindingPath)
        {
            var textBox = new TextBox
            {
                IsReadOnly = true,
                IsTabStop = false,
                MinWidth = 280,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            textBox.SetBinding(TextBox.TextProperty, new Binding
            {
                Path = new PropertyPath(bindingPath)
            });
            return textBox;
        }

        private FrameworkElement BuildRoleEditor()
        {
            var button = new Button
            {
                MinWidth = 280,
                HorizontalAlignment = HorizontalAlignment.Left,
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            var contentGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textBlock = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            textBlock.SetBinding(TextBlock.TextProperty, new Binding
            {
                Path = new PropertyPath(nameof(ProfileEditViewModel.RoleSummaryText))
            });
            Grid.SetColumn(textBlock, 0);
            contentGrid.Children.Add(textBlock);

            var icon = new FontIcon
            {
                Glyph = "\uE70D",
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 10,
                Margin = new Thickness(6, 0, 0, 0),
                Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"],
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 1);
            contentGrid.Children.Add(icon);

            button.Content = contentGrid;

            var flyoutPanel = new StackPanel
            {
                Spacing = 2,
                MinWidth = 220
            };
            flyoutPanel.Children.Add(BuildRoleCheckBox("user", nameof(ProfileEditViewModel.IsRoleUserSelected)));
            flyoutPanel.Children.Add(BuildRoleCheckBox("admin", nameof(ProfileEditViewModel.IsRoleAdminSelected)));
            flyoutPanel.Children.Add(BuildRoleCheckBox("excel", nameof(ProfileEditViewModel.IsRoleExcelSelected)));
            flyoutPanel.Children.Add(BuildRoleCheckBox("intern", nameof(ProfileEditViewModel.IsRoleInternSelected)));

            button.Flyout = new Flyout
            {
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                Content = flyoutPanel
            };

            return button;
        }

        private static CheckBox BuildRoleCheckBox(string caption, string bindingPath)
        {
            var checkBox = new CheckBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                MinHeight = 20,
                Padding = new Thickness(0),
                Margin = new Thickness(4, 1, 4, 1)
            };
            checkBox.Content = new TextBlock
            {
                Text = caption,
                Margin = new Thickness(2, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            checkBox.SetBinding(ToggleButton.IsCheckedProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                Path = new PropertyPath(bindingPath)
            });
            return checkBox;
        }

        private FrameworkElement BuildPositionEditor()
        {
            var autoSuggestBox = new AutoSuggestBox
            {
                MinWidth = 280,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxSuggestionListHeight = 180,
                UpdateTextOnSelect = false,
                ItemTemplate = BuildPositionSuggestionTemplate()
            };
            autoSuggestBox.SetBinding(ItemsControl.ItemsSourceProperty, new Binding
            {
                Path = new PropertyPath(nameof(ProfileEditViewModel.PositionSuggestionLabels))
            });

            autoSuggestBox.Loaded += (_, _) =>
            {
                autoSuggestBox.Text = ViewModel.PositionInput;
            };

            autoSuggestBox.TextChanged += async (_, args) =>
            {
                if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
                {
                    return;
                }

                await ViewModel.UpdatePositionOptionsAsync(autoSuggestBox.Text);
                autoSuggestBox.IsSuggestionListOpen = ViewModel.PositionSuggestionLabels.Count > 0;
            };

            autoSuggestBox.SuggestionChosen += (_, args) =>
            {
                var label = args.SelectedItem as string;
                var option = ViewModel.FindPositionOption(label);
                if (option is null)
                {
                    return;
                }

                ViewModel.SelectPositionOption(option);
                autoSuggestBox.Text = ViewModel.PositionInput;
            };

            autoSuggestBox.QuerySubmitted += (_, args) =>
            {
                var chosenLabel = args.ChosenSuggestion as string;
                var option = ViewModel.FindPositionOption(chosenLabel);
                if (option is not null)
                {
                    ViewModel.SelectPositionOption(option);
                }
                else
                {
                    ViewModel.CommitPositionInput(autoSuggestBox.Text);
                }

                autoSuggestBox.Text = ViewModel.PositionInput;
                autoSuggestBox.IsSuggestionListOpen = false;
            };

            autoSuggestBox.LostFocus += (_, _) =>
            {
                ViewModel.CommitPositionInput(autoSuggestBox.Text);
                autoSuggestBox.Text = ViewModel.PositionInput;
                autoSuggestBox.IsSuggestionListOpen = false;
            };

            return autoSuggestBox;
        }

        private static DataTemplate BuildPositionSuggestionTemplate()
        {
            return (DataTemplate)XamlReader.Load(
                """
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                  <TextBlock Text="{Binding}" Margin="0" Padding="0" Height="24" TextTrimming="CharacterEllipsis" />
                </DataTemplate>
                """);
        }

        private FrameworkElement BuildDepartmentEditor()
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
                Path = new PropertyPath(nameof(ProfileEditViewModel.DepartmentOptions))
            });
            comboBox.SetBinding(Selector.SelectedValueProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                Path = new PropertyPath(nameof(ProfileEditViewModel.SelectedDepartmentId))
            });
            return comboBox;
        }

        private FrameworkElement BuildPasswordEditor()
        {
            var passwordBox = new PasswordBox
            {
                MinWidth = 280,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            passwordBox.Password = ViewModel.Password;
            passwordBox.PasswordChanged += (_, _) => ViewModel.Password = passwordBox.Password;
            return passwordBox;
        }

        private FrameworkElement BuildActivatedEditor()
        {
            var checkBox = new CheckBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.SetBinding(ToggleButton.IsCheckedProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                Path = new PropertyPath(nameof(ProfileEditViewModel.IsActivated))
            });
            return checkBox;
        }
    }
}
