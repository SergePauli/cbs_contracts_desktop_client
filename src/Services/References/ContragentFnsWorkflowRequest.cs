// Carries host UI context and current Contragent identity into FNS workflow calls.
using CbsContractsDesktopClient.Models.References;
using Microsoft.UI.Xaml;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class ContragentFnsWorkflowRequest
    {
        public required XamlRoot XamlRoot { get; init; }

        public required ReferenceDefinition Definition { get; init; }

        public long? SelectedContragentId { get; init; }
    }
}
