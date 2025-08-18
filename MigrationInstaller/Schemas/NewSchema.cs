using System.Net;
using System.Text.Json.Serialization;

namespace OpenShock.ShockOSC.MigrationInstaller.Schemas;

// Minimal new schema representations for serialization
public static class NewSchema
{
    public sealed class ShockOscConfig
    {
        public OscConf Osc { get; set; } = new();
        public BehaviourConf Behaviour { get; set; } = new();
        public ChatboxConf Chatbox { get; set; } = new();
        public IDictionary<Guid, Group> Groups { get; set; } = new Dictionary<Guid, Group>();
    }

    public sealed class OscConf
    {
        public bool Hoscy { get; set; } = false;
        public ushort HoscySendPort { get; set; } = 9001;
        public bool QuestSupport { get; set; } = false;
        public bool OscQuery { get; set; } = true;
        public ushort OscSendPort { get; set; } = 9000;
        public ushort OscReceivePort { get; set; } = 9001;
    
        public string OscSendIp { get; set; } = IPAddress.Loopback.ToString();
    }

    public enum BoneAction
    {
        None = 0,
        Shock = 1,
        Vibrate = 2,
        Sound = 3
    }

    public enum ControlType
    {
        Stop = 0,
        Shock = 1,
        Vibrate = 2,
        Sound = 3
    }

    public sealed class JsonRange<T> where T : struct
    {
        public T Min { get; set; }
        public T Max { get; set; }
    }

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

        public uint? BoneHeldDurationLimit { get; set; }
    }

    public sealed class BehaviourConf : SharedBehaviourConfig
    {
        public uint HoldTime { get; set; } = 250;
        public bool DisableWhileAfk { get; set; } = true;
        public bool ForceUnmute { get; set; }
    }

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

    public sealed class ChatboxConf
    {
        public bool Enabled { get; set; } = true;
        public string Prefix { get; set; } = "[ShockOSC] ";
        public bool DisplayRemoteControl { get; set; } = true;
        public bool TimeoutEnabled { get; set; } = true;
        public uint Timeout { get; set; } = 5000;

        public HoscyMessageType HoscyType { get; set; } = HoscyMessageType.Message;
        public string IgnoredKillSwitchActive { get; set; } = "Ignoring Shock, kill switch is active";
        public string IgnoredGroupPauseActive { get; set; } = "Ignoring Shock, {GroupName} is paused"; // new field
        public string IgnoredAfk { get; set; } = "Ignoring Shock, user is afk";

        public IDictionary<ControlType, ControlTypeConf> Types { get; set; } =
            new Dictionary<ControlType, ControlTypeConf>
            {
                {
                    ControlType.Stop, new ControlTypeConf
                    {
                        Enabled = true,
                        Local = "⏸ '{GroupName}'",
                        Remote = "⏸ '{ShockerName}' by {Name}",
                        RemoteWithCustomName = "⏸ '{ShockerName}' by {CustomName} [{Name}]"
                    }
                },
                {
                    ControlType.Shock, new ControlTypeConf
                    {
                        Enabled = true,
                        Local = "⚡ '{GroupName}' {Intensity}%:{DurationSeconds}s",
                        Remote = "⚡ '{ShockerName}' {Intensity}%:{DurationSeconds}s by {Name}",
                        RemoteWithCustomName =
                            "⚡ '{ShockerName}' {Intensity}%:{DurationSeconds}s by {CustomName} [{Name}]"
                    }
                },
                {
                    ControlType.Vibrate, new ControlTypeConf
                    {
                        Enabled = true,
                        Local = "〜 '{GroupName}' {Intensity}%:{DurationSeconds}s",
                        Remote = "〜 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {Name}",
                        RemoteWithCustomName =
                            "〜 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {CustomName} [{Name}]"
                    }
                },
                {
                    ControlType.Sound, new ControlTypeConf
                    {
                        Enabled = true,
                        Local = "🔈 '{GroupName}' {Intensity}%:{DurationSeconds}s",
                        Remote = "🔈 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {Name}",
                        RemoteWithCustomName =
                            "🔈 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {CustomName} [{Name}]"
                    }
                }
            };
        public sealed class ControlTypeConf
        {
            public bool Enabled { get; set; }
            public string Local { get; set; } = string.Empty;
            public string Remote { get; set; } = string.Empty;
            public string RemoteWithCustomName { get; set; } = string.Empty;
        }

        public enum HoscyMessageType
        {
            Message,
            Notification
        }
    }
}