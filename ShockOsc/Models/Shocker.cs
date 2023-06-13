namespace ShockLink.ShockOsc.Models;

public class Shocker
{
    public DateTime LastActive { get; set; }
    public DateTime LastExecuted { get; set; }
    public DateTime LastVibration { get; set; }
    public uint LastDuration { get; set; }
    public float LastIntensity { get; set; }
    public float LastStretchValue { get; set; }
    public bool IsGrabbed { get; set; }
    public bool HasCooldownParam { get; set; }
    public bool HasActiveParam { get; set; }
    public bool HasIntensityParam { get; set; }
    public Guid Id { get; }
    public TriggerMethod TriggerMethod { get; set; }

    public Shocker(Guid id)
    {
        Id = id;
    }
}