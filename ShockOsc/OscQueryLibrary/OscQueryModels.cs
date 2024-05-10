namespace OpenShock.ShockOsc.OscQueryLibrary;

public class OscQueryModels
{
    public class RootNode
    {
        public SubNode? CONTENTS { get; set; }
    }

    public class SubNode
    {
        public Node input { get; set; }
        public TrackingRootNode tracking { get; set; }
        public Node chatbox { get; set; }
        public AvatarRootNode? avatar { get; set; }
    }

    public class TrackingRootNode
    {
        public string FULL_PATH { get; set; }
        public int ACCESS { get; set; }
        public TrackingNode CONTENTS { get; set; }
    }

    public class TrackingNode
    {
        public Node trackers { get; set; }
        public Node eye { get; set; }
        public Node vrsystem { get; set; }
    }

    public class AvatarRootNode
    {
        public string FULL_PATH { get; set; }
        public int ACCESS { get; set; }
        public AvatarNode CONTENTS { get; set; }
    }

    public class AvatarNode
    {
        public Node change { get; set; }
        public Node? parameters { get; set; }
    }

    // technically every class in the JSON is this "Node" class but that's gross
    public class Node
    {
        public string? DESCRIPTION { get; set; }
        public string FULL_PATH { get; set; }
        public int ACCESS { get; set; }
        public Dictionary<string, Node>? CONTENTS { get; set; }
        public string? TYPE { get; set; }
        public List<object>? VALUE { get; set; }
    }

    public class HostInfo
    {
        public string NAME { get; set; }
        public string OSC_IP { get; set; }
        public int OSC_PORT { get; set; }
        public string OSC_TRANSPORT { get; set; }
        public Extensions EXTENSIONS { get; set; }
    }
    
    public class Extensions
    {
        public bool ACCESS { get; set; }
        public bool CLIPMODE { get; set; }
        public bool RANGE { get; set; }
        public bool TYPE { get; set; }
        public bool VALUE { get; set; }
    }
}