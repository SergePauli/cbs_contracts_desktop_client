namespace CbsContractsDesktopClient.Models.Contacts
{
    public sealed class ContactTableRow
    {
        public required int Id { get; init; }

        public required string FullName { get; init; }

        public required string CompanyName { get; init; }

        public required string DepartmentName { get; init; }

        public required string Email { get; init; }

        public required string Status { get; init; }
    }
}
