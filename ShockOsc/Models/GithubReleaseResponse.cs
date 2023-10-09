using System.Text.Json.Serialization;

namespace OpenShock.ShockOsc.Models;

public class GithubReleaseResponse
{
    [JsonPropertyName("tag_name")]
    public required string TagName { get; set; }
    [JsonPropertyName("assets")]
    public required ICollection<Asset> Assets { get; set; }

    public class Asset
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }
        [JsonPropertyName("browser_download_url")]
        public required Uri BrowserDownloadUrl { get; set; }
    }
}