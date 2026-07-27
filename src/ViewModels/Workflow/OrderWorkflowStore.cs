using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Table;

namespace CbsContractsDesktopClient.ViewModels.Workflow
{
    public partial class OrderWorkflowStore : ObservableObject
    {
        [ObservableProperty]
        public partial TableDataRow? OrderCard { get; private set; }

        public void SetOrderCard(TableDataRow orderCard)
        {
            OrderCard = orderCard;
        }

        public void Clear()
        {
            OrderCard = null;
        }
    }
}
