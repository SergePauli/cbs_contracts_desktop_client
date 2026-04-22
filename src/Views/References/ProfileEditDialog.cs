using CbsContractsDesktopClient.ViewModels.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;

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

            var description = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap
            };
            description.SetBinding(TextBlock.TextProperty, new Binding
            {
                Path = new PropertyPath(nameof(ProfileEditViewModel.DescriptionText))
            });
            stack.Children.Add(description);

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
            AddRow(grid, "Роль", BuildReadOnlyTextBox(nameof(ProfileEditViewModel.Role)));
            AddRow(grid, "Должность", BuildPositionEditor());
            AddRow(grid, "Отдел", BuildDepartmentEditor());
            AddRow(grid, "Пароль", BuildPasswordEditor());
            AddRow(grid, "Активирован", BuildActivatedEditor());

            return grid;
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
                TextAlignment = TextAlignment.Right
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
