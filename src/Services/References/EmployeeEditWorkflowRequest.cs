// Describes an Employee edit dialog invocation shared by employee-related host views.
using CbsContractsDesktopClient.Models.References;
using Microsoft.UI.Xaml;

namespace CbsContractsDesktopClient.Services.References
{
    public sealed class EmployeeEditWorkflowRequest
    {
        public required XamlRoot XamlRoot { get; init; }

        public bool IsCreateMode { get; init; }

        public long? EmployeeId { get; init; }

        public ReferenceDefinition? Definition { get; init; }

        public EmployeeEditDialogState? InitialState { get; init; }
    }
}
