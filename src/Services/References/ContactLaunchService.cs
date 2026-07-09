using Windows.System;

namespace CbsContractsDesktopClient.Services.References
{
    public static class ContactLaunchService
    {
        public static async void Launch(Uri? uri)
        {
            if (uri is null)
            {
                return;
            }

            await Launcher.LaunchUriAsync(uri);
        }
    }
}
