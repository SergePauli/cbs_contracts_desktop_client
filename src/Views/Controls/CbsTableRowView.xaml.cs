using System;
using System.Collections.Generic;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CbsContractsDesktopClient.Views.Controls
{
    public sealed partial class CbsTableRowView : UserControl
    {
        private IReadOnlyList<CbsTableColumnDefinition> _currentColumns = Array.Empty<CbsTableColumnDefinition>();
        private readonly List<TextBlock> _textCells = [];
        private readonly List<Border> _skeletonCells = [];

        public static readonly DependencyProperty RowProperty =
            DependencyProperty.Register(
                nameof(Row),
                typeof(ReferenceDataRow),
                typeof(CbsTableRowView),
                new PropertyMetadata(null, OnStateChanged));

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(
                nameof(Columns),
                typeof(IReadOnlyList<CbsTableColumnDefinition>),
                typeof(CbsTableRowView),
                new PropertyMetadata(Array.Empty<CbsTableColumnDefinition>(), OnStateChanged));

        public static readonly DependencyProperty RowHeightProperty =
            DependencyProperty.Register(
                nameof(RowHeight),
                typeof(double),
                typeof(CbsTableRowView),
                new PropertyMetadata(22d, OnStateChanged));

        public static readonly DependencyProperty DensityProperty =
            DependencyProperty.Register(
                nameof(Density),
                typeof(CbsTableDensity),
                typeof(CbsTableRowView),
                new PropertyMetadata(CbsTableDensity.Compact, OnStateChanged));

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(
                nameof(IsSelected),
                typeof(bool),
                typeof(CbsTableRowView),
                new PropertyMetadata(false, OnStateChanged));

        public static readonly DependencyProperty IsHoveredProperty =
            DependencyProperty.Register(
                nameof(IsHovered),
                typeof(bool),
                typeof(CbsTableRowView),
                new PropertyMetadata(false, OnStateChanged));

        public static readonly DependencyProperty IsPressedProperty =
            DependencyProperty.Register(
                nameof(IsPressed),
                typeof(bool),
                typeof(CbsTableRowView),
                new PropertyMetadata(false, OnStateChanged));

        public CbsTableRowView()
        {
            InitializeComponent();
        }

        public ReferenceDataRow? Row
        {
            get => (ReferenceDataRow?)GetValue(RowProperty);
            set => SetValue(RowProperty, value);
        }

        public IReadOnlyList<CbsTableColumnDefinition> Columns
        {
            get => (IReadOnlyList<CbsTableColumnDefinition>)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public double RowHeight
        {
            get => (double)GetValue(RowHeightProperty);
            set => SetValue(RowHeightProperty, value);
        }

        public CbsTableDensity Density
        {
            get => (CbsTableDensity)GetValue(DensityProperty);
            set => SetValue(DensityProperty, value);
        }

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public bool IsHovered
        {
            get => (bool)GetValue(IsHoveredProperty);
            set => SetValue(IsHoveredProperty, value);
        }

        public bool IsPressed
        {
            get => (bool)GetValue(IsPressedProperty);
            set => SetValue(IsPressedProperty, value);
        }

        public void Configure(ReferenceDataRow? row, IReadOnlyList<CbsTableColumnDefinition> columns, double rowHeight)
        {
            Row = row;
            Columns = columns;
            RowHeight = rowHeight;
            Density = ResolveDensity(rowHeight);
            RefreshRow();
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((CbsTableRowView)d).RefreshRow();
        }

        private void RefreshRow()
        {
            MinHeight = RowHeight;

            if (Row is null || Columns.Count == 0)
            {
                return;
            }

            EnsureStructure();
            ApplyDensity();
            UpdateCellContent();
            UpdateVisualState();
        }

        private void EnsureStructure()
        {
            if (ReferenceEquals(_currentColumns, Columns)
                && _textCells.Count == Columns.Count
                && _skeletonCells.Count == Columns.Count)
            {
                return;
            }

            _currentColumns = Columns;
            _textCells.Clear();
            _skeletonCells.Clear();
            RowGrid.Children.Clear();
            RowGrid.ColumnDefinitions.Clear();

            for (var index = 0; index < Columns.Count; index++)
            {
                RowGrid.ColumnDefinitions.Add(CreateDataColumnDefinition(Columns[index]));

                var cellHost = new Grid();
                var textCell = CreateTextCell();
                var skeletonCell = CreateSkeletonCell();
                ApplyColumnAlignment(textCell, Columns[index]);

                _textCells.Add(textCell);
                _skeletonCells.Add(skeletonCell);

                cellHost.Children.Add(textCell);
                cellHost.Children.Add(skeletonCell);

                Grid.SetColumn(cellHost, index);
                RowGrid.Children.Add(cellHost);

                if (index < Columns.Count - 1)
                {
                    var splitter = new Border
                    {
                        Width = 1,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Background = (Brush)Application.Current.Resources["ShellTableGridLineBrush"]
                    };
                    Grid.SetColumn(splitter, index);
                    RowGrid.Children.Add(splitter);
                }
            }

            RowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var fillerCell = new Border
            {
                Background = (Brush)Application.Current.Resources["ShellTableRowBackgroundBrush"],
                BorderBrush = (Brush)Application.Current.Resources["ShellTableGridLineBrush"],
                BorderThickness = new Thickness(1, 0, 0, 0)
            };
            Grid.SetColumn(fillerCell, Columns.Count);
            RowGrid.Children.Add(fillerCell);

            ApplyDensity();
        }

        private void UpdateCellContent()
        {
            var isPlaceholder = Row?.IsPlaceholder == true;

            for (var index = 0; index < Columns.Count; index++)
            {
                _textCells[index].Visibility = isPlaceholder ? Visibility.Collapsed : Visibility.Visible;
                _skeletonCells[index].Visibility = isPlaceholder ? Visibility.Visible : Visibility.Collapsed;

                if (!isPlaceholder)
                {
                    _textCells[index].Text = Row?.GetValue(Columns[index].FieldKey)?.ToString() ?? string.Empty;
                }
            }
        }

        private void UpdateVisualState()
        {
            if (RowBorder is null || SelectionAccent is null)
            {
                return;
            }

            var isPlaceholder = Row?.IsPlaceholder == true;
            var backgroundKey = "ShellTableRowBackgroundBrush";

            if (!isPlaceholder)
            {
                if (IsPressed)
                {
                    backgroundKey = "ShellTableRowPressedBackgroundBrush";
                }
                else if (IsSelected)
                {
                    backgroundKey = "ShellTableRowSelectedBackgroundBrush";
                }
                else if (IsHovered)
                {
                    backgroundKey = "ShellTableRowHoverBackgroundBrush";
                }
            }

            RowBorder.Background = (Brush)Application.Current.Resources[backgroundKey];
            RowBorder.BorderBrush = (Brush)Application.Current.Resources["ShellTableGridLineBrush"];
            RowBorder.BorderThickness = new Thickness(0, 0, 0, 1);
            SelectionAccent.Visibility = !isPlaceholder && IsSelected ? Visibility.Visible : Visibility.Collapsed;
        }

        private static TextBlock CreateTextCell()
        {
            return new TextBlock
            {
                Margin = new Thickness(6, 0, 6, 0),
                Foreground = (Brush)Application.Current.Resources["ShellPrimaryTextBrush"],
                FontSize = 12,
                Text = string.Empty,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static void ApplyColumnAlignment(TextBlock textCell, CbsTableColumnDefinition column)
        {
            textCell.TextAlignment = column.Alignment switch
            {
                CbsTableColumnAlignment.Right => TextAlignment.Right,
                CbsTableColumnAlignment.Center => TextAlignment.Center,
                _ => TextAlignment.Left
            };
        }

        private static Border CreateSkeletonCell()
        {
            return new Border
            {
                Margin = new Thickness(6, 6, 6, 6),
                Height = 8,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromArgb(48, 92, 129, 173))
            };
        }

        private static CbsTableDensity ResolveDensity(double rowHeight)
        {
            if (rowHeight <= 22)
            {
                return CbsTableDensity.Compact;
            }

            if (rowHeight <= 28)
            {
                return CbsTableDensity.Standard;
            }

            return CbsTableDensity.Comfortable;
        }

        private void ApplyDensity()
        {
            var metrics = Density switch
            {
                CbsTableDensity.Comfortable => (FontSize: 14d, HorizontalPadding: 10d, SkeletonVerticalMargin: 9d, SkeletonHeight: 10d),
                CbsTableDensity.Standard => (FontSize: 13d, HorizontalPadding: 8d, SkeletonVerticalMargin: 7d, SkeletonHeight: 9d),
                _ => (FontSize: 12d, HorizontalPadding: 6d, SkeletonVerticalMargin: 6d, SkeletonHeight: 8d)
            };

            foreach (var textCell in _textCells)
            {
                textCell.FontSize = metrics.FontSize;
                textCell.Margin = new Thickness(metrics.HorizontalPadding, 0, metrics.HorizontalPadding, 0);
            }

            foreach (var skeletonCell in _skeletonCells)
            {
                skeletonCell.Margin = new Thickness(
                    metrics.HorizontalPadding,
                    metrics.SkeletonVerticalMargin,
                    metrics.HorizontalPadding,
                    metrics.SkeletonVerticalMargin);
                skeletonCell.Height = metrics.SkeletonHeight;
            }
        }

        private static ColumnDefinition CreateDataColumnDefinition(CbsTableColumnDefinition column)
        {
            var width = column.EffectiveWidth;
            if (TryParseWidth(width, out var fixedWidth))
            {
                return new ColumnDefinition { Width = new GridLength(fixedWidth) };
            }

            if (string.Equals(width, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return new ColumnDefinition { Width = GridLength.Auto };
            }

            return new ColumnDefinition { Width = new GridLength(192) };
        }

        private static bool TryParseWidth(string? width, out double pixels)
        {
            pixels = 0;
            if (string.IsNullOrWhiteSpace(width))
            {
                return false;
            }

            if (double.TryParse(width, out pixels))
            {
                return true;
            }

            if (width.EndsWith("rem", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(width[..^3], out var rem))
            {
                pixels = rem * 16;
                return true;
            }

            if (width.EndsWith("px", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(width[..^2], out var px))
            {
                pixels = px;
                return true;
            }

            return false;
        }

        public void SetColumnWidth(int columnIndex, double width)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count)
            {
                return;
            }

            if (columnIndex >= RowGrid.ColumnDefinitions.Count)
            {
                return;
            }

            RowGrid.ColumnDefinitions[columnIndex].Width = new GridLength(width);
        }

    }
}


