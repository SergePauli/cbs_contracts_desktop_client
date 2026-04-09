using CbsContractsDesktopClient.Models;

namespace CbsContractsDesktopClient.Services
{
    public interface IUserService
    {
        User? CurrentUser { get; set; }
        bool IsAuthenticated { get; }
        void SetCurrentUser(User user);
        void ClearCurrentUser();
        bool HasRole(string role);
    }
}
