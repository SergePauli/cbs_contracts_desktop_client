using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Pauli.WinUiKit.Controls;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;

namespace CbsContractsDesktopClient.Views.Orders
{
    public sealed class StageOrderNeedsAddDialog : AppEditDialog
    {
        private readonly Dropdown _order = new();

        public StageOrderNeedsAddDialog(
            IReadOnlyList<CbsTableFilterOptionDefinition> orders,
            int selectedCount)
        {
            Title = $"Добавление в заказ — позиций: {selectedCount}";
            Resources["ContentDialogMinWidth"] = 520d;
            Resources["ContentDialogMaxWidth"] = 600d;
            _order.DisplayMemberPath = nameof(CbsTableFilterOptionDefinition.Label);
            _order.TextMemberPath = nameof(CbsTableFilterOptionDefinition.Label);
            _order.MatchMemberPath = nameof(CbsTableFilterOptionDefinition.Label);
            _order.IsClearButtonEnabled = false;
            _order.ItemsSource = orders;
            Content = BuildEditContent(new Microsoft.UI.Xaml.Controls.Grid
            {
                Padding = new Thickness(8, 0, 8, 6),
                MinWidth = 500,
                Children =
                {
                    (UIElement)BuildLabeledControl("Заказ *", _order, 3)
                }
            });
            DialogChrome.Apply(this);
        }

        public long OrderId { get; private set; }

        public override bool Validate()
        {
            OrderId = JsonDataReader.TryGetLong(
                (_order.SelectedItem as CbsTableFilterOptionDefinition)?.Value) ?? 0;
            if (OrderId > 0)
            {
                return true;
            }

            ShowErrorInfo("Выберите заказ.");
            return false;
        }
    }
}
