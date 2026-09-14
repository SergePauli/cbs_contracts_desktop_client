using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Export;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Export;
using CbsContractsDesktopClient.Services.Table;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class TableExcelExportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CbsExcelExport", Guid.NewGuid().ToString("N"));
    private string Output => Path.Combine(_directory, "table.xlsx");

    public TableExcelExportTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData("ru-RU")]
    [InlineData("en-US")]
    public async Task Export_PreservesBooleanSymbolsTypesAndSemanticColors(string culture)
    {
        var oldCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var rows = new[] { Row(1, true), Row(2, false), Row(3, null) };
            var query = new FakeQueries(rows);
            var request = Request();
            await new TableExcelExportService(query).ExportAsync(request, Presentation(),
                [60, 60, 60, 100, 100, 100], Output, new ProgressRecorder(), default);
            using var book = SpreadsheetDocument.Open(Output, false);
            var errors = new OpenXmlValidator().Validate(book).ToArray();
            Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(error => error.Description + " " + error.Node?.OuterXml)));
            var worksheet = book.WorkbookPart!.WorksheetParts.Single().Worksheet;
            var dataRows = worksheet.Descendants<Row>().Skip(1).ToArray();
            Assert.Equal(new[] { "✓", "", "?" }, dataRows.Select(row => row.Elements<Cell>().ElementAt(1).InnerText));
            Assert.Equal(new[] { "✓", "", "⌛" }, dataRows.Select(row => row.Elements<Cell>().ElementAt(2).InnerText));
            Assert.DoesNotContain(worksheet.Descendants<Cell>(), cell => cell.DataType?.Value == CellValues.Boolean);
            var cells = dataRows[0].Elements<Cell>().ToArray();
            Assert.Equal(CellValues.Number, cells[3].DataType!.Value);
            Assert.Equal("1250.75", cells[3].CellValue!.Text);
            Assert.Equal(CellValues.Number, cells[4].DataType!.Value);
            Assert.Equal("Подписан", cells[5].InnerText);
            var styles = book.WorkbookPart.WorkbookStylesPart!.Stylesheet;
            var statusFormat = styles.CellFormats!.Elements<CellFormat>().ElementAt((int)cells[5].StyleIndex!.Value);
            var statusFill = styles.Fills!.Elements<Fill>().ElementAt((int)statusFormat.FillId!.Value);
            Assert.Equal("FEC2EDF6", statusFill.PatternFill!.ForegroundColor!.Rgb!.Value);
            Assert.NotNull(worksheet.Elements<AutoFilter>().Single());
            Assert.Equal("A2", worksheet.Descendants<Pane>().Single().TopLeftCell!.Value);
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
    }

    [Fact]
    public async Task Export_LoadsAllBatchesWithTheSameFiltersAndSorts()
    {
        var rows = Enumerable.Range(1, 1003).Select(id => Row(id, true)).ToArray();
        var queries = new FakeQueries(rows);
        var request = Request();
        var progress = new ProgressRecorder();
        await new TableExcelExportService(queries).ExportAsync(request, Presentation(),
            [60, 60, 60, 100, 100, 100], Output, progress, default);
        Assert.Equal(1, queries.CountRequests);
        Assert.Equal(new int?[] { 0, 1000 }, queries.Pages.Select(page => page.Offset));
        Assert.Equal(new int?[] { 1000, 3 }, queries.Pages.Select(page => page.Limit));
        Assert.All(queries.Pages, page =>
        {
            Assert.Same(request.Query.Filters, page.Filters);
            Assert.Equal(request.Query.Sorts, page.Sorts);
            Assert.Equal("Contract", page.Model);
            Assert.Equal("list", page.Preset);
        });
        Assert.Equal(new TableExportProgress(1003, 1003), progress.Last);
        using var book = SpreadsheetDocument.Open(Output, false);
        Assert.Equal(1004, book.WorkbookPart!.WorksheetParts.Single().Worksheet.Descendants<Row>().Count());
    }

    [Fact]
    public async Task Export_CancellationPreservesExistingFileAndRemovesTemporaryFile()
    {
        await File.WriteAllTextAsync(Output, "existing", System.Text.Encoding.UTF8);
        using var cancellation = new CancellationTokenSource();
        var progress = new ProgressRecorder { OnReport = value => { if (value.Written == 1000) cancellation.Cancel(); } };
        var queries = new FakeQueries(Enumerable.Range(1, 1003).Select(id => Row(id, true)).ToArray());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new TableExcelExportService(queries)
            .ExportAsync(Request(), Presentation(), [60, 60, 60, 100, 100, 100], Output, progress, cancellation.Token));
        Assert.Equal("existing", await File.ReadAllTextAsync(Output, System.Text.Encoding.UTF8));
        Assert.Equal(new[] { Output }, Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task Export_EmptySelectionProducesValidHeaderOnlyWorkbook()
    {
        var queries = new FakeQueries([]);
        await new TableExcelExportService(queries).ExportAsync(Request(), Presentation(),
            [60, 60, 60, 100, 100, 100], Output, new ProgressRecorder(), default);
        Assert.Empty(queries.Pages);
        using var book = SpreadsheetDocument.Open(Output, false);
        Assert.Empty(new OpenXmlValidator().Validate(book));
        Assert.Single(book.WorkbookPart!.WorksheetParts.Single().Worksheet.Descendants<Row>());
    }

    private static TableExportRequest Request() => new("Контракты", new DataQueryRequest
    {
        Model = "Contract", Preset = "list", Filters = new Dictionary<string, object> { ["status_id__in"] = new[] { 1 } },
        Sorts = new[] { "id asc" }
    }, [
        new() { FieldKey = "id", Header = "ID" },
        new() { FieldKey = "flag", Header = "Флаг", BodyMode = CbsTableBodyMode.BooleanIcon },
        new() { FieldKey = "flag", Header = "БЗ", BodyTemplateKey = "ContractFunded" },
        new() { FieldKey = "cost", Header = "Сумма", BodyTemplateKey = "ContractCost" },
        new() { FieldKey = "signed_at", Header = "Дата", Filter = new() { Mode = DataFilterMode.Date } },
        new() { FieldKey = "status", DisplayField = "status.name", Header = "Статус", BodyTemplateKey = "StatusBadge" }
    ], CbsTableRowStyleKey.None);

    private static TableDataRow Row(int id, bool? flag) => JsonSerializer.Deserialize<TableDataRow>(JsonSerializer.Serialize(new
    {
        id, flag, cost = 1250.75m, signed_at = "2026-09-14T12:00:00Z", status = new { id = 1, name = "Подписан" }
    }))!;

    private static TablePresentationContext Presentation() => new(new Dictionary<string, uint>
    {
        ["ShellPrimaryTextBrush"] = 0xFF1B3550, ["ShellTableRowBackgroundBrush"] = 0xFFFFFFFF,
        ["ShellTableHeaderBackgroundBrush"] = 0xFFF0F0F0, ["ShellTableHeaderTextBrush"] = 0xFF111111,
        ["ShellTableGridLineBrush"] = 0xFFAAAAAA, ["SystemFillColorSuccessBrush"] = 0xFF008800
    }, true, 12);

    private sealed class ProgressRecorder : IProgress<TableExportProgress>
    {
        public TableExportProgress? Last { get; private set; }
        public Action<TableExportProgress>? OnReport { get; init; }
        public void Report(TableExportProgress value) { Last = value; OnReport?.Invoke(value); }
    }

    private sealed class FakeQueries(IReadOnlyList<TableDataRow> rows) : IDataQueryService
    {
        public int CountRequests { get; private set; }
        public List<DataQueryRequest> Pages { get; } = [];
        public Task<int> GetCountAsync(DataQueryRequest request, CancellationToken cancellationToken = default)
        { CountRequests++; return Task.FromResult(rows.Count); }
        public Task<IReadOnlyList<T>> GetDataAsync<T>(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Pages.Add(request);
            return Task.FromResult<IReadOnlyList<T>>(rows.Skip(request.Offset!.Value).Take(request.Limit!.Value).Cast<T>().ToArray());
        }
        public Task<DataQueryPage<T>> GetPageAsync<T>(DataQueryRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
