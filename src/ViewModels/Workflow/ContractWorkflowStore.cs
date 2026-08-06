using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class ContractWorkflowStore : ObservableObject
    {
        public event EventHandler? SelectionApplied;

        private readonly HashSet<string> _deferredPropertyNames = [];
        private bool _isApplyingSelection;

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

        private void NotifySelectionApplied()
        {
            SelectionApplied?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_isApplyingSelection && e.PropertyName is not null)
            {
                _deferredPropertyNames.Add(e.PropertyName);
                return;
            }

            base.OnPropertyChanged(e);
        }

        private void BeginSelectionApplication()
        {
            if (_isApplyingSelection)
            {
                throw new InvalidOperationException("ContractWorkflowStore selection application is already in progress.");
            }

            _deferredPropertyNames.Clear();
            _isApplyingSelection = true;
        }

        private void CompleteSelectionApplication()
        {
            _isApplyingSelection = false;
            foreach (var propertyName in _deferredPropertyNames)
            {
                base.OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            }

            _deferredPropertyNames.Clear();
            NotifySelectionApplied();
        }

        private void CancelSelectionApplication()
        {
            _isApplyingSelection = false;
            _deferredPropertyNames.Clear();
        }
    }
}
