using System.Text.Json.Serialization;

namespace CbsContractsDesktopClient.Models.Shell
{
    public sealed class AuditRecord
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("action")]
        public string? Action { get; init; }

        [JsonPropertyName("when")]
        public string? When { get; init; }

        [JsonPropertyName("where")]
        public string? Where { get; init; }

        [JsonPropertyName("what")]
        public string? What { get; init; }

        [JsonPropertyName("detail")]
        public string? Detail { get; init; }

        [JsonPropertyName("field")]
        public string? Field { get; init; }

        [JsonPropertyName("before")]
        public string? Before { get; init; }

        [JsonPropertyName("after")]
        public string? After { get; init; }

        [JsonPropertyName("who")]
        public string? Who { get; init; }
    }
}
