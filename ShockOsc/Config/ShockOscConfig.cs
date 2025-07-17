using OpenShock.ShockOSC.Models;

namespace OpenShock.ShockOSC.Config;

public sealed class ShockOscConfig
{
    public OscConf Osc { get; set; } = new();
    public BehaviourConf Behaviour { get; set; } = new();
    public ChatboxConf Chatbox { get; set; } = new();
    public IDictionary<Guid, Group> Groups { get; set; } = new Dictionary<Guid, Group>();
    
    public T GetGroupOrGlobal<T>(ProgramGroup group, Func<SharedBehaviourConfig, T> selector, Func<Group, bool> groupOverrideSelector)
    {
        if(group.ConfigGroup == null) return selector(Behaviour);
        
        var groupOverride = groupOverrideSelector(group.ConfigGroup);
        SharedBehaviourConfig config = groupOverride ? group.ConfigGroup : Behaviour;
        return selector(config);    
    }
}