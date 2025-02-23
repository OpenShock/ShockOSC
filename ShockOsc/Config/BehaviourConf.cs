namespace OpenShock.ShockOSC.Config;

public sealed class BehaviourConf : SharedBehaviourConfig
{
    public uint HoldTime { get; set; } = 250;
    public bool DisableWhileAfk { get; set; } = true;
    public bool ForceUnmute { get; set; }
}