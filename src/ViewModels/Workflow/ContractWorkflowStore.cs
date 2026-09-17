using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class ContractWorkflowStore : ObservableObject
    {
        public event EventHandler? SelectionApplied;
        public event EventHandler? DetailDataApplied;

        private readonly HashSet<string> _deferredPropertyNames = [];
        private bool _isApplyingDetailData;

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

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (_isApplyingDetailData && e.PropertyName is not null)
            {
                _deferredPropertyNames.Add(e.PropertyName);
                return;
            }

            base.OnPropertyChanged(e);
        }

        private void BeginDetailDataApplication()
        {
            if (_isApplyingDetailData)
            {
                throw new InvalidOperationException("ContractWorkflowStore detail data application is already in progress.");
            }

            _deferredPropertyNames.Clear();
            _isApplyingDetailData = true;
        }

        private void CompleteDetailDataApplication()
        {
            _isApplyingDetailData = false;
            foreach (var propertyName in _deferredPropertyNames)
            {
                base.OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
            }

            _deferredPropertyNames.Clear();
            DetailDataApplied?.Invoke(this, EventArgs.Empty);
        }

        private void CancelDetailDataApplication()
        {
            _isApplyingDetailData = false;
            _deferredPropertyNames.Clear();
        }
    }
}
