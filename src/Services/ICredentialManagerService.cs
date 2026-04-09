namespace CbsContractsDesktopClient.Services
{
    public interface ICredentialManagerService
    {
        SavedCredentials? TryGetCredentials();
        void SaveCredentials(string username, string password);
        void DeleteCredentials();
    }
}
