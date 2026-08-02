using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Orders;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Dialogs;
using Windows.Foundation;
using Windows.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.Views.Controls
{
    public sealed partial class CbsTableRowView : UserControl
    {
        private IReadOnlyList<CbsTableColumnDefinition> _currentColumns = Array.Empty<CbsTableColumnDefinition>();
        private readonly List<TextBlock> _textCells = [];
        private readonly List<Border> _skeletonCells = [];
        private readonly List<Border> _badgeCells = [];
        private readonly List<TextBlock> _badgeTexts = [];
        private readonly List<Border> _cellSelectionBackgrounds = [];
        private bool _isConfiguring;
        private int _selectedColumnStart = -1;
        private int _selectedColumnEnd = -1;

        public static readonly DependencyProperty RowProperty =
            DependencyProperty.Register(
                nameof(Row),
                typeof(TableDataRow),
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

        public static readonly DependencyProperty RowStyleKeyProperty =
            DependencyProperty.Register(
                nameof(RowStyleKey),
                typeof(CbsTableRowStyleKey),
                typeof(CbsTableRowView),
                new PropertyMetadata(CbsTableRowStyleKey.None, OnStateChanged));

        public static readonly DependencyProperty ShowStageCostFractionProperty =
            DependencyProperty.Register(
                nameof(ShowStageCostFraction),
                typeof(bool),
                typeof(CbsTableRowView),
                new PropertyMetadata(false, OnStateChanged));

        public CbsTableRowView()
        {
            InitializeComponent();
        }

        public TableDataRow? Row
        {
            get => (TableDataRow?)GetValue(RowProperty);
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

        public CbsTableRowStyleKey RowStyleKey
        {
            get => (CbsTableRowStyleKey)GetValue(RowStyleKeyProperty);
            set => SetValue(RowStyleKeyProperty, value);
        }

        public bool ShowStageCostFraction
        {
            get => (bool)GetValue(ShowStageCostFractionProperty);
            set => SetValue(ShowStageCostFractionProperty, value);
        }

        public void Configure(
            TableDataRow? row,
            IReadOnlyList<CbsTableColumnDefinition> columns,
            double rowHeight,
            CbsTableRowStyleKey rowStyleKey,
            bool showStageCostFraction)
        {
            _isConfiguring = true;
            try
            {
                Row = row;
                Columns = columns;
                RowHeight = rowHeight;
                RowStyleKey = rowStyleKey;
                ShowStageCostFraction = showStageCostFraction;
                Density = ResolveDensity(rowHeight);
            }
            finally
            {
                _isConfiguring = false;
            }

            RefreshRow();
        }

        public void SetCellSelection(int columnStart, int columnEnd)
        {
            _selectedColumnStart = columnStart;
            _selectedColumnEnd = columnEnd;
            UpdateCellSelection();
        }

        public int GetColumnIndex(Point position)
        {
            var offset = 0d;
            for (var index = 0; index < Columns.Count; index++)
            {
                offset += RowGrid.ColumnDefinitions[index].ActualWidth;
                if (position.X < offset)
                {
                    return index;
                }
            }

            return -1;
        }

        internal static string GetCellText(
            CbsTableColumnDefinition column,
            TableDataRow row,
            bool showStageCostFraction)
        {
            var valueKey = column.DisplayField ?? column.ApiField ?? column.FieldKey;
            var value = row.GetValue(valueKey);
            if (string.Equals(column.BodyTemplateKey, "StageOrderSeverity", StringComparison.OrdinalIgnoreCase))
            {
                var severity = StageOrderSeverityText.Parse(checked((int)(TryGetLong(value)
                    ?? throw new InvalidOperationException("StageOrderSeverity должен содержать целочисленное значение."))));
                return StageOrderSeverityText.GetLabel(severity);
            }

            if (!string.IsNullOrWhiteSpace(column.BodyTemplateKey))
            {
                var formatted = FormatTemplateValue(column.BodyTemplateKey, row, value, showStageCostFraction);
                if (formatted is not null)
                {
                    return formatted;
                }
            }

            if (column.BodyMode == CbsTableBodyMode.BooleanIcon)
            {
                return value switch
                {
                    true => "\u2713",
                    false => string.Empty,
                    null => "?",
                    string textValue when bool.TryParse(textValue, out var parsedBoolean)
                        => parsedBoolean ? "\u2713" : string.Empty,
                    _ => "?"
                };
            }

            return FormatCellValue(column, value);
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var rowView = (CbsTableRowView)d;
            if (rowView._isConfiguring)
            {
                return;
            }

            rowView.RefreshRow();
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
                && _skeletonCells.Count == Columns.Count
                && _badgeCells.Count == Columns.Count
                && _badgeTexts.Count == Columns.Count)
            {
                return;
            }

            _currentColumns = Columns;
            _textCells.Clear();
            _skeletonCells.Clear();
            _badgeCells.Clear();
            _badgeTexts.Clear();
            _cellSelectionBackgrounds.Clear();
            RowGrid.Children.Clear();
            RowGrid.ColumnDefinitions.Clear();

            for (var index = 0; index < Columns.Count; index++)
            {
                RowGrid.ColumnDefinitions.Add(CreateDataColumnDefinition(Columns[index]));

                var cellHost = new Grid();
                var selectionBackground = new Border
                {
                    Background = (Brush)Application.Current.Resources["ShellTableRowSelectedBackgroundBrush"],
                    Visibility = Visibility.Collapsed
                };
                var textCell = CreateTextCell();
                var skeletonCell = CreateSkeletonCell();
                var badgeText = CreateBadgeText();
                var badgeCell = CreateBadgeCell(badgeText);
                ApplyColumnAlignment(textCell, Columns[index]);

                _textCells.Add(textCell);
                _skeletonCells.Add(skeletonCell);
                _badgeCells.Add(badgeCell);
                _badgeTexts.Add(badgeText);
                _cellSelectionBackgrounds.Add(selectionBackground);

                cellHost.Children.Add(selectionBackground);
                cellHost.Children.Add(textCell);
                cellHost.Children.Add(skeletonCell);
                cellHost.Children.Add(badgeCell);

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
            UpdateCellSelection();
        }

        private void UpdateCellSelection()
        {
            for (var index = 0; index < _cellSelectionBackgrounds.Count; index++)
            {
                _cellSelectionBackgrounds[index].Visibility = index >= _selectedColumnStart && index <= _selectedColumnEnd
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UpdateCellContent()
        {
            var isPlaceholder = Row?.IsPlaceholder == true;

            for (var index = 0; index < Columns.Count; index++)
            {
                _textCells[index].Visibility = isPlaceholder ? Visibility.Collapsed : Visibility.Visible;
                _skeletonCells[index].Visibility = isPlaceholder ? Visibility.Visible : Visibility.Collapsed;
                _badgeCells[index].Visibility = Visibility.Collapsed;

                if (isPlaceholder)
                {
                    _textCells[index].Text = string.Empty;
                }
                else
                {
                    var valueKey = Columns[index].DisplayField ?? Columns[index].ApiField ?? Columns[index].FieldKey;
                    var value = Row?.GetValue(valueKey);
                    if (IsBadgeTemplate(Columns[index]))
                    {
                        ApplyBadgeContent(Columns[index], _badgeCells[index], _badgeTexts[index], Row, value);
                        _textCells[index].Text = string.Empty;
                        _textCells[index].Visibility = Visibility.Collapsed;
                        _badgeCells[index].Visibility = string.IsNullOrWhiteSpace(_badgeTexts[index].Text)
                            ? Visibility.Collapsed
                            : Visibility.Visible;
                    }
                    else
                    {
                        ApplyBodyContent(_textCells[index], Columns[index], Row, value, ShowStageCostFraction);
                    }
                }
            }
        }

        private static void ApplyBodyContent(
            TextBlock textCell,
            CbsTableColumnDefinition column,
            TableDataRow? row,
            object? value,
            bool showStageCostFraction)
        {
            if (!string.IsNullOrWhiteSpace(column.BodyTemplateKey))
            {
                var formatted = FormatTemplateValue(column.BodyTemplateKey, row, value, showStageCostFraction);
                if (formatted is not null)
                {
                    textCell.Text = formatted;
                    textCell.Foreground = ResolveBrush("ShellPrimaryTextBrush", "ShellPrimaryTextBrush");
                    return;
                }
            }

            if (column.BodyMode == CbsTableBodyMode.BooleanIcon)
            {
                var (text, foregroundKey) = value switch
                {
                    true => ("\u2713", "SystemFillColorSuccessBrush"),
                    false => (string.Empty, "ShellPrimaryTextBrush"),
                    null => ("?", "ShellSecondaryTextBrush"),
                    string textValue when bool.TryParse(textValue, out var parsedBoolean)
                        => parsedBoolean ? ("\u2713", "SystemFillColorSuccessBrush") : (string.Empty, "ShellPrimaryTextBrush"),
                    _ => ("?", "ShellSecondaryTextBrush")
                };

                textCell.Text = text;
                textCell.Foreground = ResolveBrush(foregroundKey, "ShellPrimaryTextBrush");
                return;
            }

            textCell.Text = FormatCellValue(column, value);
            textCell.Foreground = ResolveBrush("ShellPrimaryTextBrush", "ShellPrimaryTextBrush");
        }

        private static string FormatCellValue(CbsTableColumnDefinition column, object? value)
        {
            if (column.Filter.Mode == DataFilterMode.Date)
            {
                return FormatDateValue(value);
            }

            return value switch
            {
                null => string.Empty,
                DateTime dateTime => dateTime.ToLocalTime().ToString(CultureInfo.CurrentCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.LocalDateTime.ToString(CultureInfo.CurrentCulture),
                string text when TryFormatDateTimeText(text, out var formattedDateTime) => formattedDateTime,
                _ => value.ToString() ?? string.Empty
            };
        }

        private static string? FormatTemplateValue(
            string templateKey,
            TableDataRow? row,
            object? value,
            bool showStageCostFraction)
        {
            if (row is null)
            {
                return null;
            }

            return templateKey switch
            {
                "StageRegion" => FirstText(
                    row.GetValue("contract.contragent.region.name"),
                    row.GetValue("contract.region.name"),
                    value),
                "StageRegister" => FormatStageRegister(row),
                "StageCost" => FormatStageCost(value, showStageCostFraction),
                "StageDuration" => FormatStageDuration(row, value),
                "StageSzi" => HasStageTaskKind(row, 10) ? "\u2713" : string.Empty,
                "IsecurityToolKind" => FormatIsecurityToolKind(value),
                "ContractDsp" => FormatContractDsp(row),
                "ContractRegion" => FirstText(
                    row.GetValue("contragent.region.name"),
                    row.GetValue("region.name"),
                    value),
                "ContractCost" => FormatStageCost(value, showStageCostFraction),
                "ContractFunded" => FormatContractFunded(value),
                _ => null
            };
        }

        private static string FormatIsecurityToolKind(object? value)
        {
            var numericValue = value switch
            {
                long longValue => longValue,
                decimal decimalValue when decimal.Truncate(decimalValue) == decimalValue => (long)decimalValue,
                _ => throw new InvalidOperationException("IsecurityTool.kind должен содержать целочисленное значение.")
            };

            return IsecurityToolKindText.GetLabel(IsecurityToolKindText.Parse(numericValue));
        }

        private static string FormatContractDsp(TableDataRow row)
        {
            return $"{FormatPresence(row.GetValue("revision.doc_link"))}{FormatPresence(row.GetValue("revision.scan_link"))}{FormatPresence(row.GetValue("revision.protocol_link"))}";
        }

        private static string FormatPresence(object? value)
        {
            return string.IsNullOrWhiteSpace(value?.ToString()) ? "-" : "+";
        }

        private static string FormatContractFunded(object? value)
        {
            return value switch
            {
                true => "\u2713",
                string text when bool.TryParse(text, out var parsed) && parsed => "\u2713",
                string text when string.Equals(text, "null", StringComparison.OrdinalIgnoreCase) => "\u231B",
                null => "\u231B",
                _ => string.Empty
            };
        }

        private static string FormatStageCost(object? value, bool showFraction)
        {
            var cost = value switch
            {
                decimal decimalValue => decimalValue,
                double doubleValue => Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture),
                float floatValue => Convert.ToDecimal(floatValue, CultureInfo.InvariantCulture),
                long longValue => longValue,
                int intValue => intValue,
                string text when decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantValue) => invariantValue,
                string text when decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var currentValue) => currentValue,
                _ => (decimal?)null
            };

            return cost is decimal amount
                ? amount.ToString(showFraction ? "N2" : "N0", CultureInfo.CurrentCulture)
                : string.Empty;
        }

        private static bool IsBadgeTemplate(CbsTableColumnDefinition column)
        {
            return string.Equals(column.BodyTemplateKey, "StatusBadge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(column.BodyTemplateKey, "StageOrderSeverity", StringComparison.OrdinalIgnoreCase)
                || string.Equals(column.BodyTemplateKey, "OrderDeliveryStatus", StringComparison.OrdinalIgnoreCase)
                || string.Equals(column.BodyTemplateKey, "OrderStatusBadge", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyBadgeContent(CbsTableColumnDefinition column, Border badgeCell, TextBlock badgeText, TableDataRow? row, object? value)
        {
            if (string.Equals(column.BodyTemplateKey, "StageOrderSeverity", StringComparison.OrdinalIgnoreCase))
            {
                ApplyStageOrderSeverityBadgeContent(badgeCell, badgeText, value);
                return;
            }

            if (string.Equals(column.BodyTemplateKey, "OrderDeliveryStatus", StringComparison.OrdinalIgnoreCase))
            {
                ApplyOrderStatusBadgeContent(badgeCell, badgeText, row, value, "order.status.id");
                return;
            }

            if (string.Equals(column.BodyTemplateKey, "OrderStatusBadge", StringComparison.OrdinalIgnoreCase))
            {
                ApplyOrderStatusBadgeContent(badgeCell, badgeText, row, value, "status.id");
                return;
            }

            ApplyStatusBadgeContent(badgeCell, badgeText, row, value);
        }

        private static void ApplyStageOrderSeverityBadgeContent(Border badgeCell, TextBlock badgeText, object? value)
        {
            var rawSeverity = TryGetLong(value);
            if (rawSeverity is null)
            {
                badgeText.Text = string.Empty;
                return;
            }

            var severity = StageOrderSeverityText.Parse(checked((int)rawSeverity.Value));
            var background = severity switch
            {
                StageOrderSeverity.Need => Windows.UI.Color.FromArgb(255, 255, 193, 7),
                StageOrderSeverity.InStock => Windows.UI.Color.FromArgb(255, 0, 176, 80),
                StageOrderSeverity.OnControl => Windows.UI.Color.FromArgb(255, 220, 53, 69),
                StageOrderSeverity.NotApproved => Windows.UI.Color.FromArgb(255, 137, 207, 240),
                StageOrderSeverity.Delivered => Windows.UI.Color.FromArgb(255, 224, 224, 224),
                _ => throw new ArgumentOutOfRangeException(nameof(value))
            };
            badgeText.Text = StageOrderSeverityText.GetLabel(severity);
            badgeText.Foreground = new SolidColorBrush(
                severity is StageOrderSeverity.Need or StageOrderSeverity.NotApproved or StageOrderSeverity.Delivered
                    ? Microsoft.UI.Colors.Black
                    : Microsoft.UI.Colors.White);
            badgeCell.Background = new SolidColorBrush(background);
        }

        private static void ApplyOrderStatusBadgeContent(
            Border badgeCell,
            TextBlock badgeText,
            TableDataRow? row,
            object? value,
            string statusIdField)
        {
            var statusName = value?.ToString();
            if (string.IsNullOrWhiteSpace(statusName))
            {
                badgeText.Text = string.Empty;
                return;
            }

            var statusId = TryGetLong(row?.GetValue(statusIdField));
            var background = statusId switch
            {
                2 => Windows.UI.Color.FromArgb(255, 194, 237, 246),
                3 => Windows.UI.Color.FromArgb(255, 201, 233, 212),
                4 => Windows.UI.Color.FromArgb(255, 255, 235, 156),
                _ => Windows.UI.Color.FromArgb(255, 224, 224, 224)
            };

            badgeText.Text = statusName;
            badgeText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 64, 64, 64));
            badgeCell.Background = new SolidColorBrush(background);
        }

        private static void ApplyStatusBadgeContent(Border badgeCell, TextBlock badgeText, TableDataRow? row, object? value)
        {
            var statusName = FirstText(value, row?.GetValue("status.name"));
            if (string.IsNullOrWhiteSpace(statusName))
            {
                badgeText.Text = string.Empty;
                return;
            }

            var statusId = TryGetLong(row?.GetValue("status.id")) ?? TryGetLong(row?.GetValue("status_id"));
            var colors = StageContractStatusDialogControls.ResolveStatusBadgeColors(statusId);
            badgeText.Text = statusName;
            badgeText.Foreground = new SolidColorBrush(colors.Foreground);
            badgeCell.Background = new SolidColorBrush(colors.Background);
        }

        private static string FormatStageRegister(TableDataRow row)
        {
            var quarter = row.GetValue("registry_quarter")?.ToString();
            var year = row.GetValue("registry_year")?.ToString();
            return string.IsNullOrWhiteSpace(quarter)
                ? string.Empty
                : string.IsNullOrWhiteSpace(year)
                    ? quarter
                    : $"{quarter}.{year}";
        }

        private static string FormatStageDuration(TableDataRow row, object? value)
        {
            var duration = value?.ToString();
            if (string.IsNullOrWhiteSpace(duration))
            {
                return string.Empty;
            }

            return $"{duration}{FormatDeadlineKind(row.GetValue("deadline_kind")?.ToString())}";
        }

        private static string FormatDeadlineKind(string? kind)
        {
            return kind switch
            {
                "calendar_plan" => "KП",
                "calendar_days" => "КД",
                "calendar_prepayment" => "KДП",
                "working_days" => "РД",
                "working_prepayment" => "РДП",
                _ => string.Empty
            };
        }

        private static bool HasStageTaskKind(TableDataRow row, long taskKindId)
        {
            if (!row.Values.TryGetValue("tasks", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var task in tasks.EnumerateArray())
            {
                if (task.ValueKind == JsonValueKind.Object
                    && task.TryGetProperty("task_kind_id", out var taskKind)
                    && taskKind.TryGetInt64(out var id)
                    && id == taskKindId)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FormatDateValue(object? value)
        {
            return value switch
            {
                null => string.Empty,
                DateTime dateTime => dateTime.ToLocalTime().ToString("d", CultureInfo.CurrentCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.LocalDateTime.ToString("d", CultureInfo.CurrentCulture),
                string text when TryFormatDateText(text, out var formattedDate) => formattedDate,
                _ => value.ToString() ?? string.Empty
            };
        }

        private static bool TryFormatDateTimeText(string text, out string formattedValue)
        {
            formattedValue = string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (!text.Contains('-') || (!text.Contains('T') && !text.Contains(':')))
            {
                return false;
            }

            if (DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out var dateTimeOffset))
            {
                formattedValue = dateTimeOffset.LocalDateTime.ToString(CultureInfo.CurrentCulture);
                return true;
            }

            if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                out var dateTime))
            {
                formattedValue = dateTime.ToString(CultureInfo.CurrentCulture);
                return true;
            }

            return false;
        }

        private static bool TryFormatDateText(string text, out string formattedValue)
        {
            formattedValue = string.Empty;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out var dateTimeOffset))
            {
                formattedValue = dateTimeOffset.LocalDateTime.ToString("d", CultureInfo.CurrentCulture);
                return true;
            }

            if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                out var dateTime))
            {
                formattedValue = dateTime.ToString("d", CultureInfo.CurrentCulture);
                return true;
            }

            return false;
        }

        private static Brush ResolveBrush(string resourceKey, string fallbackResourceKey)
        {
            if (Application.Current.Resources.TryGetValue(resourceKey, out var resource)
                && resource is Brush brush)
            {
                return brush;
            }

            return (Brush)Application.Current.Resources[fallbackResourceKey];
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
            ApplyConditionalRowStyle(isPlaceholder);
        }

        private void ApplyConditionalRowStyle(bool isPlaceholder)
        {
            var foregroundKey = "ShellPrimaryTextBrush";
            var fontWeight = FontWeights.Normal;

            if (!isPlaceholder
                && RowStyleKey is CbsTableRowStyleKey.StageDeadline or CbsTableRowStyleKey.ContractDeadline
                && Row is not null)
            {
                var style = RowStyleKey == CbsTableRowStyleKey.ContractDeadline
                    ? ResolveContractDeadlineStyle(Row)
                    : ResolveStageDeadlineStyle(Row);
                if (!string.IsNullOrWhiteSpace(style.BackgroundBrushKey) && !IsSelected && !IsHovered && !IsPressed)
                {
                    RowBorder.Background = ResolveBrush(style.BackgroundBrushKey, "ShellTableRowBackgroundBrush");
                }

                if (!string.IsNullOrWhiteSpace(style.ForegroundBrushKey))
                {
                    foregroundKey = style.ForegroundBrushKey;
                }

                fontWeight = style.IsSemibold ? FontWeights.SemiBold : FontWeights.Normal;
            }

            foreach (var textCell in _textCells)
            {
                textCell.FontWeight = fontWeight;
                if (!string.Equals(textCell.Text, "\u2713", StringComparison.Ordinal))
                {
                    textCell.Foreground = ResolveBrush(foregroundKey, "ShellPrimaryTextBrush");
                }
            }
        }

        private static ConditionalRowStyle ResolveStageDeadlineStyle(TableDataRow row)
        {
            var deadline = TryGetDateTime(row.GetValue("deadline_at"));
            var statusId = TryGetLong(row.GetValue("status.id")) ?? TryGetLong(row.GetValue("status_id")) ?? 2;
            var governmental = TryGetBoolean(row.GetValue("contract.governmental"));
            return ResolveDeadlineStyle(deadline, statusId, governmental);
        }

        private static ConditionalRowStyle ResolveContractDeadlineStyle(TableDataRow row)
        {
            var deadline =
                TryGetDateTime(row.GetValue("stage.deadline_at"))
                ?? TryGetDateTime(row.GetValue("expired_at"))
                ?? TryGetDateTime(row.GetValue("deadline_at"))
                ?? TryGetDateTime(row.GetValue("expire_at"));
            var statusId =
                TryGetLong(row.GetValue("stage.status.id"))
                ?? TryGetLong(row.GetValue("stage.status_id"))
                ?? TryGetLong(row.GetValue("status.id"))
                ?? TryGetLong(row.GetValue("status_id"))
                ?? 2;
            var governmental = TryGetBoolean(row.GetValue("governmental"));
            return ResolveDeadlineStyle(deadline, statusId, governmental);
        }

        private static ConditionalRowStyle ResolveDeadlineStyle(DateTimeOffset? deadline, long statusId, bool governmental)
        {
            var isNotDone = statusId is not (5 or 4 or 7 or 6);

            if (deadline is null)
            {
                return governmental
                    ? new ConditionalRowStyle("StageGovernmentForegroundBrush", null, null, true)
                    : ConditionalRowStyle.Empty;
            }

            var now = DateTimeOffset.Now;
            var isDeadline = deadline.Value <= now;
            var isCloseDeadline = deadline.Value.AddDays(-14) <= now;

            if (isNotDone && isDeadline && governmental)
            {
                return new ConditionalRowStyle(
                    "StageGovernmentForegroundBrush",
                    "StageDeadlineAlertBackgroundBrush",
                    "StageDeadlineAlertBorderBrush",
                    true);
            }

            if (isNotDone && isCloseDeadline && !isDeadline)
            {
                return new ConditionalRowStyle(
                    governmental ? "StageGovernmentForegroundBrush" : null,
                    "StageDeadlineWarningBackgroundBrush",
                    "StageDeadlineWarningBorderBrush",
                    governmental);
            }

            if (isNotDone && isDeadline)
            {
                return new ConditionalRowStyle("StageDeadlineTextBrush", null, "StageDeadlineAlertBorderBrush", false);
            }

            return governmental
                ? new ConditionalRowStyle("StageGovernmentForegroundBrush", null, null, true)
                : ConditionalRowStyle.Empty;
        }

        private static DateTimeOffset? TryGetDateTime(object? value)
        {
            return value switch
            {
                DateTimeOffset dateTimeOffset => dateTimeOffset,
                DateTime dateTime => dateTime,
                string text when DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed) => parsed,
                string text when DateTimeOffset.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsed) => parsed,
                _ => null
            };
        }

        private static long? TryGetLong(object? value)
        {
            return value switch
            {
                long longValue => longValue,
                int intValue => intValue,
                decimal decimalValue => (long)decimalValue,
                string text when long.TryParse(text, out var parsed) => parsed,
                _ => null
            };
        }

        private static bool TryGetBoolean(object? value)
        {
            return value switch
            {
                bool booleanValue => booleanValue,
                string text when bool.TryParse(text, out var parsed) => parsed,
                _ => false
            };
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

        private static TextBlock CreateBadgeText()
        {
            return new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Text = string.Empty,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Border CreateBadgeCell(TextBlock badgeText)
        {
            return new Border
            {
                Margin = new Thickness(6, 2, 6, 2),
                Padding = new Thickness(6, 1, 6, 1),
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 120,
                Child = badgeText,
                Visibility = Visibility.Collapsed
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

            foreach (var badgeCell in _badgeCells)
            {
                badgeCell.Margin = new Thickness(metrics.HorizontalPadding, 2, metrics.HorizontalPadding, 2);
            }

            foreach (var badgeText in _badgeTexts)
            {
                badgeText.FontSize = Math.Max(11, metrics.FontSize - 1);
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

    internal sealed record ConditionalRowStyle(
        string? ForegroundBrushKey,
        string? BackgroundBrushKey,
        string? BorderBrushKey,
        bool IsSemibold)
    {
        public static ConditionalRowStyle Empty { get; } = new(null, null, null, false);
    }
}


