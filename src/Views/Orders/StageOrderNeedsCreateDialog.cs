using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;

namespace CbsContractsDesktopClient.Views.Orders
{
    public sealed class StageOrderNeedsCreateDialog : AppEditDialog
    {
        private readonly StageOrderNeedsCreateViewModel _viewModel;

        public StageOrderNeedsCreateDialog(StageOrderNeedsCreateViewModel viewModel, int selectedCount)
        {
            _viewModel = viewModel;
            DataContext = viewModel;
            Title = $"Создание заказа — позиций: {selectedCount}";
            Resources["ContentDialogMinWidth"] = 520d;
            Resources["ContentDialogMaxWidth"] = 600d;
            Content = BuildEditContent(BuildContent());
            DialogChrome.Apply(this);
        }

        public override bool Validate()
        {
            try
            {
                _ = _viewModel.BuildInput();
                return true;
            }
            catch (Exception ex)
            {
                ShowErrorInfo(ex.Message);
                return false;
            }
        }

        private FrameworkElement BuildContent()
        {
            var grid = new Grid
            {
                Padding = new Thickness(8, 0, 8, 6),
                RowSpacing = 7,
                MinWidth = 500
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var supplier = DialogLookupEditors.BuildAutoSuggestBox(
                nameof(StageOrderNeedsCreateViewModel.ContragentSuggestionLabels),
                () => _viewModel.ContragentInput,
                _viewModel.UpdateContragentOptionsAsync,
                _viewModel.TrySelectContragent,
                _viewModel.CommitContragent,
                () => _viewModel.ContragentInput,
                minWidth: 484,
                maxWidth: 484,
                bindingSource: _viewModel);
            Add(grid, BuildLabeledControl("Поставщик *", supplier, 3), 0);

            var description = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 54
            };
            description.SetBinding(TextBox.TextProperty, new Microsoft.UI.Xaml.Data.Binding
            {
                Path = new PropertyPath(nameof(StageOrderNeedsCreateViewModel.Description)),
                Mode = Microsoft.UI.Xaml.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = Microsoft.UI.Xaml.Data.UpdateSourceTrigger.PropertyChanged
            });
            Add(grid, BuildLabeledControl("Описание", description, 3), 1);
            return grid;
        }

        private static void Add(Grid grid, UIElement element, int row)
        {
            Grid.SetRow((FrameworkElement)element, row);
            grid.Children.Add(element);
        }
    }
}
