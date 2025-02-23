namespace OpenShock.ShockOSC.Config;

public sealed class MedalIcymi
{
    public bool IcymiEnabled { get; set; } = false;
    public string IcymiName { get; set; } = "ShockOSC";
    public string IcymiDescription { get; set; } = "ShockOSC activated.";
    public int IcymiClipDuration { get; set; } = 30;
    public IcymiAlertType IcymiAlertType { get; set; } = IcymiAlertType.Default;
    public IcymiTriggerAction IcymiTriggerAction { get; set; } = IcymiTriggerAction.SaveClip;
    public IcymiGame IcymiGame { get; set; } = IcymiGame.VRChat;
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