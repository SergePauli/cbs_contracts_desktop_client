using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services.Orders;
using CbsContractsDesktopClient.ViewModels.Workflow;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Pauli.WinUiKit.Controls;
using CbsContractsDesktopClient.Views.References;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;

namespace CbsContractsDesktopClient.Views.Orders
{
    public sealed class StageSupplyEditFlyout
    {
        private readonly StageSupplyEditWorkflow _workflow;
        private readonly StageSupplyEditViewModel _viewModel;
        private readonly Flyout _flyout = new();
        private readonly InfoBar _error = new() { Severity = InfoBarSeverity.Error, IsOpen = false };
        private readonly AutoSuggestBox _tool;
        private readonly Dropdown _severity = new();
        private readonly TextBox _amount = new();
        private readonly Button _save = new() { Content = "Сохранить" };
        private readonly Button _cancel = new() { Content = "Отмена" };
        private TaskCompletionSource<bool>? _completion;
        public long? SavedRowId { get; private set; }

        public StageSupplyEditFlyout(
            StageSupplyEditWorkflow workflow,
            StageSupplyEditViewModel viewModel)
        {
            _workflow = workflow;
            _viewModel = viewModel;
            _tool = DialogLookupEditors.BuildAutoSuggestBox(
                nameof(StageSupplyEditViewModel.ToolSuggestions),
                () => _viewModel.ToolInput,
                _viewModel.UpdateToolSuggestionsAsync,
                _viewModel.TrySelectTool,
                _viewModel.CommitTool,
                () => _viewModel.ToolInput,
                minWidth: 496,
                maxWidth: 496,
                maxSuggestionListHeight: 240,
                bindingSource: _viewModel);
            ConfigureCompactInput(_tool);
            ConfigureEditors();
            _flyout.Content = BuildContent();
            _flyout.Placement = FlyoutPlacementMode.Top;
            _flyout.FlyoutPresenterStyle = new Style(typeof(FlyoutPresenter))
            {
                Setters =
                {
                    new Setter(FrameworkElement.MinWidthProperty, 520d),
                    new Setter(FrameworkElement.MaxWidthProperty, 520d),
                    new Setter(Control.PaddingProperty, new Thickness(12))
                }
            };
            _flyout.Closed += (_, _) => _completion?.TrySetResult(false);
            _save.Click += async (_, _) => await SaveAsync();
            _cancel.Click += (_, _) => _flyout.Hide();
        }

        public Task<bool> ShowAsync(FrameworkElement target)
        {
            _completion = new TaskCompletionSource<bool>();
            _flyout.ShowAt(target);
            return _completion.Task;
        }

        private void ConfigureEditors()
        {
            _severity.DisplayMemberPath = nameof(ReferenceEnumOption.Label);
            _severity.TextMemberPath = nameof(ReferenceEnumOption.Label);
            _severity.MatchMemberPath = nameof(ReferenceEnumOption.Label);
            _severity.ItemsSource = _viewModel.SeverityOptions;
            _severity.SelectedItem = _viewModel.SelectedSeverity;
            _severity.IsClearButtonEnabled = false;
            _severity.MinHeight = 0;
            _severity.Height = 28;
            _severity.UseCompactDensity = true;
            _severity.HorizontalAlignment = HorizontalAlignment.Stretch;
            _severity.SelectionChanged += (_, _) => _viewModel.SelectedSeverity = _severity.SelectedItem as ReferenceEnumOption;

            _amount.Text = _viewModel.Amount;
            ConfigureCompactInput(_amount);
            _amount.SetBinding(TextBox.TextProperty, new Microsoft.UI.Xaml.Data.Binding
            {
                Source = _viewModel,
                Path = new PropertyPath(nameof(StageSupplyEditViewModel.Amount)),
                Mode = Microsoft.UI.Xaml.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = Microsoft.UI.Xaml.Data.UpdateSourceTrigger.PropertyChanged
            });
        }

        private FrameworkElement BuildContent()
        {
            var grid = new Grid { RowSpacing = 7, ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            for (var index = 0; index < 3; index++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var tool = (FrameworkElement)BuildLabeledControl("Наименование *", _tool, 3);
            Grid.SetColumnSpan(tool, 2);
            grid.Children.Add(tool);

            var amount = (FrameworkElement)BuildLabeledControl("Количество", _amount, 3);
            Grid.SetRow(amount, 1);
            grid.Children.Add(amount);
            var severity = (FrameworkElement)BuildLabeledControl("Важность *", _severity, 3);
            Grid.SetRow(severity, 1);
            Grid.SetColumn(severity, 1);
            grid.Children.Add(severity);

            var footer = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 3, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(_save, 1);
            Grid.SetColumn(_cancel, 2);
            footer.Children.Add(_save);
            footer.Children.Add(_cancel);
            Grid.SetRow(footer, 2);
            Grid.SetColumnSpan(footer, 2);
            grid.Children.Add(footer);

            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(_error);
            stack.Children.Add(grid);
            return stack;
        }

        private async Task SaveAsync()
        {
            _error.IsOpen = false;
            _save.IsEnabled = false;
            try
            {
                _viewModel.CommitTool(_tool.Text);
                SavedRowId = await _workflow.SaveAsync(_viewModel);
                _completion?.TrySetResult(true);
                _flyout.Hide();
            }
            catch (Exception ex)
            {
                _error.Message = ex.Message;
                _error.IsOpen = true;
            }
            finally
            {
                _save.IsEnabled = true;
            }
        }

        private static void ConfigureCompactInput(Control control)
        {
            control.MinHeight = 0;
            control.Height = 28;
            control.VerticalContentAlignment = VerticalAlignment.Center;
        }

    }
}
