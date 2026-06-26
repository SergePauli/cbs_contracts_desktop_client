namespace CbsContractsDesktopClient.Services
{
    public interface IAccessTokenRefreshService
    {
        Task<string> RefreshAccessTokenAsync(string expiredAccessToken, CancellationToken cancellationToken = default);
    }
}
