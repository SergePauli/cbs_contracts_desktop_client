using System.Globalization;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Models.References
{
    public sealed class ContragentEditDialogState
    {
        public required ReferenceDefinition Definition { get; init; }

        public required bool IsCreateMode { get; init; }

        public long? Id { get; init; }

        public string ObjUuid { get; init; } = string.Empty;

        public long? RequisitesId { get; init; }

        public string RequisitesListKey { get; init; } = string.Empty;

        public long? OrganizationId { get; init; }

        public string Inn { get; init; } = string.Empty;

        public string Kpp { get; init; } = string.Empty;

        public string Division { get; init; } = string.Empty;

        public long? OwnershipId { get; init; }

        public string OwnershipName { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public long? RegionId { get; init; }

        public string RegionName { get; init; } = string.Empty;

        public long? RealAddressId { get; init; }

        public string RealAddressListKey { get; init; } = string.Empty;

        public long? AddressRealAddressId { get; init; }

        public string AddressReal { get; init; } = string.Empty;

        public string FullName { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string Ogrn { get; init; } = string.Empty;

        public string Okfc { get; init; } = string.Empty;

        public string Okopf { get; init; } = string.Empty;

        public string Okpo { get; init; } = string.Empty;

        public string Okogu { get; init; } = string.Empty;

        public string Okved { get; init; } = string.Empty;

        public string Oktmo { get; init; } = string.Empty;

        public string BankName { get; init; } = string.Empty;

        public string BankBik { get; init; } = string.Empty;

        public string BankAccount { get; init; } = string.Empty;

        public string BankCorAccount { get; init; } = string.Empty;

        public IReadOnlyList<EmployeeContactEditItem> Contacts { get; init; } = [];

        public string ContactsText { get; init; } = string.Empty;

        public IReadOnlyList<ContragentOrganizationHistoryItem> OrganizationHistory { get; init; } = [];

        public IReadOnlyList<CbsTableFilterOptionDefinition> OwnershipOptions { get; init; } = [];

        public IReadOnlyList<CbsTableFilterOptionDefinition> RegionOptions { get; init; } = [];

        public CbsTableFilterOptionDefinition? InitialAddressOption { get; init; }
    }

    public sealed class ContragentOrganizationHistoryItem
    {
        public long? Id { get; init; }

        public long? OrganizationId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string FullName { get; init; } = string.Empty;

        public string Inn { get; init; } = string.Empty;

        public string Kpp { get; init; } = string.Empty;

        public string Division { get; init; } = string.Empty;

        public string OwnershipName { get; init; } = string.Empty;

        public long? OwnershipId { get; init; }

        public string OwnershipCode { get; init; } = string.Empty;

        public string Ogrn { get; init; } = string.Empty;

        public string Okfc { get; init; } = string.Empty;

        public string Okopf { get; init; } = string.Empty;

        public string Okpo { get; init; } = string.Empty;

        public string Okogu { get; init; } = string.Empty;

        public string Okved { get; init; } = string.Empty;

        public string Oktmo { get; init; } = string.Empty;

        public string ListKey { get; init; } = string.Empty;

        public string CreatedAt { get; init; } = string.Empty;

        public string UpdatedAt { get; init; } = string.Empty;

        public bool OriginalIsActive { get; init; }

        public bool IsActive { get; set; }

        public bool IsMarkedForDestroy { get; set; }

        public string StatusText => IsActive ? "Активна" : "Архив";

        public string PrimaryText => !string.IsNullOrWhiteSpace(Name)
            ? Name
            : !string.IsNullOrWhiteSpace(FullName)
                ? FullName
                : "Регистрация";

        public string RequisitesText
        {
            get
            {
                var parts = new[]
                {
                    FormatPart("ИНН", Inn),
                    FormatPart("КПП", Kpp),
                    FormatPart("КодПодр.", Division),
                    FormatPart("Форма", OwnershipName),
                    FormatPart("ОКОПФ", OwnershipCode)
                };

                return string.Join("; ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
            }
        }

        public string PeriodText
        {
            get
            {
                var parts = new[]
                {
                    FormatPart("внесено", FormatDateTime(CreatedAt)),
                    FormatPart("обновлено", FormatDateTime(UpdatedAt))
                };

                return string.Join("; ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
            }
        }

        private static string? FormatPart(string label, string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : $"{label}: {value}";
        }

        private static string FormatDateTime(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out var timestamp)
                ? timestamp.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                : value;
        }
    }
}
