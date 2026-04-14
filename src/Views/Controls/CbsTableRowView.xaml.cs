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
                new PropertyMetadata(40d, OnStateChanged));

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

        public void Configure(ReferenceDataRow? row, IReadOnlyList<CbsTableColumnDefinition> columns, double rowHeight)
        {
            Row = row;
            Columns = columns;
            RowHeight = rowHeight;
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
            UpdateCellContent();
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
                RowGrid.ColumnDefinitions.Add(CreateColumnDefinition(Columns[index]));

                var cellHost = new Grid();
                var textCell = CreateTextCell();
                var skeletonCell = CreateSkeletonCell();

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
                        Background = (Brush)Application.Current.Resources["ShellPanelBorderBrush"]
                    };
                    Grid.SetColumn(splitter, index);
                    RowGrid.Children.Add(splitter);
                }
            }
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

        private static TextBlock CreateTextCell()
        {
            return new TextBlock
            {
                Margin = new Thickness(8, 2, 8, 2),
                Foreground = (Brush)Application.Current.Resources["ShellPrimaryTextBrush"],
                Text = string.Empty,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Border CreateSkeletonCell()
        {
            return new Border
            {
                Margin = new Thickness(8, 7, 8, 7),
                Height = 10,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromArgb(48, 92, 129, 173))
            };
        }

        private static ColumnDefinition CreateColumnDefinition(CbsTableColumnDefinition column)
        {
            if (double.TryParse(column.Width, out var fixedWidth))
            {
                return new ColumnDefinition { Width = new GridLength(fixedWidth) };
            }

            if (string.Equals(column.Width, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return new ColumnDefinition { Width = GridLength.Auto };
            }

            if (string.Equals(column.FieldKey, "id", StringComparison.OrdinalIgnoreCase))
            {
                return new ColumnDefinition { Width = new GridLength(88) };
            }

            return new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        }

    }
}


