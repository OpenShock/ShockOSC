using OpenShock.ShockOsc.OscChangeTracker;

namespace OpenShock.ShockOsc.Models;

public sealed class ProgramGroup
{
    public DateTime LastActive { get; set; }
    public DateTime LastExecuted { get; set; }
    public DateTime LastVibration { get; set; }
    public uint LastDuration { get; set; }
    public byte LastIntensity { get; set; }
    public float LastStretchValue { get; set; }
    public bool IsGrabbed { get; set; }
    
    public ChangeTrackedOscParam<bool> ParamActive { get; }
    public ChangeTrackedOscParam<bool> ParamCooldown { get; }
    public ChangeTrackedOscParam<float> ParamCooldownPercentage { get; }
    public ChangeTrackedOscParam<float> ParamIntensity { get; }
    
    
    public Guid Id { get; }
    public string Name { get; }
    public TriggerMethod TriggerMethod { get; set; }

    public ProgramGroup(Guid id, string name, OscClient oscClient)
    {
        Id = id;
        Name = name;

        ParamActive = new ChangeTrackedOscParam<bool>(Name, "_Active", false, oscClient);
        ParamCooldown = new ChangeTrackedOscParam<bool>(Name, "_Cooldown", false, oscClient);
        ParamCooldownPercentage = new ChangeTrackedOscParam<float>(Name, "_CooldownPercentage", 0f, oscClient);
        ParamIntensity = new ChangeTrackedOscParam<float>(Name, "_Intensity", 0f, oscClient);
    }

    public void Reset()
    {
        IsGrabbed = false;
        LastStretchValue = 0;
    }
}