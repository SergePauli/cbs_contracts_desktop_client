using CbsContractsDesktopClient.Models.Export;

namespace CbsContractsDesktopClient.Stores.Table;

public partial class TablePageStore
{
    public bool CanExport => _userService?.CurrentUser?.Role
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Contains("excel", StringComparer.OrdinalIgnoreCase) == true;

    public TableExportRequest CreateExportRequest()
    {
        if (!CanExport)
            throw new UnauthorizedAccessException("Для экспорта требуется роль excel.");
        var definition = CurrentTablePage?.Clone()
            ?? throw new InvalidOperationException("Таблица для экспорта не открыта.");
        var query = _state?.CreateQuerySnapshot()
            ?? throw new InvalidOperationException("Запрос таблицы ещё не инициализирован.");
        return new(definition.Title, query,
            definition.Columns.Where(column => column.IsVisible).ToList().AsReadOnly(), definition.RowStyleKey);
    }
}
