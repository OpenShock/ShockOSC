using System.Text.Json.Serialization;

namespace OpenShock.ShockOsc.OscQueryLibrary;

public sealed class RootNode : Node<RootNode.RootContents>
{
    public sealed class RootContents
    {
        [JsonPropertyName("avatar")] public Node<AvatarContents>? Avatar { get; set; }
    }
}

public sealed class AvatarContents
{
    [JsonPropertyName("change")] public required OscParameterNodeEnd<string> Change { get; set; }

    [JsonPropertyName("parameters")] public Node<IDictionary<string, OscParameterNode>>? Parameters { get; set; }
}

public sealed class OscParameterNode : Node<IDictionary<string, OscParameterNode>>
{
    [JsonPropertyName("TYPE")] public string? Type { get; set; }

    [JsonPropertyName("VALUE")] public IEnumerable<object>? Value { get; set; }
}

public sealed class OscParameterNodeEnd<T> : Node
{
    [JsonPropertyName("TYPE")] public required string Type { get; set; }

    [JsonPropertyName("VALUE")] public required IEnumerable<T> Value { get; set; }
}