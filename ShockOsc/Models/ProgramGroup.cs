using OpenShock.SDK.CSharp.Models;
using OpenShock.ShockOsc.Config;
using OpenShock.ShockOsc.OscChangeTracker;
using OpenShock.ShockOsc.Services;

namespace OpenShock.ShockOsc.Models;

public sealed class ProgramGroup
{
    public DateTime LastActive { get; set; }
    public DateTime LastExecuted { get; set; }
    public DateTime LastVibration { get; set; }
    public DateTime PhysBoneGrabLimitTime { get; set; }
    public ushort LastDuration { get; set; }
    public byte LastIntensity { get; set; }
    public float LastStretchValue { get; set; }
    public bool IsGrabbed { get; set; }

    /// <summary>
    ///  Scaled to 0-100
    /// </summary>
    public byte NextIntensity { get; set; } = 0;
    
    /// <summary>
    /// Not scaled, 0-1 float, needs to be scaled to duration limits
    /// </summary>
    public float NextDuration { get; set; } = 0;
    
    public ChangeTrackedOscParam<bool> ParamActive { get; }
    public ChangeTrackedOscParam<bool> ParamCooldown { get; }
    public ChangeTrackedOscParam<float> ParamCooldownPercentage { get; }
    public ChangeTrackedOscParam<float> ParamIntensity { get; }
    
    public byte LastConcurrentIntensity { get; set; } = 0;
    public byte ConcurrentIntensity { get; set; } = 0;
    public ControlType ConcurrentType { get; set; } = ControlType.Stop;
    
    public Guid Id { get; }
    public string Name { get; }
    public TriggerMethod TriggerMethod { get; set; }

    public Group? ConfigGroup { get; }

    public ProgramGroup(Guid id, string name, OscClient oscClient, Group? group)
    {
        Id = id;
        Name = name;
        ConfigGroup = group;

        ParamActive = new ChangeTrackedOscParam<bool>(Name, "_Active", false, oscClient);
        ParamCooldown = new ChangeTrackedOscParam<bool>(Name, "_Cooldown", false, oscClient);
        ParamCooldownPercentage = new ChangeTrackedOscParam<float>(Name, "_CooldownPercentage", 0f, oscClient);
        ParamIntensity = new ChangeTrackedOscParam<float>(Name, "_Intensity", 0f, oscClient);
    }

    public void Reset()
    {
        IsGrabbed = false;
        LastStretchValue = 0;
        ConcurrentType = ControlType.Stop;
        ConcurrentIntensity = 0;
        LastConcurrentIntensity = 0;
        NextIntensity = 0;
        NextDuration = 0;
    }
}