using System.IO;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Export;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services.Table;

namespace CbsContractsDesktopClient.Services.Export;

public sealed class TableExcelExportService(IDataQueryService queries)
{
    private const int BatchSize = 1000;

    public async Task ExportAsync(TableExportRequest request, TablePresentationContext presentation,
        IReadOnlyList<double> columnWidths, string path, IProgress<TableExportProgress> progress,
        CancellationToken cancellationToken)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var total = await queries.GetCountAsync(request.Query, cancellationToken);
            if (total > 1_048_575)
                throw new InvalidOperationException("Выборка превышает вместимость листа Excel (1 048 575 строк данных). Уточните фильтры.");
            if (request.Columns.Count == 0)
                throw new InvalidOperationException("Нет видимых столбцов для экспорта.");
            progress.Report(new(0, total));
            var writer = new ExcelTableWriter(temporaryPath, request, presentation, columnWidths);
            try
            {
                var offset = 0;
                while (offset < total)
                {
                    var rows = await queries.GetDataAsync<TableDataRow>(new DataQueryRequest
                    {
                        Model = request.Query.Model, Preset = request.Query.Preset,
                        Filters = request.Query.Filters, Sorts = request.Query.Sorts,
                        Offset = offset, Limit = Math.Min(BatchSize, total - offset)
                    }, cancellationToken);
                    if (rows.Count == 0 || rows.Count > Math.Min(BatchSize, total - offset))
                        throw new InvalidOperationException($"Размер страницы экспорта при offset={offset} не соответствует запросу. Возможно, выборка изменилась; повторите экспорт.");
                    await Task.Run(() =>
                    {
                        foreach (var row in rows)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            writer.WriteRow(row);
                        }
                    }, cancellationToken);
                    offset += rows.Count;
                    progress.Report(new(offset, total));
                }
                await Task.Run(writer.Complete, cancellationToken);
            }
            finally
            {
                await Task.Run(writer.Dispose);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
