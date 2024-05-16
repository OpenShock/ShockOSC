using System.Text.Json.Serialization;

namespace OpenShock.ShockOsc.OscQueryLibrary;

// technically every class in the JSON is this "Node" class but that's gross
public class Node
{
    [JsonPropertyName("DESCRIPTION")]
    public string? Description { get; set; }
        
    [JsonPropertyName("FULL_PATH")]
    public required string FullPath { get; set; }
        
    [JsonPropertyName("ACCESS")]
    public required int Access { get; set; }
}
    
public class Node<T> : Node
{
    [JsonPropertyName("CONTENTS")]
    public T? Contents { get; set; }
}