// Defines the registry boundary for editable reference screen definitions.
using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions
{
    public interface IReferenceDefinitionService
    {
        bool TryGetByRoute(string? route, out ReferenceDefinition definition);
    }
}
