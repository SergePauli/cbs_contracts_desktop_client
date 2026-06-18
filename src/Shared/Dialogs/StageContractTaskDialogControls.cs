using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Shared.Formatting;
using Pauli.WinUiKit.Controls;

namespace CbsContractsDesktopClient.Shared.Dialogs;

public static class StageContractTaskDialogControls
{
    public static IReadOnlyList<StageTaskOption> CreateTaskOptions(
        IReadOnlyList<ReferenceLookupItem> taskKindItems,
        IReadOnlyList<StageTaskRecord> selectedTasks)
    {
        var selectedKinds = selectedTasks
            .Select(static item => item.TaskKindId)
            .Where(static id => id is not null)
            .Select(static id => id!.Value)
            .ToHashSet();

        return taskKindItems
            .Where(static item => string.IsNullOrWhiteSpace(item.Code))
            .Select(item => new StageTaskOption(
                TaskKindId: AppFormatters.TryGetLong(item.Id) ?? 0,
                Name: item.DisplayName,
                IsSelected: AppFormatters.TryGetLong(item.Id) is long id && selectedKinds.Contains(id)))
            .Where(static item => item.TaskKindId > 0 && !string.IsNullOrWhiteSpace(item.Name))
            .OrderBy(static item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static MultiSelect ConfigureTasksMultiSelect(
        MultiSelect multiSelect,
        IReadOnlyList<StageTaskOption> taskOptions,
        IReadOnlySet<long> selectedTaskKindIds,
        EventHandler<MultiSelectChangedEventArgs> selectionChanged)
    {
        multiSelect.Options = taskOptions;
        multiSelect.Value = taskOptions
            .Where(option => selectedTaskKindIds.Contains(option.TaskKindId))
            .ToList();
        multiSelect.Display = "chip";
        multiSelect.OptionLabel = nameof(StageTaskOption.Name);
        multiSelect.MaxSelectedLabels = 4;
        multiSelect.Placeholder = "Выбрать";
        multiSelect.Tooltip = "Прочие задачи";
        multiSelect.SelectionChanged += selectionChanged;
        return multiSelect;
    }

    public static void UpdateSelectedTaskKindIds(
        ISet<long> selectedTaskKindIds,
        MultiSelectChangedEventArgs args)
    {
        selectedTaskKindIds.Clear();
        foreach (var option in args.Value.OfType<StageTaskOption>())
        {
            selectedTaskKindIds.Add(option.TaskKindId);
        }
    }
}

public sealed record StageTaskOption(long TaskKindId, string Name, bool IsSelected);

public sealed record StageTaskRecord(long? Id, string? ListKey, long? TaskKindId, string Name);
