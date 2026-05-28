using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HoloPatcher.UI.Rte
{
    public sealed class RteDocument
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("tags")]
        public Dictionary<string, List<RteRange>> Tags { get; set; } = new Dictionary<string, List<RteRange>>();

        [JsonPropertyName("tag_configs")]
        public Dictionary<string, Dictionary<string, string>> TagConfigs { get; set; } = new Dictionary<string, Dictionary<string, string>>();

        public static RteDocument Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new RteDocument();
            }

            RteDocument document = JsonSerializer.Deserialize<RteDocument>(json, SerializerOptions);
            return document ?? new RteDocument();
        }

        public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true
        };
    }

    public sealed class RteRange
    {
        [JsonPropertyName("0")]
        public string Start { get; set; } = "1.0";

        [JsonPropertyName("1")]
        public string End { get; set; } = "1.0";
    }
}

