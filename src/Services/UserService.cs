using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models;

namespace CbsContractsDesktopClient.Services
{
    public partial class UserService : ObservableObject
    {
        [ObservableProperty]
        public partial User? CurrentUser { get; set; }

        public bool IsAuthenticated => CurrentUser != null;

        public void SetCurrentUser(User user)
        {
            CurrentUser = user;
        }

        public void ClearCurrentUser()
        {
            CurrentUser = null;
        }

        public bool HasRole(string role)
        {
            return CurrentUser?.Role?.Equals(role, StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
