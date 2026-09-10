using CbsContractsDesktopClient.Models.Shell;

namespace CbsContractsDesktopClient.Stores.Table;

public static class AuditCreatePayloadBuilder
{
    public static IReadOnlyDictionary<string, object?> Build(
        PendingAuditEntry entry,
        int userId,
        int personId)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.AuditableType))
        {
            throw new InvalidOperationException("Audit create entry must contain auditable_type.");
        }

        if (entry.AuditableId <= 0)
        {
            throw new InvalidOperationException("Audit create entry must contain auditable_id.");
        }

        if (string.IsNullOrWhiteSpace(entry.AuditableField))
        {
            throw new InvalidOperationException("Audit create entry must contain auditable_field.");
        }

        if (string.IsNullOrWhiteSpace(entry.Action))
        {
            throw new InvalidOperationException("Audit create entry must contain action.");
        }

        if (string.IsNullOrWhiteSpace(entry.Detail))
        {
            throw new InvalidOperationException("Audit create entry must contain detail.");
        }

        if (string.IsNullOrWhiteSpace(entry.Before))
        {
            throw new InvalidOperationException("Audit create entry must contain before.");
        }

        if (string.IsNullOrWhiteSpace(entry.After))
        {
            throw new InvalidOperationException("Audit create entry must contain after.");
        }

        if (userId <= 0)
        {
            throw new InvalidOperationException("Audit create entry must contain user_id.");
        }

        if (personId <= 0)
        {
            throw new InvalidOperationException("Audit create entry must contain person_id.");
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["auditable_type"] = entry.AuditableType,
            ["auditable_id"] = entry.AuditableId,
            ["auditable_field"] = entry.AuditableField,
            ["action"] = entry.Action,
            ["detail"] = entry.Detail,
            ["before"] = entry.Before,
            ["after"] = entry.After,
            ["user_id"] = userId,
            ["person_id"] = personId
        };
    }
}
