using CbsContractsDesktopClient.Models;

namespace CbsContractsDesktopClient.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(string username, string password);
        void Logout();
    }
}
