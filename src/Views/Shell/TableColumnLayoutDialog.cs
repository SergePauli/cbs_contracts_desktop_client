using CbsContractsDesktopClient.Models.Table;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Shell
{
    internal sealed class TableColumnLayoutDialog : ContentDialog
    {
        private readonly StackPanel _itemsHost = new() { Spacing = 4 };
        private readonly List<ColumnLayoutItem> _items;

        public TableColumnLayoutDialog(IReadOnlyList<CbsTableColumnDefinition> columns)
        {
            _items = columns
                .Select(static column => new ColumnLayoutItem(column))
                .ToList();

            Title = "Расстановка столбцов";
            PrimaryButtonText = "Применить";
            CloseButtonText = "Отмена";
            DefaultButton = ContentDialogButton.Primary;
            Content = BuildContent();
            RebuildItems();
        }

        public IReadOnlyList<CbsTableColumnDefinition> BuildColumns()
        {
            return _items.Select(static item =>
            {
                item.Column.IsVisible = item.IsVisible || item.Column.IsImmutable;
                return item.Column;
            }).ToList();
        }

        private FrameworkElement BuildContent()
        {
            var root = new Grid
            {
                MinWidth = 420,
                MaxHeight = 560,
                RowSpacing = 8
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            root.Children.Add(new TextBlock
            {
                Text = "Включите нужные столбцы и задайте порядок кнопками вверх/вниз.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var scroller = new ScrollViewer
            {
                Content = _itemsHost,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollMode = ScrollMode.Enabled
            };
            Grid.SetRow(scroller, 1);
            root.Children.Add(scroller);
            return root;
        }

        private void RebuildItems()
        {
            _itemsHost.Children.Clear();

            for (var index = 0; index < _items.Count; index++)
            {
                _itemsHost.Children.Add(CreateItemRow(_items[index], index));
            }
        }

        private FrameworkElement CreateItemRow(ColumnLayoutItem item, int index)
        {
            var grid = new Grid
            {
                ColumnSpacing = 8,
                Padding = new Thickness(4, 2, 4, 2)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var checkBox = new CheckBox
            {
                Content = item.Column.Header,
                IsChecked = item.IsVisible || item.Column.IsImmutable,
                IsEnabled = !item.Column.IsImmutable,
                VerticalAlignment = VerticalAlignment.Center
            };
            checkBox.Checked += (_, _) => item.IsVisible = true;
            checkBox.Unchecked += (_, _) => item.IsVisible = false;
            grid.Children.Add(checkBox);

            var upButton = CreateMoveButton("\uE70E", "Выше", index > 0, () => MoveItem(index, -1));
            Grid.SetColumn(upButton, 1);
            grid.Children.Add(upButton);

            var downButton = CreateMoveButton("\uE70D", "Ниже", index < _items.Count - 1, () => MoveItem(index, 1));
            Grid.SetColumn(downButton, 2);
            grid.Children.Add(downButton);

            return grid;
        }

        private static Button CreateMoveButton(string glyph, string tooltip, bool isEnabled, Action move)
        {
            var button = new Button
            {
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                Content = glyph,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                IsEnabled = isEnabled
            };
            ToolTipService.SetToolTip(button, tooltip);
            button.Click += (_, _) => move();
            return button;
        }

        private void MoveItem(int index, int offset)
        {
            var targetIndex = index + offset;
            if (targetIndex < 0 || targetIndex >= _items.Count)
            {
                return;
            }

            (_items[index], _items[targetIndex]) = (_items[targetIndex], _items[index]);
            RebuildItems();
        }

        private sealed class ColumnLayoutItem
        {
            public ColumnLayoutItem(CbsTableColumnDefinition column)
            {
                Column = column;
                IsVisible = column.IsVisible;
            }

            public CbsTableColumnDefinition Column { get; }

            public bool IsVisible { get; set; }
        }
    }
}
