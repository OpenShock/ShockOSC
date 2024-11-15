namespace OpenShock.ShockOsc.Config;

public class SharedBehaviourConfig
{
    public bool RandomIntensity { get; set; }
    public bool RandomDuration { get; set; }
    
    public JsonRange<ushort> DurationRange { get; set; } = new JsonRange<ushort> { Min = 1000, Max = 5000 };
    public JsonRange<byte> IntensityRange { get; set; } = new JsonRange<byte> { Min = 1, Max = 30 };
    public byte FixedIntensity { get; set; } = 50;
    public ushort FixedDuration { get; set; } = 2000;
    
    public uint CooldownTime { get; set; } = 5000;
    
    public BoneAction WhileBoneHeld { get; set; } = BoneAction.Vibrate;
    public BoneAction WhenBoneReleased { get; set; } = BoneAction.Shock;

    public uint? BoneHeldDurationLimit { get; set; } = null;
}