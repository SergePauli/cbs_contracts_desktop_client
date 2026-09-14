using System.Globalization;
using CbsContractsDesktopClient.Models.Export;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.Table;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CbsContractsDesktopClient.Services.Export;

internal sealed class ExcelTableWriter : IDisposable
{
    private readonly SpreadsheetDocument _document;
    private readonly OpenXmlWriter _writer;
    private readonly WorkbookStylesPart _stylesPart;
    private readonly TableExportRequest _request;
    private readonly TablePresentationContext _presentation;
    private readonly Fonts _fonts = new();
    private readonly Fills _fills = new(new Fill(new PatternFill { PatternType = PatternValues.None }),
        new Fill(new PatternFill { PatternType = PatternValues.Gray125 }));
    private readonly CellFormats _formats = new(new CellFormat());
    private readonly NumberingFormats _numberFormats = new();
    private readonly Dictionary<(string, uint, uint, bool, CbsTableColumnAlignment, double), uint> _styleIds = [];
    private uint _rowIndex = 1;

    public ExcelTableWriter(string path, TableExportRequest request, TablePresentationContext presentation,
        IReadOnlyList<double> columnWidths)
    {
        _request = request;
        _presentation = presentation;
        _document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbook = _document.AddWorkbookPart();
        workbook.Workbook = new Workbook();
        _stylesPart = workbook.AddNewPart<WorkbookStylesPart>();
        var sheet = workbook.AddNewPart<WorksheetPart>();
        _document.PackageProperties.Title = request.Title;
        workbook.Workbook.Append(new Sheets(new Sheet { Name = "Данные", SheetId = 1,
            Id = workbook.GetIdOfPart(sheet) }));
        _writer = OpenXmlWriter.Create(sheet);
        _writer.WriteStartElement(new Worksheet());
        _writer.WriteElement(new SheetViews(new SheetView(new Pane
        {
            VerticalSplit = 1, TopLeftCell = "A2", ActivePane = PaneValues.BottomLeft, State = PaneStateValues.Frozen
        }) { WorkbookViewId = 0 }));
        var columns = new Columns();
        for (var i = 0; i < request.Columns.Count; i++)
            columns.Append(new Column { Min = (uint)i + 1, Max = (uint)i + 1,
                Width = Math.Clamp((columnWidths[i] - 5) / 7, 1, 255), CustomWidth = true });
        _writer.WriteElement(columns);
        _writer.WriteStartElement(new SheetData());
        _writer.WriteStartElement(new Row { RowIndex = _rowIndex });
        foreach (var column in request.Columns)
            WriteCell(new(column.Header, column.Header, "General", presentation.Colors["ShellTableHeaderTextBrush"],
                presentation.Colors["ShellTableHeaderBackgroundBrush"], true, column.Alignment,
                presentation.FontSize switch { 14 => 13, 13 => 12.5, _ => 12 }));
        _writer.WriteEndElement();
    }

    public void WriteRow(TableDataRow row)
    {
        _writer.WriteStartElement(new Row { RowIndex = ++_rowIndex });
        foreach (var column in _request.Columns)
        {
            try
            {
                WriteCell(TableCellPresentationBuilder.Build(column, row, _request.RowStyleKey, _presentation));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Экспорт: строка {_rowIndex}, столбец «{column.Header}» ({column.FieldKey}).", ex);
            }
        }
        _writer.WriteEndElement();
    }

    private void WriteCell(TableCellPresentation presentation)
    {
        var cell = new Cell { StyleIndex = GetStyle(presentation) };
        switch (presentation.Value)
        {
            case null:
                break;
            case bool:
                SetText(cell, presentation.Text);
                break;
            case DateTime date:
                cell.DataType = CellValues.Number;
                cell.CellValue = new CellValue(date.ToOADate().ToString("R", CultureInfo.InvariantCulture));
                break;
            case byte or short or int or long or float or double or decimal:
                var number = Convert.ToString(presentation.Value, CultureInfo.InvariantCulture)!;
                // Excel preserves only 15 significant decimal digits. Store larger values as literal text.
                if (number.Count(char.IsDigit) > 15)
                    SetText(cell, number);
                else
                {
                    cell.DataType = CellValues.Number;
                    cell.CellValue = new CellValue(number);
                }
                break;
            default:
                SetText(cell, presentation.Value.ToString()!);
                break;
        }
        _writer.WriteElement(cell);
    }

    private static void SetText(Cell cell, string value)
    {
        if (value.Length > 32767)
            throw new InvalidOperationException("Текст превышает ограничение Excel: 32 767 символов в ячейке.");
        cell.DataType = CellValues.InlineString;
        cell.InlineString = new InlineString(new Text(value) { Space = SpaceProcessingModeValues.Preserve });
    }

    private uint GetStyle(TableCellPresentation cell)
    {
        var key = (cell.NumberFormat, cell.Foreground, cell.Background, cell.Bold, cell.Alignment, cell.FontSize);
        if (_styleIds.TryGetValue(key, out var id)) return id;
        var fontId = (uint)_fonts.ChildElements.Count;
        var font = new Font(new FontSize { Val = cell.FontSize * 0.75 }, new Color { Rgb = cell.Foreground.ToString("X8") },
            new FontName { Val = "Segoe UI" });
        if (cell.Bold) font.Bold = new Bold();
        _fonts.Append(font);
        var fillId = (uint)_fills.ChildElements.Count;
        _fills.Append(new Fill(new PatternFill(new ForegroundColor { Rgb = cell.Background.ToString("X8") },
            new BackgroundColor { Indexed = 64 }) { PatternType = PatternValues.Solid }));
        uint numberId = 0;
        if (cell.NumberFormat != "General")
        {
            numberId = 164 + (uint)_numberFormats.ChildElements.Count;
            _numberFormats.Append(new NumberingFormat { NumberFormatId = numberId, FormatCode = cell.NumberFormat });
        }
        id = (uint)_formats.ChildElements.Count;
        _formats.Append(new CellFormat(new Alignment
        {
            Horizontal = cell.Alignment switch
            {
                CbsTableColumnAlignment.Center => HorizontalAlignmentValues.Center,
                CbsTableColumnAlignment.Right => HorizontalAlignmentValues.Right,
                _ => HorizontalAlignmentValues.Left
            },
            Vertical = VerticalAlignmentValues.Center, WrapText = true
        }) { FontId = fontId, FillId = fillId, BorderId = 0, NumberFormatId = numberId,
            FormatId = 0, ApplyFont = true, ApplyFill = true, ApplyBorder = true,
            ApplyNumberFormat = true, ApplyAlignment = true });
        _styleIds.Add(key, id);
        return id;
    }

    public void Complete()
    {
        _writer.WriteEndElement();
        var lastColumn = string.Empty;
        for (var n = _request.Columns.Count; n > 0; n = (n - 1) / 26)
            lastColumn = (char)('A' + (n - 1) % 26) + lastColumn;
        _writer.WriteElement(new AutoFilter { Reference = $"A1:{lastColumn}{_rowIndex}" });
        _writer.WriteEndElement();
        _writer.Close();
        var gridColor = _presentation.Colors["ShellTableGridLineBrush"].ToString("X8");
        var borders = new Borders(new Border(
            new LeftBorder(new Color { Rgb = gridColor }) { Style = BorderStyleValues.Thin },
            new RightBorder(new Color { Rgb = gridColor }) { Style = BorderStyleValues.Thin },
            new TopBorder(new Color { Rgb = gridColor }) { Style = BorderStyleValues.Thin },
            new BottomBorder(new Color { Rgb = gridColor }) { Style = BorderStyleValues.Thin }, new DiagonalBorder()));
        _stylesPart.Stylesheet = new Stylesheet(_numberFormats, _fonts, _fills, borders,
            new CellStyleFormats(new CellFormat()), _formats,
            new CellStyles(new CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }));
        _stylesPart.Stylesheet.Save();
        _document.WorkbookPart!.Workbook.Save();
    }

    public void Dispose()
    {
        _writer.Dispose();
        _document.Dispose();
    }
}
