using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Stores.Orders;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Orders
{
    public sealed class OrderPositionsTableView : UserControl
    {
        private readonly OrderPositionsStore _store;
        private readonly CbsTableView _table = new()
        {
            TableStateKey = "/orders/positions",
            RowHeight = 24,
            SupportsRowSelection = true,
            Columns = BuildColumns()
        };

        public OrderPositionsTableView(OrderPositionsStore store)
        {
            _store = store;
            Height = 270;
            Content = new Grid
            {
                Children = { _table }
            };
            _table.RowSelectionChanged += (_, args) => _store.SelectedRow = args.IsSelected ? args.Row : null;
            _table.RowDoubleTapped += (_, args) => PositionEditRequested?.Invoke(this, new StageOrderEditRequestedEventArgs(args.Row));
            _store.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(OrderPositionsStore.Rows))
                {
                    _table.ItemsSource = _store.Rows;
                    _table.LoadedCount = _store.Rows.Count;
                    _table.TotalCount = _store.Rows.Count;
                }
                else if (args.PropertyName == nameof(OrderPositionsStore.IsLoading))
                {
                    _table.IsLoading = _store.IsLoading;
                }
            };
        }

        public event EventHandler<StageOrderEditRequestedEventArgs>? PositionEditRequested;

        private static IReadOnlyList<CbsTableColumnDefinition> BuildColumns() =>
        [
            Column("stage", "Этап", "stage.name", "9rem"),
            Column("contragent", "Контрагент", "stage.contragent", "18rem"),
            Column("isecurity_tool", "Товар", "isecurity_tool.name", "22rem"),
            Column("amount", "Кол-во", "amount", "3.2rem", CbsTableColumnAlignment.Right),
            Column("price_cost", "Цена", "price_cost", "10rem", CbsTableColumnAlignment.Right),
            Column("cost", "Сумма", "cost", "11rem", CbsTableColumnAlignment.Right),
            Column("severity", "Важность", "severity", "18rem", bodyTemplateKey: "StageOrderSeverity"),
            Column("description", "Описание", "description", "22rem")
        ];

        private static CbsTableColumnDefinition Column(
            string key,
            string header,
            string displayField,
            string width,
            CbsTableColumnAlignment alignment = CbsTableColumnAlignment.Left,
            string? bodyTemplateKey = null) => new()
        {
            FieldKey = key,
            Header = header,
            ApiField = displayField,
            DisplayField = displayField,
            DefaultWidth = width,
            Alignment = alignment,
            IsSortable = false,
            BodyTemplateKey = bodyTemplateKey
        };
    }

    public sealed class StageOrderEditRequestedEventArgs(TableDataRow row) : EventArgs
    {
        public TableDataRow Row { get; } = row;
    }
}
