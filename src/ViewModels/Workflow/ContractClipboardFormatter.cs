using System.Globalization;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

namespace CbsContractsDesktopClient.ViewModels.Workflow;

public static class ContractClipboardFormatter
{
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    public static string BuildForContract(
        ContractEditState contract,
        RevisionEditState contractRevision,
        IReadOnlyList<EmployeeBoxItem> employees)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(contractRevision);
        var responsibles = contract.ContractResponsibles.Select(responsible =>
        {
            var contacts = string.Join(", ", employees
                .Where(employee => employee.Id == responsible.EmployeeId)
                .SelectMany(static employee => employee.Contacts));
            return contacts.Length == 0
                ? responsible.FullName
                : $"{responsible.FullName}: {contacts}";
        });
        return $"{BuildContractLine(contract, contractRevision)}{Environment.NewLine}"
            + string.Join(Environment.NewLine, responsibles);
    }

    public static string BuildForStage(
        ContractEditState contract,
        RevisionEditState contractRevision,
        StageEditState stage)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(contractRevision);
        ArgumentNullException.ThrowIfNull(stage);

        return $"{BuildContractLine(contract, contractRevision)}"
            + $" | работы проводятся с {FormatDate(stage.StartAt)} до {FormatDate(stage.DeadlineAt)}"
            + $"{Environment.NewLine}{BuildResponsibleNames(contract)}";
    }

    private static string BuildContractLine(
        ContractEditState contract,
        RevisionEditState contractRevision)
    {
        var identity = string.Join(
            " ",
            new[] { contract.ContragentName, contractRevision.Description, contract.ExternalNumber }
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!.Trim()));
        return contract.SignedAt is DateTimeOffset signedAt
            ? $"{identity} от {FormatDate(signedAt)}"
            : identity;
    }

    private static string BuildResponsibleNames(ContractEditState contract)
    {
        return string.Join(", ", contract.ContractResponsibles.Select(static responsible => responsible.FullName));
    }

    private static string FormatDate(DateTimeOffset? value)
    {
        return value?.ToString("d", RuCulture) ?? string.Empty;
    }
}
