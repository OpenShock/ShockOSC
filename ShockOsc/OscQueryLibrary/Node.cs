using System.Text.Json.Serialization;

namespace OpenShock.ShockOsc.OscQueryLibrary;

// technically every class in the JSON is this "Node" class but that's gross
public class Node
{
    [JsonPropertyOrder(-4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("DESCRIPTION")]
    public string? Description { get; set; }
        
    [JsonPropertyOrder(-3)]
    [JsonPropertyName("FULL_PATH")]
    public required string FullPath { get; set; }
        
    [JsonPropertyOrder(-2)]
    [JsonPropertyName("ACCESS")]
    public required int Access { get; set; }
}
    
public class Node<T> : Node
{
    [JsonPropertyOrder(-1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("CONTENTS")]
    public T? Contents { get; set; }
}