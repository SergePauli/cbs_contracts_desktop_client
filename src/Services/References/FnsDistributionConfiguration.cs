using System.Reflection;
using System.Text;

namespace CbsContractsDesktopClient.Services.References
{
    internal static class FnsDistributionConfiguration
    {
        private const string ApiKeyMetadataName = "FnsDistributionApiKeyBase64";

        public static string? ApiKey { get; } = ReadApiKey();

        private static string? ReadApiKey()
        {
            var encodedApiKey = typeof(FnsDistributionConfiguration)
                .Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .SingleOrDefault(static attribute => attribute.Key == ApiKeyMetadataName)
                ?.Value;

            return string.IsNullOrWhiteSpace(encodedApiKey)
                ? null
                : Encoding.UTF8.GetString(Convert.FromBase64String(encodedApiKey));
        }
    }
}
