// ReSharper disable InconsistentNaming
namespace OpenShock.ShockOSC.Config;

public sealed class MedalIcymi
{
    public bool Enabled { get; set; } = false;
    public string Name { get; set; } = "ShockOSC";
    public string Description { get; set; } = "ShockOSC activated.";
    public int ClipDuration { get; set; } = 30;
    public IcymiAlertType AlertType { get; set; } = IcymiAlertType.Default;
    public IcymiTriggerAction TriggerAction { get; set; } = IcymiTriggerAction.SaveClip;
    public IcymiGame Game { get; set; } = IcymiGame.VRChat;
}

public enum IcymiTriggerAction
{
    SaveClip
}

public enum IcymiAlertType
{
    Default,
    Disabled,
    SoundOnly,
    OverlayOnly
}

public enum IcymiGame
{
    VRChat,
    ChilloutVR
}