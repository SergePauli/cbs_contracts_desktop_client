using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Shell;
using CbsContractsDesktopClient.Services.Workspace;

namespace CbsContractsDesktopClient.Services.Shell;

public sealed record ContractInfoAuditSummary(
    bool IsImported,
    string CreatedAtLabel,
    string? CreatedAt,
    string CreatedByLabel,
    string? CreatedBy,
    string? ClosedBy);

public static class ContractInfoAuditLoader
{
    public static async Task<ContractInfoAuditSummary> LoadAsync(
        IDataQueryService dataQueryService,
        long contractId,
        bool isContractClosed,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(dataQueryService);

            var createdAudit = await LoadCreatedAuditAsync(dataQueryService, contractId, cancellationToken);
            var closedBy = isContractClosed
                ? await LoadClosedByAsync(dataQueryService, contractId, cancellationToken)
                : null;
            return new ContractInfoAuditSummary(
                IsImported(createdAudit),
                GetCreatedAtLabel(createdAudit),
                Normalize(createdAudit?.When),
                GetCreatedByLabel(createdAudit),
                GetCreatedByValue(createdAudit),
                closedBy);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"ContractInfoAuditLoader.LoadAsync: {ex.Message}", ex);
        }
    }

    private static async Task<AuditRecord?> LoadCreatedAuditAsync(
        IDataQueryService dataQueryService,
        long contractId,
        CancellationToken cancellationToken)
    {
        var audits = await dataQueryService.GetDataAsync<AuditRecord>(
            new DataQueryRequest
            {
                Model = "Audit",
                Preset = "card",
                Filters = new Dictionary<string, object?>
                {
                    ["auditable_type__eq"] = "Contract",
                    ["auditable_id__eq"] = contractId,
                    ["action__in"] = new[]
                    {
                        AuditPanelFormatter.GetActionFilterValue(AuditPanelFormatter.AddedAction)!.Value,
                        AuditPanelFormatter.GetActionFilterValue(AuditPanelFormatter.ImportedAction)!.Value
                    }
                },
                Sorts = ["created_at asc"],
                Limit = 1
            },
            cancellationToken);

        return audits.FirstOrDefault();
    }

    private static string GetCreatedAtLabel(AuditRecord? audit)
    {
        return IsImported(audit)
                ? "Импортирован"
                : "Создан";
    }

    private static string GetCreatedByLabel(AuditRecord? audit)
    {
        return IsImported(audit) ? "Импортирован" : "Создал";
    }

    private static string? GetCreatedByValue(AuditRecord? audit)
    {
        return IsImported(audit)
            ? Normalize(audit?.When)
            : Normalize(audit?.Who);
    }

    private static bool IsImported(AuditRecord? audit)
    {
        return string.Equals(
            AuditPanelFormatter.NormalizeAction(audit?.Action),
            AuditPanelFormatter.ImportedAction,
            StringComparison.Ordinal);
    }

    private static async Task<string?> LoadClosedByAsync(
        IDataQueryService dataQueryService,
        long contractId,
        CancellationToken cancellationToken)
    {
        var audits = await dataQueryService.GetDataAsync<AuditRecord>(
            new DataQueryRequest
            {
                Model = "Audit",
                Preset = "card",
                Filters = new Dictionary<string, object?>
                {
                    ["auditable_type__eq"] = "Contract",
                    ["auditable_id__eq"] = contractId,
                    ["action__in"] = new[] { AuditPanelFormatter.GetActionFilterValue(AuditPanelFormatter.UpdatedAction)!.Value }
                },
                Sorts = ["created_at desc"],
                Limit = 100
            },
            cancellationToken);

        return Normalize(audits.FirstOrDefault(IsContractCloseAudit)?.Who);
    }

    private static bool IsContractCloseAudit(AuditRecord audit)
    {
        var field = audit.Field?.Trim();
        if (string.Equals(field, "status", StringComparison.OrdinalIgnoreCase)
            || string.Equals(field, "статус", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(audit.After?.Trim(), "5", StringComparison.OrdinalIgnoreCase)
                || string.Equals(audit.After?.Trim(), "closed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(audit.After?.Trim(), "Закрыт", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
