using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Pauli.WinUiKit.Controls;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;

namespace CbsContractsDesktopClient.Views.Orders
{
    public sealed partial class OrderEditDialog : AppEditDialog
    {
        private readonly OrderEditViewModel _vm;
        private readonly Dropdown _status = new();
        private readonly Flyout _costMismatchFlyout;

        public OrderEditDialog(OrderEditViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            _costMismatchFlyout = (Flyout)Resources["CostMismatchFlyout"];
            ((FrameworkElement)_costMismatchFlyout.Content).DataContext = viewModel;
            DataContext = viewModel;
            Title = viewModel.State.IsCreateMode ? "Создание заказа" : "Редактирование заказа";
            Resources["ContentDialogMinWidth"] = 720d;
            Resources["ContentDialogMaxWidth"] = 780d;
            ConfigureStatus();
            Content = BuildEditContent(BuildContent());
            DialogChrome.Apply(this);
        }

        public override bool Validate()
        {
            _vm.SelectedStatus = _status.SelectedItem as Models.Table.CbsTableFilterOptionDefinition;
            try
            {
                _ = _vm.State.IsCreateMode
                    ? OrderEditPayloadBuilder.BuildForCreate(_vm)
                    : OrderEditPayloadBuilder.BuildForUpdate(_vm);
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
            var grid = new Grid { Padding = new Thickness(8, 0, 8, 8), RowSpacing = 7, ColumnSpacing = 8, MinWidth = 720 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            for (var i = 0; i < 4; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var number = new TextBox();
            number.SetBinding(TextBox.TextProperty, TwoWay(nameof(OrderEditViewModel.OrderNumber)));
            Add(grid, BuildLabeledControl("Номер счёта", number, 3), 0, 0);
            Add(grid, BuildLabeledControl("Статус *", _status, 3), 0, 1);
            var cost = BuildMoneyInputTextBox();
            cost.SetBinding(TextBox.TextProperty, TwoWay(nameof(OrderEditViewModel.CostText)));
            cost.GotFocus += Cost_GotFocus;
            cost.TextChanged += (_, _) => _costMismatchFlyout.Hide();
            Add(grid, BuildLabeledControl("Сумма", cost, 3), 0, 2);

            var supplier = DialogLookupEditors.BuildAutoSuggestBox(
                nameof(OrderEditViewModel.ContragentSuggestionLabels),
                () => _vm.ContragentInput,
                _vm.UpdateContragentOptionsAsync,
                _vm.TrySelectContragent,
                _vm.CommitContragent,
                () => _vm.ContragentInput,
                minWidth: 690,
                maxWidth: 690,
                bindingSource: _vm);
            var supplierControl = (FrameworkElement)BuildLabeledControl("Поставщик *", supplier, 3);
            Grid.SetColumnSpan(supplierControl, 3);
            Add(grid, supplierControl, 1, 0);

            var dates = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            dates.Children.Add(BuildLabeledControl("Запрошен", DateEditor(_vm.RequestedAt, value => _vm.RequestedAt = value), 3));
            dates.Children.Add(BuildLabeledControl("Заказан", DateEditor(_vm.OrderedAt, value => _vm.OrderedAt = value), 3));
            dates.Children.Add(BuildLabeledControl("Оплачен", DateEditor(_vm.PaymentAt, value => _vm.PaymentAt = value), 3));
            dates.Children.Add(BuildLabeledControl("Получен", DateEditor(_vm.ReceivedAt, value => _vm.ReceivedAt = value), 3));
            Grid.SetColumnSpan(dates, 3);
            Add(grid, dates, 2, 0);

            var description = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 58 };
            description.SetBinding(TextBox.TextProperty, TwoWay(nameof(OrderEditViewModel.Description)));
            var descriptionControl = (FrameworkElement)BuildLabeledControl("Описание", description, 3);
            Grid.SetColumnSpan(descriptionControl, 3);
            Add(grid, descriptionControl, 3, 0);
            return grid;
        }

        private void ConfigureStatus()
        {
            _status.DisplayMemberPath = nameof(Models.Table.CbsTableFilterOptionDefinition.Label);
            _status.TextMemberPath = nameof(Models.Table.CbsTableFilterOptionDefinition.Label);
            _status.MatchMemberPath = nameof(Models.Table.CbsTableFilterOptionDefinition.Label);
            _status.IsClearButtonEnabled = false;
            _status.ItemsSource = _vm.StatusOptions;
            _status.SelectedItem = _vm.SelectedStatus;
        }

        private void Cost_GotFocus(object sender, RoutedEventArgs args)
        {
            if (_vm.State.IsCreateMode || _costMismatchFlyout.IsOpen) return;
            decimal? difference;
            try
            {
                difference = _vm.GetCostDifference();
            }
            catch (FormatException)
            {
                ShowErrorInfo("Введите корректную сумму заказа.");
                return;
            }
            catch (OverflowException)
            {
                ShowErrorInfo("Сумма заказа выходит за допустимый диапазон.");
                return;
            }
            if (difference == 0m) return;
            _vm.CostDifferenceText = difference?.ToString("+0.00;-0.00;0.00") ?? "сумма заказа не указана";
            _costMismatchFlyout.ShowAt((FrameworkElement)sender, new FlyoutShowOptions
            {
                ShowMode = FlyoutShowMode.Transient
            });
        }

        private void ApplyCalculatedCost_Click(object sender, RoutedEventArgs args)
        {
            _vm.ApplyCalculatedCost();
            _costMismatchFlyout.Hide();
        }

        private static CalendarInput DateEditor(DateTimeOffset? value, Action<DateTimeOffset?> setValue)
        {
            var input = new CalendarInput { Date = value, Width = 106 };
            input.DateChanged += (_, args) => setValue(args.NewDate);
            return input;
        }

        private static Microsoft.UI.Xaml.Data.Binding TwoWay(string path) => new()
        {
            Path = new PropertyPath(path), Mode = Microsoft.UI.Xaml.Data.BindingMode.TwoWay,
            UpdateSourceTrigger = Microsoft.UI.Xaml.Data.UpdateSourceTrigger.PropertyChanged
        };
        private static void Add(Grid grid, UIElement element, int row, int column)
        {
            Grid.SetRow((FrameworkElement)element, row); Grid.SetColumn((FrameworkElement)element, column); grid.Children.Add(element);
        }
    }
}
