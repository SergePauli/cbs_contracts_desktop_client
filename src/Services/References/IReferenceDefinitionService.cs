using CbsContractsDesktopClient.Models.References;

namespace CbsContractsDesktopClient.Services.References
{
    public interface IReferenceDefinitionService
    {
        bool TryGetByRoute(string? route, out ReferenceDefinition definition);
    }
}
