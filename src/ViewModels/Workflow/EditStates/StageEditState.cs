using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Shared.Formatting;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

public sealed class StageEditState : IEditState
{
    private StageEditState(StageEditStateSnapshot original)
    {
        Original = original;
        RestoreOriginal();
    }

    public StageEditStateSnapshot Original { get; }

    public long Id { get; private set; }

    public string? ListKey { get; private set; }

    public string? Name { get; private set; }

    public int? Priority { get; private set; }

    public decimal? Cost { get; private set; }

    public StatusEditState Status { get; set; } = new(null, null);

    public TaskKindEditState TaskKind { get; private set; } = new(null, null, null);

    public string? DeadlineKind { get; set; }

    public int? Duration { get; set; }

    public DateTimeOffset? StartAt { get; set; }

    public DateTimeOffset? DeadlineAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? PaymentDeadlineKind { get; set; }

    public int? PaymentDuration { get; set; }

    public DateTimeOffset? PaymentDeadlineAt { get; set; }

    public DateTimeOffset? PaymentAt { get; set; }

    public DateTimeOffset? PrepaymentAt { get; set; }

    public DateTimeOffset? InvoiceAt { get; set; }

    public DateTimeOffset? FundedAt { get; set; }

    public bool IsFunded { get; set; }

    public bool? IsRideOut { get; set; }

    public DateTimeOffset? RideOutAt { get; set; }

    public bool? IsSended { get; set; }

    public DateTimeOffset? SendedAt { get; set; }

    public int? RegistryQuarter { get; set; }

    public int? RegistryYear { get; set; }

    public IReadOnlyList<StageTaskEditState> Tasks { get; private set; } = [];

    public IReadOnlyList<StagePerformerEditState> Performers { get; set; } = [];

    public DateTimeOffset? PaymentBaseDate => PrepaymentAt ?? PaymentAt;

    public bool HasChanges =>
        HasStatusChanges
        || HasDeadlineRuleChanges
        || HasFinancialChanges
        || HasOziChanges
        || !SameDate(ClosedAt, Original.ClosedAt);

    public bool HasStatusChanges => Status.Id != Original.Status.Id;

    public bool HasDeadlineRuleChanges =>
        !string.Equals(DeadlineKind, Original.DeadlineKind, StringComparison.Ordinal)
        || Duration != Original.Duration
        || !SameDate(StartAt, Original.StartAt)
        || !SameDate(DeadlineAt, Original.DeadlineAt);

    public bool HasFinancialChanges =>
        !string.Equals(PaymentDeadlineKind, Original.PaymentDeadlineKind, StringComparison.Ordinal)
        || PaymentDuration != Original.PaymentDuration
        || !SameDate(PaymentDeadlineAt, Original.PaymentDeadlineAt)
        || !SameDate(PaymentAt, Original.PaymentAt)
        || !SameDate(PrepaymentAt, Original.PrepaymentAt)
        || !SameDate(InvoiceAt, Original.InvoiceAt)
        || !SameDate(FundedAt, Original.FundedAt)
        || IsFunded != Original.IsFunded;

    public bool HasOziChanges =>
        !SameDate(CompletedAt, Original.CompletedAt)
        || !SameDate(RideOutAt, Original.RideOutAt)
        || !SameDate(SendedAt, Original.SendedAt)
        || IsRideOut != Original.IsRideOut
        || IsSended != Original.IsSended
        || RegistryQuarter != Original.RegistryQuarter
        || RegistryYear != Original.RegistryYear
        || !SamePerformerEmployees(Performers, Original.Performers);

    public void RestoreOriginal()
    {
        Id = Original.Id;
        ListKey = Original.ListKey;
        Name = Original.Name;
        Priority = Original.Priority;
        Cost = Original.Cost;
        Status = Original.Status;
        TaskKind = Original.TaskKind;
        DeadlineKind = Original.DeadlineKind;
        Duration = Original.Duration;
        StartAt = Original.StartAt;
        DeadlineAt = Original.DeadlineAt;
        ClosedAt = Original.ClosedAt;
        CompletedAt = Original.CompletedAt;
        PaymentDeadlineKind = Original.PaymentDeadlineKind;
        PaymentDuration = Original.PaymentDuration;
        PaymentDeadlineAt = Original.PaymentDeadlineAt;
        PaymentAt = Original.PaymentAt;
        PrepaymentAt = Original.PrepaymentAt;
        InvoiceAt = Original.InvoiceAt;
        FundedAt = Original.FundedAt;
        IsFunded = Original.IsFunded;
        IsRideOut = Original.IsRideOut;
        RideOutAt = Original.RideOutAt;
        IsSended = Original.IsSended;
        SendedAt = Original.SendedAt;
        RegistryQuarter = Original.RegistryQuarter;
        RegistryYear = Original.RegistryYear;
        Tasks = Original.Tasks;
        Performers = Original.Performers;
    }

    public bool IsLastOpenStageIn(ContractEditState? contract, long closedStatusId)
    {
        if (contract is null || contract.Stages.Count == 0)
        {
            return false;
        }

        return contract.Stages
            .Where(stage => stage.Id != Id)
            .All(stage => stage.StatusId == closedStatusId);
    }

    public bool IsStatusChangedTo(long statusId)
    {
        return Status.Id == statusId && Original.Status.Id != statusId;
    }

    public bool ShouldCloseContract(ContractEditState? contract, long closedStatusId)
    {
        return contract is not null
            && IsStatusChangedTo(closedStatusId)
            && ClosedAt is not null
            && contract.Status.Id != closedStatusId
            && IsLastOpenStageIn(contract, closedStatusId);
    }

    public string GetSectionTitle(ContractEditState? contract)
    {
        if (contract?.IsMultiStage != true)
        {
            return "Этап";
        }

        if (Priority is null)
        {
            throw new InvalidOperationException("Stage edit state must contain priority for multistage section title.");
        }

        if (string.IsNullOrWhiteSpace(TaskKind.Name))
        {
            throw new InvalidOperationException("Stage edit state must contain task_kind.name for multistage section title.");
        }

        return $"Этап {Priority.Value:00} {TaskKind.Name}";
    }

    public string? GetSectionTitleAmount(ContractEditState? contract)
    {
        if (contract?.IsMultiStage != true)
        {
            return null;
        }

        var costText = AppFormatters.FormatMoney(Cost);
        return string.IsNullOrWhiteSpace(costText) ? null : costText;
    }

    public string GetEditDialogTitle()
    {
        return string.IsNullOrWhiteSpace(Name)
            ? "Редактирование этапа"
            : $"Редактирование этапа {Name}";
    }

    public StageFinEditPayloadInput ToFinPayloadInput(string? comment, int? profileId)
    {
        return new StageFinEditPayloadInput(
            Id,
            ListKey,
            PaymentAt,
            PrepaymentAt,
            InvoiceAt,
            FundedAt,
            IsFunded,
            StartAt,
            DeadlineAt,
            PaymentDeadlineAt,
            comment,
            profileId);
    }

    public StageCommerEditPayloadInput ToCommerPayloadInput(
        IReadOnlyCollection<long> selectedTaskKindIds,
        string? comment,
        int? profileId)
    {
        return new StageCommerEditPayloadInput(
            Id,
            ListKey,
            Status.Id,
            DeadlineKind,
            DeadlineAt,
            StartAt,
            PaymentDeadlineKind,
            PaymentDeadlineAt,
            Duration,
            PaymentDuration,
            ClosedAt,
            selectedTaskKindIds,
            comment,
            profileId);
    }

    public StageOziEditPayloadInput ToOziPayloadInput(
        IReadOnlyList<StagePerformerEditState> selectedPerformers,
        string? comment,
        int? profileId)
    {
        return new StageOziEditPayloadInput(
            Id,
            ListKey,
            Status.Id,
            CompletedAt,
            RideOutAt,
            SendedAt,
            ClosedAt,
            IsRideOut,
            IsSended,
            RegistryQuarter,
            RegistryYear,
            selectedPerformers,
            comment,
            profileId);
    }

    public static StageEditState FromRow(ReferenceDataRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var id = TryGetLong(row.GetValue("id"))
            ?? throw new InvalidOperationException("Stage edit row must contain id.");

        return new StageEditState(new StageEditStateSnapshot(
            Id: id,
            ListKey: row.GetValue("list_key")?.ToString(),
            Name: row.GetValue("name")?.ToString(),
            Priority: TryGetInt(row.GetValue("priority")),
            Cost: TryGetDecimal(row.GetValue("cost")),
            Status: new StatusEditState(
                TryGetLong(row.GetValue("status.id")) ?? TryGetLong(row.GetValue("status_id")),
                row.GetValue("status.name")?.ToString()),
            TaskKind: new TaskKindEditState(
                TryGetLong(row.GetValue("task_kind.id")) ?? TryGetLong(row.GetValue("task_kind_id")),
                row.GetValue("task_kind.name")?.ToString(),
                row.GetValue("task_kind.code")?.ToString()),
            DeadlineKind: row.GetValue("deadline_kind")?.ToString(),
            Duration: TryGetInt(row.GetValue("duration")),
            StartAt: AppFormatters.ParseDate(row.GetValue("start_at")),
            DeadlineAt: AppFormatters.ParseDate(row.GetValue("deadline_at")),
            ClosedAt: AppFormatters.ParseDate(row.GetValue("closed_at")),
            CompletedAt: AppFormatters.ParseDate(row.GetValue("completed_at")),
            PaymentDeadlineKind: row.GetValue("payment_deadline_kind")?.ToString(),
            PaymentDuration: TryGetInt(row.GetValue("payment_duration")),
            PaymentDeadlineAt: AppFormatters.ParseDate(row.GetValue("payment_deadline_at")),
            PaymentAt: AppFormatters.ParseDate(row.GetValue("payment_at")),
            PrepaymentAt: AppFormatters.ParseDate(row.GetValue("prepayment_at")),
            InvoiceAt: AppFormatters.ParseDate(row.GetValue("invoice_at")),
            FundedAt: AppFormatters.ParseDate(row.GetValue("funded_at")),
            IsFunded: TryGetBool(row.GetValue("is_funded")) == true,
            IsRideOut: TryGetBool(row.GetValue("is_ride_out")),
            RideOutAt: AppFormatters.ParseDate(row.GetValue("ride_out_at")),
            IsSended: TryGetBool(row.GetValue("is_sended")),
            SendedAt: AppFormatters.ParseDate(row.GetValue("sended_at")),
            RegistryQuarter: TryGetInt(row.GetValue("registry_quarter")),
            RegistryYear: TryGetInt(row.GetValue("registry_year")),
            Tasks: ReadTasks(row),
            Performers: ReadPerformers(row)));
    }

    private static IReadOnlyList<StageTaskEditState> ReadTasks(ReferenceDataRow row)
    {
        return EnumerateObjectArray(row, "tasks")
            .Select(static task => new StageTaskEditState(
                Id: TryGetLong(task, "id"),
                ListKey: TryGetString(task, "list_key"),
                TaskKindId: TryGetLong(task, "task_kind_id"),
                Name: TryGetString(task, "name")))
            .ToList();
    }

    private static IReadOnlyList<StagePerformerEditState> ReadPerformers(ReferenceDataRow row)
    {
        return EnumerateObjectArray(row, "performers")
            .Select(static performer => new StagePerformerEditState(
                Id: TryGetLong(performer, "id"),
                ListKey: TryGetString(performer, "list_key"),
                EmployeeId: TryGetLong(performer, "employee_id") ?? TryGetLong(performer, "id"),
                Name: TryGetString(performer, "name") ?? string.Empty,
                Priority: TryGetInt(performer, "priority")))
            .ToList();
    }

    private static bool SamePerformerEmployees(
        IReadOnlyList<StagePerformerEditState> left,
        IReadOnlyList<StagePerformerEditState> right)
    {
        return left
            .Select(static item => item.EmployeeId)
            .Where(static id => id is not null)
            .Select(static id => id!.Value)
            .SequenceEqual(right
                .Select(static item => item.EmployeeId)
                .Where(static id => id is not null)
                .Select(static id => id!.Value));
    }

    private static decimal? TryGetDecimal(object? value)
    {
        return value switch
        {
            decimal decimalValue => decimalValue,
            long int64Value => int64Value,
            int int32Value => int32Value,
            string text when decimal.TryParse(
                text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedValue) => parsedValue,
            _ => null
        };
    }

    private static bool SameDate(DateTimeOffset? left, DateTimeOffset? right)
    {
        return ToDateOnly(left) == ToDateOnly(right);
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
    {
        return value is null
            ? null
            : DateOnly.FromDateTime(value.Value.Date);
    }
}

public sealed record StageEditStateSnapshot(
    long Id,
    string? ListKey,
    string? Name,
    int? Priority,
    decimal? Cost,
    StatusEditState Status,
    TaskKindEditState TaskKind,
    string? DeadlineKind,
    int? Duration,
    DateTimeOffset? StartAt,
    DateTimeOffset? DeadlineAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? CompletedAt,
    string? PaymentDeadlineKind,
    int? PaymentDuration,
    DateTimeOffset? PaymentDeadlineAt,
    DateTimeOffset? PaymentAt,
    DateTimeOffset? PrepaymentAt,
    DateTimeOffset? InvoiceAt,
    DateTimeOffset? FundedAt,
    bool IsFunded,
    bool? IsRideOut,
    DateTimeOffset? RideOutAt,
    bool? IsSended,
    DateTimeOffset? SendedAt,
    int? RegistryQuarter,
    int? RegistryYear,
    IReadOnlyList<StageTaskEditState> Tasks,
    IReadOnlyList<StagePerformerEditState> Performers);
