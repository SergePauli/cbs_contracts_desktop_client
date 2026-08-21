using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.ViewModels.Reports;

public enum ActivityReportSectionKind
{
    StatusChanges,
    PendingStages,
    AddedContracts,
    DeadlineChanges,
    Comments,
    Funding,
    Payments
}

public enum ActivityReportTargetKind
{
    Contract,
    Stage
}

public sealed record ActivityReportRow(
    TableDataRow DisplayRow,
    TableDataRow? TargetRow,
    ActivityReportTargetKind TargetKind);

public sealed record ActivityReportSection(
    ActivityReportSectionKind Kind,
    string Title,
    IReadOnlyList<ActivityReportRow> Rows);

public sealed record ActivityReportResult(IReadOnlyList<ActivityReportSection> Sections);

public sealed class ActivityReportLimitExceededException : InvalidOperationException
{
    public ActivityReportLimitExceededException(string sectionTitle)
        : base($"Раздел «{sectionTitle}» содержит больше 999 записей. Уменьшите период отчета и повторите попытку.")
    {
    }
}
