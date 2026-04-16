using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CbsContractsDesktopClient.Models.Data
{
    public sealed class DataQueryRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("preset")]
        public string? Preset { get; init; }

        [JsonPropertyName("filters")]
        public object? Filters { get; init; }

        [JsonPropertyName("sorts")]
        public IReadOnlyList<string>? Sorts { get; init; }

        [JsonPropertyName("limit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Limit { get; init; }

        [JsonPropertyName("offset")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Offset { get; init; }
    }
}
