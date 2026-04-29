using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using CbsContractsDesktopClient.Models.Shell;
using CbsContractsDesktopClient.ViewModels.Shell;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class AuditPanelView : UserControl
    {
        private const double ShiftThreshold = 140;
        private bool _isSubscribed;
        private bool _isAuditShiftPending;

        public AppShellViewModel ViewModel { get; }
        public ReferencesContentViewModel ReferencesViewModel { get; }

        public AuditPanelView()
        {
            ViewModel = App.Services.GetRequiredService<AppShellViewModel>();
            ReferencesViewModel = App.Services.GetRequiredService<ReferencesContentViewModel>();
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            RenderTimeline();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_isSubscribed)
            {
                ViewModel.PropertyChanged += OnViewModelPropertyChanged;
                _isSubscribed = true;
            }

            RenderTimeline();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_isSubscribed)
            {
                ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _isSubscribed = false;
            }
        }

        private async void AuditScrollViewer_ViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_isAuditShiftPending
                || AuditScrollViewer.ExtentHeight <= 0
                || AuditScrollViewer.ViewportHeight <= 0)
            {
                return;
            }

            var distanceToBottom =
                AuditScrollViewer.ExtentHeight
                - AuditScrollViewer.VerticalOffset
                - AuditScrollViewer.ViewportHeight;
            var direction = 0;
            if (AuditScrollViewer.VerticalOffset <= ShiftThreshold)
            {
                direction = -1;
            }
            else if (distanceToBottom <= ShiftThreshold)
            {
                direction = 1;
            }

            if (direction == 0)
            {
                return;
            }

            _isAuditShiftPending = true;
            try
            {
                var shifted = await ReferencesViewModel.ShiftAuditPanelWindowAsync(direction);
                if (shifted)
                {
                    QueueAuditScrollReposition(direction);
                }
            }
            finally
            {
                _isAuditShiftPending = false;
            }
        }

        private void QueueAuditScrollReposition(int direction)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var targetOffset = direction > 0
                    ? ShiftThreshold + 1
                    : Math.Max(0, AuditScrollViewer.ExtentHeight - AuditScrollViewer.ViewportHeight - ShiftThreshold - 1);
                AuditScrollViewer.ChangeView(
                    horizontalOffset: null,
                    verticalOffset: targetOffset,
                    zoomFactor: null,
                    disableAnimation: true);
            });
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppShellViewModel.AuditPanelState))
            {
                RenderTimeline();
            }
        }

        private void RenderTimeline()
        {
            AuditTimelineHost.Children.Clear();

            foreach (var entry in ViewModel.AuditPanelState.Entries)
            {
                AuditTimelineHost.Children.Add(CreateTimelineItem(entry));
            }
        }

        private static UIElement CreateTimelineItem(AuditEntry entry)
        {
            var root = new Grid
            {
                ColumnSpacing = 10,
                Margin = new Thickness(0, 0, 0, 10)
            };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var markerHost = new Grid();
            markerHost.Children.Add(new Ellipse
            {
                Width = 10,
                Height = 10,
                Margin = new Thickness(0, 13, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Fill = GetBrush("ShellAccentBrush")
            });

            var content = new StackPanel { Spacing = 5 };
            content.Children.Add(CreateHeaderLine(entry.Timestamp, entry.Title));
            content.Children.Add(CreateDetailsPanel(entry.Description));

            var card = new Border
            {
                Padding = new Thickness(10, 8, 10, 10),
                Background = GetBrush(entry.BackgroundBrushKey, "ShellAccentPanelBackgroundBrush"),
                BorderBrush = GetBrush("ShellPanelBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = GetCornerRadius("ShellAuditItemCornerRadius"),
                Child = content
            };
            Grid.SetColumn(card, 1);

            root.Children.Add(markerHost);
            root.Children.Add(card);
            return root;
        }

        private static TextBlock CreateHeaderLine(string timestamp, string title)
        {
            var textBlock = new TextBlock
            {
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                TextWrapping = TextWrapping.WrapWholeWords
            };
            textBlock.Inlines.Add(new Run
            {
                Text = string.IsNullOrWhiteSpace(timestamp) ? string.Empty : $"{timestamp}  ",
                Foreground = GetBrush("ShellTertiaryTextBrush"),
                FontSize = 12
            });
            textBlock.Inlines.Add(new Run
            {
                Text = title,
                Foreground = GetBrush("ShellPrimaryTextBrush"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            });

            return textBlock;
        }

        private static StackPanel CreateDetailsPanel(string description)
        {
            var panel = new StackPanel { Spacing = 3 };
            var lines = description
                .Split('\n')
                .Select(static line => line.TrimEnd('\r'))
                .Where(static line => !string.IsNullOrWhiteSpace(line));

            foreach (var line in lines)
            {
                panel.Children.Add(CreateDetailLine(line));
            }

            return panel;
        }

        private static TextBlock CreateDetailLine(string line)
        {
            var textBlock = new TextBlock
            {
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                FontSize = 12,
                Foreground = GetBrush("ShellSecondaryTextBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            };

            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                textBlock.Text = line;
                return textBlock;
            }

            var label = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].TrimStart();

            textBlock.Inlines.Add(new Run
            {
                Text = $"{label}: ",
                Foreground = GetBrush("ShellPrimaryTextBrush"),
                FontWeight = FontWeights.SemiBold
            });
            textBlock.Inlines.Add(new Run
            {
                Text = value,
                Foreground = GetBrush("ShellSecondaryTextBrush")
            });

            return textBlock;
        }

        private static Brush GetBrush(string resourceKey, string fallbackResourceKey = "ShellAccentPanelBackgroundBrush")
        {
            return Application.Current.Resources.TryGetValue(resourceKey, out var resource)
                && resource is Brush brush
                    ? brush
                    : (Brush)Application.Current.Resources[fallbackResourceKey];
        }

        private static CornerRadius GetCornerRadius(string resourceKey)
        {
            return Application.Current.Resources.TryGetValue(resourceKey, out var resource)
                && resource is CornerRadius cornerRadius
                    ? cornerRadius
                    : new CornerRadius(8);
        }
    }
}
