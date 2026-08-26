using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

namespace CbsContractsDesktopClient.ViewModels.Workflow;

public static class RevisionEditPayloadBuilder
{
    public static IReadOnlyDictionary<string, object?> BuildForUpdate(RevisionEditState revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        var id = revision.Id
            ?? throw new InvalidOperationException("Revision update payload must contain id.");
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = id
        };
        if (!string.IsNullOrWhiteSpace(revision.ListKey))
        {
            payload["list_key"] = revision.ListKey;
        }

        AppendChanged(payload, "priority", revision.Original.Priority, revision.Priority);
        AppendChanged(payload, "is_signed", revision.Original.IsSigned, revision.IsSigned);
        AppendChanged(payload, "is_present", revision.Original.IsPresent, revision.IsPresent);
        AppendChanged(payload, "used", revision.Original.Used, revision.Used);
        AppendChangedText(payload, "description", revision.Original.Description, revision.Description);
        AppendChangedText(payload, "doc_link", revision.Original.DocLink, revision.DocLink);
        AppendChangedText(payload, "scan_link", revision.Original.ScanLink, revision.ScanLink);
        AppendChangedText(payload, "protocol_link", revision.Original.ProtocolLink, revision.ProtocolLink);
        AppendChangedText(payload, "zip_link", revision.Original.ZipLink, revision.ZipLink);
        return payload;
    }

    private static void AppendChanged(
        IDictionary<string, object?> payload,
        string key,
        object? originalValue,
        object? value)
    {
        if (!Equals(originalValue, value))
        {
            payload[key] = value;
        }
    }

    private static void AppendChangedText(
        IDictionary<string, object?> payload,
        string key,
        string? originalValue,
        string? value)
    {
        var original = NormalizeText(originalValue);
        var current = NormalizeText(value);
        if (!string.Equals(original, current, StringComparison.Ordinal))
        {
            payload[key] = current;
        }
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
