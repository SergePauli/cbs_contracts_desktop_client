// Carries the saved Employee row so callers can refresh their table state.
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class EmployeeEditWorkflowResult
    {
        public required ReferenceDefinition Definition { get; init; }

        public required TableDataRow SavedRow { get; init; }
    }
}
