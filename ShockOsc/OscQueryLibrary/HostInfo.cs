using System.Net;
using System.Text.Json.Serialization;
using OpenShock.ShockOsc.Utils;

// ReSharper disable InconsistentNaming

namespace OpenShock.ShockOsc.OscQueryLibrary;

public sealed class HostInfo
{
    [JsonPropertyName("NAME")]
    public required string Name { get; set; }
        
    [JsonPropertyName("OSC_IP")]
    [JsonConverter(typeof(JsonIPAddressConverter))]
    public required IPAddress OscIp { get; set; }
        
    [JsonPropertyName("OSC_PORT")]
    public required ushort OscPort { get; set; }
        
    [JsonPropertyName("OSC_TRANSPORT")]
    [JsonConverter(typeof(JsonStringEnumConverter<OscTransportType>))]
    public required OscTransportType OscTransport { get; set; }
        
    [JsonPropertyName("EXTENSIONS")]
    public required ExtensionsNode Extensions { get; set; }
    
    public enum OscTransportType
    {
        TCP,
        UDP
    }
    
    public sealed class ExtensionsNode
    {
        [JsonPropertyName("ACCESS")]
        public required bool Access { get; set; }
        
        [JsonPropertyName("CLIPMODE")]
        public required bool ClipMode { get; set; }
        
        [JsonPropertyName("RANGE")]
        public required bool Range { get; set; }
        
        [JsonPropertyName("TYPE")]
        public required bool Type { get; set; }
        
        [JsonPropertyName("VALUE")]
        public required bool Value { get; set; }
    }
}