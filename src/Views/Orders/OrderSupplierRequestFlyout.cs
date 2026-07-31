using System.Globalization;
using CbsContractsDesktopClient.ViewModels.Workflow;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;

namespace CbsContractsDesktopClient.Views.Orders;

public sealed class OrderSupplierRequestFlyout
{
    private readonly Flyout _flyout;
    private readonly string _clipboardText;

    public OrderSupplierRequestFlyout(IReadOnlyList<OrderSupplierRequestLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        _clipboardText = OrderSupplierRequestFormatter.BuildClipboardText(lines);
        _flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
        };
        _flyout.Content = BuildContent(lines);
    }

    public event EventHandler? Copied;

    public void ShowAt(FrameworkElement anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        _flyout.ShowAt(anchor);
    }

    private FrameworkElement BuildContent(IReadOnlyList<OrderSupplierRequestLine> lines)
    {
        var rows = new StackPanel { Spacing = 4 };
        foreach (var line in lines)
        {
            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = line.Name,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            var amount = new TextBlock
            {
                Text = $"{line.Amount.ToString("0.##", CultureInfo.CurrentCulture)} {line.Unit}",
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(amount, 1);
            grid.Children.Add(name);
            grid.Children.Add(amount);
            rows.Children.Add(grid);
        }

        var copyButton = new Button
        {
            Content = "Копировать",
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(10, 3, 10, 3)
        };
        copyButton.Click += (_, _) =>
        {
            var package = new DataPackage();
            package.SetText(_clipboardText);
            Clipboard.SetContent(package);
            Copied?.Invoke(this, EventArgs.Empty);
        };

        var closeButton = new Button
        {
            Content = "Закрыть",
            Padding = new Thickness(10, 3, 10, 3)
        };
        closeButton.Click += (_, _) => _flyout.Hide();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 6,
            Children =
            {
                copyButton,
                closeButton
            }
        };

        return new StackPanel
        {
            Width = 420,
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "Сводная потребность",
                    FontWeight = FontWeights.SemiBold
                },
                new ScrollViewer
                {
                    MaxHeight = 360,
                    Content = rows
                },
                actions
            }
        };
    }
}
