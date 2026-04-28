using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Models.References
{
    public sealed class EmployeeEditDialogState
    {
        public required ReferenceDefinition Definition { get; init; }

        public required bool IsCreateMode { get; init; }

        public long? Id { get; init; }

        public string? ListKey { get; init; }

        public long? PersonId { get; init; }

        public string PersonName { get; init; } = string.Empty;

        public long? PositionId { get; init; }

        public string PositionName { get; init; } = string.Empty;

        public long? ContragentId { get; init; }

        public string ContragentName { get; init; } = string.Empty;

        public bool IsUsed { get; init; } = true;

        public int? Priority { get; init; }

        public string Description { get; init; } = string.Empty;

        public IReadOnlyList<EmployeeContactEditItem> Contacts { get; init; } = [];

        public string ContactsText { get; init; } = string.Empty;

        public CbsTableFilterOptionDefinition? InitialPositionOption => PositionId.HasValue && !string.IsNullOrWhiteSpace(PositionName)
            ? new CbsTableFilterOptionDefinition
            {
                Value = PositionId.Value,
                Label = PositionName
            }
            : null;

        public CbsTableFilterOptionDefinition? InitialContragentOption => ContragentId.HasValue && !string.IsNullOrWhiteSpace(ContragentName)
            ? new CbsTableFilterOptionDefinition
            {
                Value = ContragentId.Value,
                Label = ContragentName
            }
            : null;
    }
}
