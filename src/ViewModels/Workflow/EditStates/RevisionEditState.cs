using CbsContractsDesktopClient.Models.Table;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

public sealed class RevisionEditState : IEditState
{
    private RevisionEditState(RevisionEditStateSnapshot original)
    {
        Original = original;
        RestoreOriginal();
    }

    public RevisionEditStateSnapshot Original { get; }

    public long? Id { get; private set; }

    public string? ListKey { get; private set; }

    public long Priority { get; set; }

    public bool IsPresent { get; set; }

    public bool IsSigned { get; set; }

    public bool Used { get; set; }

    public string? Description { get; set; }

    public string? DocLink { get; set; }

    public string? ScanLink { get; set; }

    public string? ProtocolLink { get; set; }

    public string? ZipLink { get; set; }

    public bool IsDestroyed { get; set; }

    public bool HasChanges =>
        IsDestroyed
        || Priority != Original.Priority
        || IsPresent != Original.IsPresent
        || IsSigned != Original.IsSigned
        || Used != Original.Used
        || !SameText(Description, Original.Description)
        || !SameText(DocLink, Original.DocLink)
        || !SameText(ScanLink, Original.ScanLink)
        || !SameText(ProtocolLink, Original.ProtocolLink)
        || !SameText(ZipLink, Original.ZipLink);

    public void RestoreOriginal()
    {
        Id = Original.Id;
        ListKey = Original.ListKey;
        Priority = Original.Priority;
        IsPresent = Original.IsPresent;
        IsSigned = Original.IsSigned;
        Used = Original.Used;
        Description = Original.Description;
        DocLink = Original.DocLink;
        ScanLink = Original.ScanLink;
        ProtocolLink = Original.ProtocolLink;
        ZipLink = Original.ZipLink;
        IsDestroyed = false;
    }

    public static RevisionEditState FromRow(TableDataRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new RevisionEditState(new RevisionEditStateSnapshot(
            Id: TryGetLong(row.GetValue("id")),
            ListKey: row.GetValue("list_key")?.ToString(),
            Priority: TryGetLong(row.GetValue("priority")) ?? 0,
            IsPresent: TryGetBool(row.GetValue("is_present")) == true,
            IsSigned: TryGetBool(row.GetValue("is_signed")) == true,
            Used: TryGetBool(row.GetValue("used")) != false,
            Description: NormalizeText(row.GetValue("description")?.ToString()),
            DocLink: NormalizeText(row.GetValue("doc_link")?.ToString()),
            ScanLink: NormalizeText(row.GetValue("scan_link")?.ToString()),
            ProtocolLink: NormalizeText(row.GetValue("protocol_link")?.ToString()),
            ZipLink: NormalizeText(row.GetValue("zip_link")?.ToString())));
    }

    public static RevisionEditState CreateNew(long priority, string? description = null)
    {
        return new RevisionEditState(new RevisionEditStateSnapshot(
            Id: null,
            ListKey: Guid.NewGuid().ToString(),
            Priority: priority,
            IsPresent: false,
            IsSigned: false,
            Used: true,
            Description: description,
            DocLink: null,
            ScanLink: null,
            ProtocolLink: null,
            ZipLink: null));
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool SameText(string? left, string? right)
    {
        return string.Equals(NormalizeText(left), NormalizeText(right), StringComparison.Ordinal);
    }
}

public sealed record RevisionEditStateSnapshot(
    long? Id,
    string? ListKey,
    long Priority,
    bool IsPresent,
    bool IsSigned,
    bool Used,
    string? Description,
    string? DocLink,
    string? ScanLink,
    string? ProtocolLink,
    string? ZipLink);
