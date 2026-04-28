namespace CbsContractsDesktopClient.Models.References
{
    public sealed class EmployeeContactEditItem
    {
        public long? Id { get; init; }

        public string? ListKey { get; init; }

        public string Value { get; init; } = string.Empty;

        public string Type { get; init; } = "Email";
    }
}
