using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.Stores.Orders;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Orders
{
    public sealed class StageSupplyView : UserControl
    {
        private readonly StageSupplyStore _store;
        private readonly CbsTableView _table = new()
        {
            RowHeight = 24,
            SupportsRowSelection = true,
            Columns = BuildColumns()
        };
        private readonly Button _editButton;
        private readonly Button _deleteButton;
        private readonly IReferenceLookupCacheService _lookupCache;
        private IReadOnlySet<long> _selectedStatusIds = new HashSet<long>();
        private bool _isLoaded;

        public StageSupplyView(StageSupplyStore store)
        {
            _store = store;
            _lookupCache = App.Services.GetRequiredService<IReferenceLookupCacheService>();
            _table.TableStateKey = $"/stages/{store.StageId}/supply";
            var addButton = CompactButton("\uE710", "Добавить позицию");
            _editButton = CompactButton("\uE70F", "Редактировать позицию");
            _deleteButton = CompactButton("\uE74D", "Удалить позицию");
            _editButton.IsEnabled = false;
            _deleteButton.IsEnabled = false;
            addButton.Click += (_, _) => CreateRequested?.Invoke(this, new StageSupplyCreateRequestedEventArgs(addButton));
            _editButton.Click += (_, _) =>
            {
                if (_store.SelectedRow is not null && CanEdit(_store.SelectedRow))
                    EditRequested?.Invoke(this, new StageSupplyEditRequestedEventArgs(_store.SelectedRow, _editButton));
            };
            _deleteButton.Click += (_, _) =>
            {
                if (_store.SelectedRow is not null && CanEdit(_store.SelectedRow))
                    DeleteRequested?.Invoke(this, new StageSupplyDeleteRequestedEventArgs(_store.SelectedRow));
            };
            _table.RowSelectionChanged += (_, args) =>
            {
                _store.SelectedRow = args.IsSelected ? args.Row : null;
                _editButton.IsEnabled = _store.SelectedRow is not null && CanEdit(_store.SelectedRow);
                _deleteButton.IsEnabled = _store.SelectedRow is not null && CanEdit(_store.SelectedRow);
            };
            _table.RowDoubleTapped += (_, args) =>
            {
                if (CanEdit(args.Row))
                    EditRequested?.Invoke(this, new StageSupplyEditRequestedEventArgs(args.Row, _table));
            };
            _table.FilterRequested += (_, args) =>
            {
                if (!string.Equals(args.FieldKey, "status", StringComparison.Ordinal))
                {
                    return;
                }

                _selectedStatusIds = ReadSelectedStatusIds(args.Value);
                ApplyRows();
            };
            _store.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(StageSupplyStore.Rows))
                {
                    ApplyRows();
                }
                else if (args.PropertyName == nameof(StageSupplyStore.IsLoading))
                {
                    _table.IsLoading = _store.IsLoading;
                }
            };

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { addButton, _editButton, _deleteButton }
            };
            var layout = new Grid { RowSpacing = 5, MinHeight = 205 };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(190) });
            Grid.SetRow(actions, 0);
            Grid.SetRow(_table, 1);
            layout.Children.Add(actions);
            layout.Children.Add(_table);
            Content = layout;
            Loaded += async (_, _) =>
            {
                if (_isLoaded) return;
                _isLoaded = true;
                try
                {
                    var statusOptions = await _lookupCache.GetOptionsAsync("OrderStatus");
                    _table.MultiSelectOptionsSources =
                        new Dictionary<string, IReadOnlyList<CbsTableFilterOptionDefinition>>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["OrderStatus"] = statusOptions
                        };
                    await ReloadAsync();
                }
                catch (Exception ex)
                {
                    LoadFailed?.Invoke(this, new StageSupplyLoadFailedEventArgs(ex));
                }
            };
        }

        public event EventHandler<StageSupplyCreateRequestedEventArgs>? CreateRequested;
        public event EventHandler<StageSupplyEditRequestedEventArgs>? EditRequested;
        public event EventHandler<StageSupplyDeleteRequestedEventArgs>? DeleteRequested;

        public async Task ReloadAsync(long? expectedRowId = null)
        {
            try
            {
                await _store.ReloadAsync(expectedRowId);
            }
            catch (Exception ex)
            {
                LoadFailed?.Invoke(this, new StageSupplyLoadFailedEventArgs(ex));
            }
        }

        public event EventHandler<StageSupplyLoadFailedEventArgs>? LoadFailed;

        private static Button CompactButton(string glyph, string tooltip)
        {
            var button = new Button
            {
                Width = 30,
                Height = 26,
                Padding = new Thickness(5, 2, 5, 2),
                Content = new FontIcon { Glyph = glyph, FontSize = 12 }
            };
            ToolTipService.SetToolTip(button, tooltip);
            return button;
        }

        private static bool CanEdit(TableDataRow row) =>
            string.IsNullOrWhiteSpace(row.GetValue("order.order_number")?.ToString());

        private static IReadOnlyList<CbsTableColumnDefinition> BuildColumns() =>
        [
            Column("isecurity_tool", "Товар", "isecurity_tool.name", "18rem"),
            Column("amount", "Кол-во", "amount", "4rem", CbsTableColumnAlignment.Right),
            Column("severity", "Важность", "severity", "10rem", bodyTemplateKey: "StageOrderSeverity"),
            Column("order", "Заказ", "order.order_number", "9rem"),
            Column("supplier", "Поставщик", "order.supplier.name", "14rem"),
            StatusColumn(),
            Column("price_cost", "Цена", "price_cost", "8rem", CbsTableColumnAlignment.Right),
            Column("cost", "Сумма", "cost", "9rem", CbsTableColumnAlignment.Right),
            Column("description", "Описание", "description", "16rem")
        ];

        private static CbsTableColumnDefinition StatusColumn() => new()
        {
            FieldKey = "status",
            Header = "Статус",
            ApiField = "order.status.name",
            DisplayField = "order.status.name",
            FilterField = "order.status.id",
            DefaultWidth = "10rem",
            Alignment = CbsTableColumnAlignment.Left,
            IsSortable = false,
            BodyTemplateKey = "OrderDeliveryStatus",
            IsFilterable = true,
            Filter = new CbsTableColumnFilterDefinition
            {
                IsEnabled = true,
                EditorKind = CbsTableFilterEditorKind.MultiSelect,
                Mode = DataFilterMode.Numeric,
                MatchMode = DataFilterMatchMode.In,
                OptionsSourceKey = "OrderStatus"
            }
        };

        private void ApplyRows()
        {
            var rows = _selectedStatusIds.Count == 0
                ? _store.Rows
                : _store.Rows
                    .Where(row => JsonDataReader.TryGetLong(row.GetValue("order.status.id")) is long statusId
                        && _selectedStatusIds.Contains(statusId))
                    .ToList();
            _table.ItemsSource = rows;
            _table.LoadedCount = rows.Count;
            _table.TotalCount = rows.Count;
            _store.SelectedRow = null;
            _editButton.IsEnabled = false;
            _deleteButton.IsEnabled = false;
        }

        private static IReadOnlySet<long> ReadSelectedStatusIds(object? value)
        {
            if (value is not System.Collections.IEnumerable values || value is string)
            {
                return new HashSet<long>();
            }

            return values.Cast<object?>()
                .Select(JsonDataReader.TryGetLong)
                .Where(static id => id is not null)
                .Select(static id => id!.Value)
                .ToHashSet();
        }

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

    public sealed class StageSupplyCreateRequestedEventArgs(FrameworkElement anchor) : EventArgs
    {
        public FrameworkElement Anchor { get; } = anchor;
    }

    public sealed class StageSupplyEditRequestedEventArgs(TableDataRow row, FrameworkElement anchor) : EventArgs
    {
        public TableDataRow Row { get; } = row;
        public FrameworkElement Anchor { get; } = anchor;
    }

    public sealed class StageSupplyDeleteRequestedEventArgs(TableDataRow row) : EventArgs
    {
        public TableDataRow Row { get; } = row;
    }

    public sealed class StageSupplyLoadFailedEventArgs(Exception exception) : EventArgs
    {
        public Exception Exception { get; } = exception;
    }
}
