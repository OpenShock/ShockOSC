namespace OpenShock.ShockOSC.Config;

public sealed class Group : SharedBehaviourConfig
{
    public required string Name { get; set; }
    public IList<Guid> Shockers { get; set; } = new List<Guid>();
    public bool OverrideIntensity { get; set; }
    public bool OverrideDuration { get; set; }
    public bool OverrideCooldownTime { get; set; }

    public bool OverrideBoneHeldAction { get; set; }
    public bool OverrideBoneReleasedAction { get; set; }
    
    public bool OverrideBoneHeldDurationLimit { get; set; }
}
