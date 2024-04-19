namespace OpenShock.ShockOsc.Config;

public sealed class Group
{
    public required string Name { get; set; }
    public IList<Guid> Shockers { get; set; } = new List<Guid>();
    
    public bool OverrideIntensity { get; set; }
    
    public bool RandomIntensity { get; set; }
    public JsonRange IntensityRange { get; set; } = new JsonRange { Min = 1, Max = 30 };
    public byte FixedIntensity { get; set; } = 50;
    
    public bool OverrideDuration { get; set; }
    public bool RandomDuration { get; set; }
    public uint RandomDurationStep { get; set; } = 1000;
    public JsonRange DurationRange { get; set; } = new JsonRange { Min = 1000, Max = 5000 };
    public uint FixedDuration { get; set; } = 2000;
    
    public bool OverrideCooldownTime { get; set; }
    public uint CooldownTime { get; set; } = 5000;
}