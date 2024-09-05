namespace OpenShock.ShockOsc.Config;

public sealed class BehaviourConf
{
    public bool RandomIntensity { get; set; }
    public bool RandomDuration { get; set; }
    public JsonRange<ushort> DurationRange { get; set; } = new JsonRange<ushort> { Min = 1000, Max = 5000 };
    public JsonRange<byte> IntensityRange { get; set; } = new JsonRange<byte> { Min = 1, Max = 30 };
    public byte FixedIntensity { get; set; } = 50;
    public ushort FixedDuration { get; set; } = 2000;
    public uint HoldTime { get; set; } = 250;
    public uint CooldownTime { get; set; } = 5000;
    public BoneHeldAction WhileBoneHeld { get; set; } = BoneHeldAction.Vibrate;
    public bool DisableWhileAfk { get; set; } = true;
    public bool ForceUnmute { get; set; }

    public enum BoneHeldAction
    {
        Vibrate = 0,
        Shock = 1,
        None = 2
    }
}