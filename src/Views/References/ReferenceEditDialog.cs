using System.ComponentModel;
using CbsContractsDesktopClient.Helpers;
using CbsContractsDesktopClient.ViewModels.References;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.References
{
    public sealed class ReferenceEditDialog : ContentDialog
    {
        private readonly List<(TextBox Editor, ReferenceEditFieldViewModel ViewModel)> _textEditors = [];

        public ReferenceEditDialog(ReferenceEditViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            Title = viewModel.DialogTitle;
            PrimaryButtonText = viewModel.PrimaryButtonText;
            CloseButtonText = "Отмена";
            DefaultButton = ContentDialogButton.Primary;
            IsPrimaryButtonEnabled = viewModel.CanSubmit;
            PrimaryButtonClick += ContentDialog_PrimaryButtonClick;
            Unloaded += OnUnloaded;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            foreach (var field in viewModel.Fields)
            {
                field.PropertyChanged += OnFieldPropertyChanged;
            }
            Content = BuildContent();
        }

        public ReferenceEditViewModel ViewModel { get; }

        private static BoolVisibilityConverter BoolVisibilityConverter { get; } = new();

        private static Style? BodyTextBlockStyle => Application.Current.Resources["BodyTextBlockStyle"] as Style;

        private static Style? BodyStrongTextBlockStyle => Application.Current.Resources["BodyStrongTextBlockStyle"] as Style;

        private static Brush? ShellPrimaryTextBrush => Application.Current.Resources["ShellPrimaryTextBrush"] as Brush;

        private static Brush? ShellSecondaryTextBrush => Application.Current.Resources["ShellSecondaryTextBrush"] as Brush;

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReferenceEditViewModel.CanSubmit))
            {
                IsPrimaryButtonEnabled = ViewModel.CanSubmit;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            foreach (var field in ViewModel.Fields)
            {
                field.PropertyChanged -= OnFieldPropertyChanged;
            }
            PrimaryButtonClick -= ContentDialog_PrimaryButtonClick;
            Unloaded -= OnUnloaded;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SyncTextEditorsToViewModel();

            if (!ViewModel.CanSubmit)
            {
                args.Cancel = true;
            }
        }

        private void OnFieldPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdatePrimaryButtonState();
        }

        private UIElement BuildContent()
        {
            var root = new Grid
            {
                MinWidth = 560,
                MaxWidth = 720
            };

            var stack = new StackPanel
            {
                Spacing = 16
            };

            var description = new TextBlock
            {
                Style = BodyTextBlockStyle,
                Foreground = ShellSecondaryTextBrush,
                TextWrapping = TextWrapping.Wrap
            };
            description.SetBinding(TextBlock.TextProperty, new Binding
            {
                Path = new PropertyPath(nameof(ReferenceEditViewModel.DescriptionText))
            });
            stack.Children.Add(description);

            var fieldsPanel = new StackPanel();
            foreach (var item in ViewModel.Fields)
            {
                fieldsPanel.Children.Add(BuildFieldEditor(item));
            }

            stack.Children.Add(new ScrollViewer
            {
                MaxHeight = 480,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = fieldsPanel
            });

            root.Children.Add(stack);
            return root;
        }

        private UIElement BuildFieldEditor(ReferenceEditFieldViewModel item)
        {
            var container = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 12),
                Spacing = 6,
                DataContext = item
            };

            var label = new TextBlock
            {
                Style = BodyStrongTextBlockStyle,
                Foreground = ShellPrimaryTextBrush
            };
            label.SetBinding(TextBlock.TextProperty, new Binding
            {
                Path = new PropertyPath(nameof(ReferenceEditFieldViewModel.DisplayLabel))
            });
            container.Children.Add(label);

            var textBox = new TextBox();
            textBox.IsTabStop = !item.IsReadOnly;
            textBox.IsHitTestVisible = !item.IsReadOnly;
            textBox.SetBinding(TextBox.IsReadOnlyProperty, new Binding
            {
                Path = new PropertyPath(nameof(ReferenceEditFieldViewModel.IsReadOnly))
            });
            textBox.Text = item.TextValue;
            _textEditors.Add((textBox, item));
            textBox.TextChanged += (_, _) =>
            {
                SyncTextEditorToViewModel(textBox, item);
                UpdatePrimaryButtonState();
            };
            textBox.SetBinding(UIElement.VisibilityProperty, new Binding
            {
                Path = new PropertyPath(nameof(ReferenceEditFieldViewModel.IsTextEditor)),
                Converter = BoolVisibilityConverter
            });
            container.Children.Add(textBox);

            var checkBox = new CheckBox
            {
                Content = "Установлено"
            };
            checkBox.SetBinding(ToggleButton.IsCheckedProperty, new Binding
            {
                Mode = BindingMode.TwoWay,
                Path = new PropertyPath(nameof(ReferenceEditFieldViewModel.BoolValue))
            });
            checkBox.SetBinding(Control.IsEnabledProperty, new Binding
            {
                Path = new PropertyPath(nameof(ReferenceEditFieldViewModel.IsEditableBoolean))
            });
            checkBox.SetBinding(UIElement.VisibilityProperty, new Binding
            {
                Path = new PropertyPath(nameof(ReferenceEditFieldViewModel.IsBooleanEditor)),
                Converter = BoolVisibilityConverter
            });
            container.Children.Add(checkBox);

            var validation = new TextBlock
            {
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.IndianRed)
            };
            validation.SetBinding(TextBlock.TextProperty, new Binding
            {
                Path = new PropertyPath(nameof(ReferenceEditFieldViewModel.ValidationMessage))
            });
            validation.SetBinding(UIElement.VisibilityProperty, new Binding
            {
                Path = new PropertyPath(nameof(ReferenceEditFieldViewModel.HasValidationError)),
                Converter = BoolVisibilityConverter
            });
            container.Children.Add(validation);

            return container;
        }

        private void SyncTextEditorsToViewModel()
        {
            foreach (var (editor, viewModel) in _textEditors)
            {
                SyncTextEditorToViewModel(editor, viewModel);
            }
        }

        private void UpdatePrimaryButtonState()
        {
            SyncTextEditorsToViewModel();
            IsPrimaryButtonEnabled = ViewModel.CanSubmit;
        }

        private static void SyncTextEditorToViewModel(TextBox editor, ReferenceEditFieldViewModel viewModel)
        {
            if (!string.Equals(viewModel.TextValue, editor.Text, StringComparison.Ordinal))
            {
                viewModel.TextValue = editor.Text;
            }
        }
    }
}
