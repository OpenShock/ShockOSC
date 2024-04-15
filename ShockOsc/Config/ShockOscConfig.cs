﻿namespace OpenShock.ShockOsc.Config;

public sealed class ShockOscConfig
{
    public OscConf Osc { get; set; } = new();
    public BehaviourConf Behaviour { get; set; } = new();
    public OpenShockConf OpenShock { get; set; } = new();
    public ChatboxConf Chatbox { get; set; } = new();
    public IDictionary<Guid, Group> Groups { get; set; } = new Dictionary<Guid, Group>();
    public Version? LastIgnoredVersion { get; set; } = null;
    
    public sealed class Group
    {
        public required string Name { get; set; }
        public IList<Guid> Shockers { get; set; } = new List<Guid>();
    }
}