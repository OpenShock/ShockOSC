using OpenShock.Desktop.ModuleBase.Models;

namespace OpenShock.ShockOSC.Config;

public sealed class AvatarParameterAction
{
    public required string ParameterName { get; set; }
    public ControlType Action { get; set; } = ControlType.Shock;
    public Guid GroupId { get; set; }
    public ParameterTriggerKind TriggerKind { get; set; } = ParameterTriggerKind.OnTrue;
    public float Threshold { get; set; } = 0.5f;
    public byte? OverrideIntensity { get; set; }
    public ushort? OverrideDuration { get; set; }
}

public enum ParameterTriggerKind
{
    /// <summary>
    /// Triggers when a bool parameter becomes true
    /// </summary>
    OnTrue,

    /// <summary>
    /// Triggers when a bool parameter becomes false
    /// </summary>
    OnFalse,

    /// <summary>
    /// Triggers when a float parameter exceeds the threshold
    /// </summary>
    Threshold,

    /// <summary>
    /// Triggers on any value change (non-default)
    /// </summary>
    OnChange
}
