using CbsContractsDesktopClient.ViewModels.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.References
{
    public sealed class EmployeeEditDialog : ContentDialog
    {
        public EmployeeEditDialog(EmployeeEditViewModel viewModel)
        {
            MinWidth = 500;            
            MaxWidth = 800;
            FullSizeDesired = false;
            HorizontalAlignment = HorizontalAlignment.Center;
            ViewModel = viewModel;
            DataContext = viewModel;
            Title = viewModel.DialogTitle;
            PrimaryButtonText = viewModel.PrimaryButtonText;
            CloseButtonText = "Закрыть";
            DefaultButton = ContentDialogButton.Close;
            SetBinding(IsPrimaryButtonEnabledProperty, new Binding
            {
                Path = new PropertyPath(nameof(EmployeeEditViewModel.CanSubmit))
            });
            Resources["ContentDialogMinWidth"] = 500d;
            Resources["ContentDialogMaxWidth"] = 800d;
            Content = BuildContent();
        }

        public EmployeeEditViewModel ViewModel { get; }

        private UIElement BuildContent()
        {
            var host = new Grid
            {
                Width = 700,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var root = new Grid
            {
                MinWidth = 400,                
                MaxWidth = 700,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var stack = new StackPanel
            {
                Spacing = 14
            };
            stack.Children.Add(BuildValidationInfoBar());
            stack.Children.Add(BuildFieldsGrid());

            root.Children.Add(new ScrollViewer
            {
                MaxHeight = 560,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stack
            });

            host.Children.Add(root);
            return host;
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
                Width = new GridLength(90)
            });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            if (!ViewModel.State.IsCreateMode)
            {
                AddRow(grid, "ID", BuildReadOnlyTextBox(nameof(EmployeeEditViewModel.Id)));
            }

            AddRow(grid, "ФИО", BuildTextBoxEditor(nameof(EmployeeEditViewModel.PersonName)), isRequired: true);
            AddRow(grid, "Должность", BuildPositionEditor(), isRequired: true);
            AddRow(grid, "Контрагент", BuildContragentEditor(), isRequired: true);
            AddRow(grid, "Контакты", BuildContactsEditor(), isRequired: true);
            AddRow(grid, "Актуален", BuildUsedEditor());
            AddRow(grid, "Пор.", BuildTextBoxEditor(nameof(EmployeeEditViewModel.PriorityText), minWidth: 80));
            AddRow(grid, "Описание", BuildDescriptionEditor());

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
                Path = new PropertyPath(nameof(EmployeeEditViewModel.ErrorInfoMessage))
            });
            infoBar.SetBinding(InfoBar.IsOpenProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                Path = new PropertyPath(nameof(EmployeeEditViewModel.IsErrorInfoVisible))
            });
            return infoBar;
        }

        private static void AddRow(Grid grid, string labelText, FrameworkElement editor, bool isRequired = false)
        {
            var rowIndex = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto
            });

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
            if (!isRequired)
            {
                return new TextBlock
                {
                    Text = labelText,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Right,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"]
                };
            }

            var labelPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Orientation = Orientation.Horizontal,
                Spacing = 2
            };
            labelPanel.Children.Add(new TextBlock
            {
                Text = labelText,
                TextAlignment = TextAlignment.Right,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["ShellSecondaryTextBrush"]
            });
            labelPanel.Children.Add(new TextBlock
            {
                Text = "*",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Firebrick)
            });

            return labelPanel;
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

        private static TextBox BuildTextBoxEditor(string bindingPath, double minWidth = 360)
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

        private FrameworkElement BuildPositionEditor()
        {
            return DialogLookupEditors.BuildAutoSuggestBox(
                nameof(EmployeeEditViewModel.PositionSuggestionLabels),
                () => ViewModel.PositionInput,
                async text => await ViewModel.UpdatePositionOptionsAsync(text),
                TrySelectPositionSuggestion,
                text => ViewModel.CommitPositionInput(text),
                () => ViewModel.PositionInput,
                minWidth: 200,
                maxWidth: 500);
        }

        private bool TrySelectPositionSuggestion(string? label)
        {
            var option = ViewModel.FindPositionOption(label);
            if (option is null)
            {
                return false;
            }

            ViewModel.SelectPositionOption(option);
            return true;
        }

        private FrameworkElement BuildContragentEditor()
        {
            return DialogLookupEditors.BuildAutoSuggestBox(
                nameof(EmployeeEditViewModel.ContragentSuggestionLabels),
                () => ViewModel.ContragentInput,
                async text => await ViewModel.UpdateContragentOptionsAsync(text),
                TrySelectContragentSuggestion,
                text => ViewModel.CommitContragentInput(text),
                () => ViewModel.ContragentInput,
                minWidth: 400,
                maxWidth: 500,
                maxSuggestionListHeight: 240);
        }

        private bool TrySelectContragentSuggestion(string? label)
        {
            var option = ViewModel.FindContragentOption(label);
            if (option is null)
            {
                return false;
            }

            ViewModel.SelectContragentOption(option);
            return true;
        }

        private static FrameworkElement BuildContactsEditor()
        {
            var editor = new DialogContactsEditor();
            editor.SetBinding(DialogContactsEditor.ContactsTextProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Path = new PropertyPath(nameof(EmployeeEditViewModel.ContactsText))
            });
            return editor;
        }

        private static TextBox BuildDescriptionEditor()
        {
            return BuildMultilineTextBox(nameof(EmployeeEditViewModel.Description), minHeight: 76);
        }

        private static TextBox BuildMultilineTextBox(string bindingPath, double minHeight)
        {
            var textBox = new TextBox
            {                
                MaxWidth = 500,
                MinWidth = 400,
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

        private FrameworkElement BuildUsedEditor()
        {
            var checkBox = new CheckBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.SetBinding(CheckBox.IsCheckedProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                Path = new PropertyPath(nameof(EmployeeEditViewModel.IsUsed))
            });
            return checkBox;
        }
    }
}
