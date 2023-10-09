using System.Text.Json.Serialization;

namespace OpenShock.ShockOsc.Models;

public class AvatarConfigJson
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    [JsonPropertyName("parameters")]
    public required IEnumerable<Parameter> Parameters { get; set; }
    
    public class Parameter
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }
        [JsonPropertyName("input")]
        public InputOutput? Input { get; set; }
        [JsonPropertyName("output")]
        public InputOutput? Output { get; set; }
    }
    
    public class InputOutput
    {
        [JsonPropertyName("address")]
        public required string Address { get; set; }
        [JsonPropertyName("type")]
        public required string Type { get; set; }
    }
}



