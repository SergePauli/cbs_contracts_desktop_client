// Carries the saved Contragent row so host views can refresh their table state.
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class ContragentFnsWorkflowResult
    {
        public required ReferenceDefinition Definition { get; init; }

        public required bool IsCreateMode { get; init; }

        public required TableDataRow SavedRow { get; init; }

        public required string SuccessTitle { get; init; }
    }
}
