using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Reports;

public sealed class ActivityReportLoader
{
    private const int MaximumRowCount = 999;
    private const int ProbeLimit = MaximumRowCount + 1;
    private readonly IDataQueryService _dataQueryService;

    public ActivityReportLoader(IDataQueryService dataQueryService)
    {
        _dataQueryService = dataQueryService;
    }

    public async Task<ActivityReportResult> LoadAsync(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        if (endDate.Date < startDate.Date)
        {
            throw new InvalidOperationException("Дата окончания периода не может быть раньше даты начала.");
        }

        var periodFilters = new Dictionary<string, object?>
        {
            ["created_at__gt"] = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["created_at__lt"] = endDate.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };

        var loads = new[]
        {
            LoadEventsAsync(ActivityReportSectionKind.StatusChanges, "Сменили статус", "Audit", Merge(periodFilters, ("auditable_field__eq", "status")), cancellationToken),
            LoadEventsAsync(ActivityReportSectionKind.PendingStages, "Переданы в ОИ", "Audit", Merge(periodFilters, ("auditable_field__eq", "status"), ("after__eq", "В работе")), cancellationToken),
            LoadEventsAsync(ActivityReportSectionKind.AddedContracts, "Новые контракты", "Audit", Merge(periodFilters, ("action__eq", 0), ("auditable_type__eq", "Contract")), cancellationToken),
            LoadEventsAsync(ActivityReportSectionKind.DeadlineChanges, "Изменение сроков", "Audit", Merge(periodFilters, ("auditable_field__eq", "deadline_at")), cancellationToken),
            LoadEventsAsync(ActivityReportSectionKind.Comments, "Добавлены комментарии", "Comment", periodFilters, cancellationToken),
            LoadEventsAsync(ActivityReportSectionKind.Funding, "Бухгалтерское закрытие", "Audit", Merge(periodFilters, ("auditable_field__eq", "is_funded")), cancellationToken),
            LoadEventsAsync(ActivityReportSectionKind.Payments, "Внесение оплаты", "Audit", Merge(periodFilters, ("auditable_field__in", new[] { "payment_at", "prepayment_at" })), cancellationToken)
        };
        var eventSets = await Task.WhenAll(loads);

        var contractIds = new HashSet<long>();
        var stageIds = new HashSet<long>();
        foreach (var set in eventSets)
        {
            foreach (var row in set.Rows)
            {
                var id = RequireTargetId(row, set.Kind);
                if (ResolveTargetKind(row, set.Kind) == ActivityReportTargetKind.Contract)
                {
                    contractIds.Add(id);
                }
                else
                {
                    stageIds.Add(id);
                }
            }
        }

        var contractsTask = LoadTargetsAsync("Contract", contractIds, cancellationToken);
        var stagesTask = LoadTargetsAsync("Stage", stageIds, cancellationToken);
        await Task.WhenAll(contractsTask, stagesTask);
        var contracts = contractsTask.Result.ToDictionary(RequireId);
        var stages = stagesTask.Result.ToDictionary(RequireId);
        var missingContracts = contractIds.Where(id => !contracts.ContainsKey(id)).ToList();
        var missingStages = stageIds.Where(id => !stages.ContainsKey(id)).ToList();
        var deletedNames = await LoadDeletedNamesAsync(missingContracts, missingStages, cancellationToken);

        return new ActivityReportResult(eventSets.Select(set => new ActivityReportSection(
            set.Kind,
            set.Title,
            set.Rows.Select(row => BuildRow(row, set.Kind, contracts, stages, deletedNames)).ToList())).ToList());
    }

    private async Task<EventSet> LoadEventsAsync(
        ActivityReportSectionKind kind,
        string title,
        string model,
        IReadOnlyDictionary<string, object?> filters,
        CancellationToken cancellationToken)
    {
        var rows = await _dataQueryService.GetDataAsync<TableDataRow>(new DataQueryRequest
        {
            Model = model,
            Preset = "card",
            Filters = filters,
            Limit = ProbeLimit
        }, cancellationToken);
        if (rows.Count > MaximumRowCount)
        {
            throw new ActivityReportLimitExceededException(title);
        }

        return new EventSet(
            kind,
            title,
            rows.Where(row => IsEventIncluded(row, kind)).ToList());
    }

    private static bool IsEventIncluded(TableDataRow row, ActivityReportSectionKind kind)
    {
        var targetIdField = kind == ActivityReportSectionKind.Comments ? "commentable_id" : "obj_id";
        if (TryGetLong(row.GetValue(targetIdField)) is null)
        {
            return false;
        }

        return kind != ActivityReportSectionKind.PendingStages
            || string.Equals(row.GetValue("after")?.ToString(), "В работе", StringComparison.Ordinal);
    }

    private async Task<IReadOnlyList<TableDataRow>> LoadTargetsAsync(
        string model,
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dataQueryService.GetDataAsync<TableDataRow>(new DataQueryRequest
        {
            Model = model,
            Preset = "list",
            Filters = new Dictionary<string, object?> { ["id__in"] = ids },
            Limit = MaximumRowCount
        }, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<DeletedTargetKey, string>> LoadDeletedNamesAsync(
        IReadOnlyCollection<long> contractIds,
        IReadOnlyCollection<long> stageIds,
        CancellationToken cancellationToken)
    {
        var targetFilters = new List<IReadOnlyDictionary<string, object?>>();
        AddDeletedTargetFilter(targetFilters, "Contract", contractIds);
        AddDeletedTargetFilter(targetFilters, "Stage", stageIds);
        if (targetFilters.Count == 0)
        {
            return new Dictionary<DeletedTargetKey, string>();
        }

        var filters = new Dictionary<string, object?>
        {
            ["action__eq"] = 0
        };
        if (targetFilters.Count == 1)
        {
            foreach (var (key, value) in targetFilters[0])
            {
                filters[key] = value;
            }
        }
        else
        {
            filters["or"] = targetFilters.Select(static branch => new Dictionary<string, object?>
            {
                ["and"] = branch
            }).ToList();
        }

        var request = new DataQueryRequest
        {
            Model = "Audit",
            Preset = "card",
            Filters = filters,
            Sorts = ["created_at desc"],
            Limit = MaximumRowCount
        };

        var rows = await _dataQueryService.GetDataAsync<TableDataRow>(request, cancellationToken);

        var result = new Dictionary<DeletedTargetKey, string>();
        foreach (var row in rows)
        {
            var id = TryGetLong(row.GetValue("obj_id")) ?? TryGetLong(row.GetValue("auditable_id"));
            var targetKind = ResolveDeletedTargetKind(row);
            var name = row.GetValue("detail")?.ToString()?.Trim();
            if (id is long targetId && targetKind is ActivityReportTargetKind kind && !string.IsNullOrWhiteSpace(name))
            {
                result.TryAdd(new DeletedTargetKey(kind, targetId), name);
            }
        }

        return result;
    }

    private static void AddDeletedTargetFilter(
        ICollection<IReadOnlyDictionary<string, object?>> filters,
        string auditableType,
        IReadOnlyCollection<long> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        filters.Add(new Dictionary<string, object?>
        {
            ["auditable_type__eq"] = auditableType,
            ["auditable_id__in"] = ids
        });
    }

    private static ActivityReportTargetKind? ResolveDeletedTargetKind(TableDataRow row)
    {
        var type = row.GetValue("auditable_type")?.ToString() ?? row.GetValue("where")?.ToString();
        if (string.Equals(type, "Contract", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "Контракты", StringComparison.OrdinalIgnoreCase))
        {
            return ActivityReportTargetKind.Contract;
        }

        if (string.Equals(type, "Stage", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "Этапы", StringComparison.OrdinalIgnoreCase))
        {
            return ActivityReportTargetKind.Stage;
        }

        return null;
    }

    private static ActivityReportRow BuildRow(
        TableDataRow source,
        ActivityReportSectionKind kind,
        IReadOnlyDictionary<long, TableDataRow> contracts,
        IReadOnlyDictionary<long, TableDataRow> stages,
        IReadOnlyDictionary<DeletedTargetKey, string> deletedNames)
    {
        var targetKind = ResolveTargetKind(source, kind);
        var targetId = RequireTargetId(source, kind);
        var target = targetKind == ActivityReportTargetKind.Contract
            ? contracts.GetValueOrDefault(targetId)
            : stages.GetValueOrDefault(targetId);
        var deletedName = target is null
            ? deletedNames.GetValueOrDefault(new DeletedTargetKey(targetKind, targetId))
            : null;
        var deletedTarget = ParseDeletedTargetDetail(deletedName);
        var values = new Dictionary<string, object?>
        {
            ["id"] = TryGetLong(source.GetValue("id")),
            ["num"] = target is null ? deletedTarget.Number ?? $"удален #{targetId}" : target.GetValue("name"),
            ["is_gov"] = targetKind == ActivityReportTargetKind.Contract ? target?.GetValue("governmental") : target?.GetValue("contract.governmental"),
            ["start"] = targetKind == ActivityReportTargetKind.Contract ? target?.GetValue("signed_at") : target?.GetValue("start_at"),
            ["contragent"] = target is null
                ? deletedTarget.Name
                : targetKind == ActivityReportTargetKind.Contract
                    ? target.GetValue("contragent.name")
                    : target.GetValue("contract.contragent.name"),
            ["cost"] = target is null ? "удален" : target.GetValue("cost"),
            ["old_value"] = source.GetValue("before"),
            ["new_value"] = kind == ActivityReportSectionKind.Comments ? source.GetValue("content") : source.GetValue("after"),
            ["what"] = source.GetValue("field"),
            ["operation"] = $"{source.GetValue("field")}: {source.GetValue("before")} → {source.GetValue("after")}",
            ["when"] = source.GetValue("when"),
            ["who"] = kind == ActivityReportSectionKind.Comments ? source.GetValue("person.name") : source.GetValue("who"),
            ["status"] = target?.GetValue("status.name")
        };
        return new ActivityReportRow(ToRow(values), target, targetKind);
    }

    private static (string? Number, string? Name) ParseDeletedTargetDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return (null, null);
        }

        var value = detail.Trim().TrimStart('*').TrimStart();
        var separatorIndex = value.IndexOf(' ');
        return separatorIndex < 0
            ? (value, null)
            : (value[..separatorIndex], value[(separatorIndex + 1)..].Trim());
    }

    private static ActivityReportTargetKind ResolveTargetKind(TableDataRow row, ActivityReportSectionKind kind)
    {
        if (kind is ActivityReportSectionKind.PendingStages or ActivityReportSectionKind.Funding or ActivityReportSectionKind.Payments)
        {
            return ActivityReportTargetKind.Stage;
        }

        if (kind == ActivityReportSectionKind.AddedContracts)
        {
            return ActivityReportTargetKind.Contract;
        }

        var type = kind == ActivityReportSectionKind.Comments ? row.GetValue("commentable_type") : row.GetValue("where");
        return string.Equals(type?.ToString(), "Contract", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type?.ToString(), "Контракты", StringComparison.OrdinalIgnoreCase)
            ? ActivityReportTargetKind.Contract
            : ActivityReportTargetKind.Stage;
    }

    private static long RequireTargetId(TableDataRow row, ActivityReportSectionKind kind) =>
        TryGetLong(row.GetValue(kind == ActivityReportSectionKind.Comments ? "commentable_id" : "obj_id"))
        ?? throw new InvalidOperationException("Строка события отчета не содержит идентификатор объекта.");

    private static long RequireId(TableDataRow row) => TryGetLong(row.GetValue("id"))
        ?? throw new InvalidOperationException("Строка объекта отчета не содержит id.");

    private static Dictionary<string, object?> Merge(
        IReadOnlyDictionary<string, object?> source,
        params (string Key, object? Value)[] additions)
    {
        var result = new Dictionary<string, object?>(source);
        foreach (var addition in additions)
        {
            result[addition.Key] = addition.Value;
        }
        return result;
    }

    private static TableDataRow ToRow(IReadOnlyDictionary<string, object?> values) =>
        JsonSerializer.Deserialize<TableDataRow>(JsonSerializer.Serialize(values))
        ?? throw new InvalidOperationException("Не удалось сформировать строку отчета.");

    private sealed record EventSet(ActivityReportSectionKind Kind, string Title, IReadOnlyList<TableDataRow> Rows);

    private readonly record struct DeletedTargetKey(ActivityReportTargetKind Kind, long Id);
}
