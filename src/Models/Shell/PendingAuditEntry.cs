namespace CbsContractsDesktopClient.Models.Shell;

public sealed record PendingAuditEntry(
    string AuditableType,
    long AuditableId,
    string AuditableField,
    string Action,
    string Detail,
    string Before,
    string After);
