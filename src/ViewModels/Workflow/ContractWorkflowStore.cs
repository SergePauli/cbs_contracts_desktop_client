using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class ContractWorkflowStore : ObservableObject
    {
        [ObservableProperty]
        public partial TableDataRow? SelectedRevision { get; set; }

        [ObservableProperty]
        public partial TableDataRow? SelectedStage { get; set; }

        [ObservableProperty]
        public partial TableDataRow? Contract { get; set; }

        [ObservableProperty]
        public partial TableDataRow? Contragent { get; set; }

        [ObservableProperty]
        public partial StageEditState? SelectedStageEditState { get; set; }

        [ObservableProperty]
        public partial ContractEditState? SelectedContractEditState { get; set; }

        [ObservableProperty]
        public partial int? FocusedRevisionPriority { get; set; }

        [ObservableProperty]
        public partial string SelectedRowHeader { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SelectedFooterText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial IReadOnlyList<TableDataRow> Comments { get; set; } = [];
    }
}
