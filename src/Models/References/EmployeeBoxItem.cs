namespace CbsContractsDesktopClient.Models.References
{
    public sealed class EmployeeBoxItem
    {
        public long? Id { get; init; }

        public string FullName { get; init; } = string.Empty;

        public string Position { get; init; } = string.Empty;

        public IReadOnlyList<string> Contacts { get; init; } = [];

        public string Description { get; init; } = string.Empty;

        public bool IsActive { get; init; } = true;

        public string ContactsSummary => Contacts.Count == 0
            ? string.Empty
            : string.Join(", ", Contacts);

        public string StatusText => IsActive ? "активен" : "неактивен";

        public string CopyText
        {
            get
            {
                var name = string.IsNullOrWhiteSpace(FullName) ? "Сотрудник" : FullName;
                var position = string.IsNullOrWhiteSpace(Position) ? string.Empty : $" - {Position}";
                var contacts = string.IsNullOrWhiteSpace(ContactsSummary) ? string.Empty : $": {ContactsSummary}";
                return $"{name}{position}{contacts}";
            }
        }
    }
}
