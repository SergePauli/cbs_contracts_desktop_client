using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Settings;

namespace CbsContractsDesktopClient.Services.Settings
{
    public interface ILocalUserSettingsService
    {
        LocalUserSettings Get();

        Task<LocalUserSettings> GetAsync(CancellationToken cancellationToken = default);

        Task SaveAsync(LocalUserSettings settings, CancellationToken cancellationToken = default);
    }
}
