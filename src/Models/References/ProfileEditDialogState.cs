using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Models.References
{
    public sealed class ProfileEditDialogState
    {
        public required ReferenceDefinition Definition { get; init; }

        public bool IsCreateMode { get; init; }

        public long? Id { get; init; }

        public string Login { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public string PersonName { get; init; } = string.Empty;

        public string Role { get; init; } = string.Empty;

        public string PositionName { get; init; } = string.Empty;

        public long? PositionId { get; init; }

        public long? DepartmentId { get; init; }

        public string DepartmentName { get; init; } = string.Empty;

        public string Password { get; init; } = string.Empty;

        public bool IsActive { get; init; }

        public string LastLoginText { get; init; } = string.Empty;

        public IReadOnlyList<CbsTableFilterOptionDefinition> DepartmentOptions { get; init; } = [];
    }
}
