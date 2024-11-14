using OpenShock.ShockOsc.Models;

namespace OpenShock.ShockOsc.Config;

public class ConfigUtils
{
    private readonly ConfigManager _configManager;

    public ConfigUtils(ConfigManager configManager)
    {
        _configManager = configManager;
    }

    public T GetGroupOrGlobal<T>(ProgramGroup group, Func<SharedBehaviourConfig, T> selector, Func<Group, bool> groupOverrideSelector)
    {
        if(group.ConfigGroup == null) return selector(_configManager.Config.Behaviour);
        
        var groupOverride = groupOverrideSelector(group.ConfigGroup);
        SharedBehaviourConfig config = groupOverride ? group.ConfigGroup : _configManager.Config.Behaviour;
        return selector(config);    
    }
}