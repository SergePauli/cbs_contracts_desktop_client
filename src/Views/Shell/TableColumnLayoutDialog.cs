using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
            PrimaryButtonText = string.Empty;
            CloseButtonText = string.Empty;
            DefaultButton = ContentDialogButton.None;
            Content = BuildContent();
            RebuildItems();
            DialogChrome.Apply(this);
        }

        public bool WasApplied { get; private set; }

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
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

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

            var footer = BuildFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            return root;
        }

        private FrameworkElement BuildFooter()
        {
            var applyButton = BuildFooterButton("Применить", "\uE73E", true, () =>
            {
                WasApplied = true;
                Hide();
            });
            var cancelButton = BuildFooterButton("Отмена", "\uE711", false, Hide);

            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 4,
                Children =
                {
                    applyButton,
                    cancelButton
                }
            };
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
                FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                IsEnabled = isEnabled
            };
            ToolTipService.SetToolTip(button, tooltip);
            button.Click += (_, _) => move();
            return button;
        }

        private static Button BuildFooterButton(string text, string glyph, bool isPrimary, Action click)
        {
            var foreground = Application.Current.Resources[
                    isPrimary ? "ShellTableRowSelectedBorderBrush" : "ShellSecondaryTextBrush"] as Brush
                ?? new SolidColorBrush(isPrimary ? Microsoft.UI.Colors.SeaGreen : Microsoft.UI.Colors.DimGray);
            var button = new Button
            {
                Width = 112,
                MinHeight = 28,
                Padding = new Thickness(8, 2, 8, 2),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Foreground = foreground,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new FontIcon
                        {
                            Glyph = glyph,
                            FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                            FontSize = 12,
                            Foreground = foreground
                        },
                        new TextBlock
                        {
                            Text = text,
                            Foreground = foreground,
                            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 232, 235, 239));
            button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 225, 231));
            button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 232, 235, 239));
            button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 225, 231));
            button.Click += (_, _) => click();
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
