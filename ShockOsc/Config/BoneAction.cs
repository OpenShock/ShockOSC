using OpenShock.SDK.CSharp.Models;

namespace OpenShock.ShockOSC.Config;

public enum BoneAction
{
    None = 0,
    Shock = 1,
    Vibrate = 2,
    Sound = 3
}

public static class BoneActionExtensions
{
    public static readonly BoneAction[] BoneActions = Enum.GetValues(typeof(BoneAction)).Cast<BoneAction>().ToArray();
    
    public static ControlType ToControlType(this BoneAction action)
    {
        return action switch
        {
            BoneAction.Shock => ControlType.Shock,
            BoneAction.Vibrate => ControlType.Vibrate,
            BoneAction.Sound => ControlType.Sound,
            _ => ControlType.Vibrate
        };
    }
}