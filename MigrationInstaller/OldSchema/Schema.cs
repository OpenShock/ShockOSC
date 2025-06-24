using System.Text.Json.Serialization;
using Semver;

namespace OpenShock.ShockOSC.MigrationInstaller.OldSchema;

public sealed class ShockOscConfig
{
    public OscConf Osc { get; set; } = new();
    public BehaviourConf Behaviour { get; set; } = new();
    public OpenShockConf OpenShock { get; set; } = new();
    public ChatboxConf Chatbox { get; set; } = new();
    public IDictionary<Guid, Group> Groups { get; set; } = new Dictionary<Guid, Group>();
    
    public AppConfig App { get; set; } = new();
}

public sealed class AppConfig
{
    public bool CloseToTray { get; set; } = true;
    public bool DiscordPreview { get; set; } = false;
    
    public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Release;
    public SemVersion? LastIgnoredVersion { get; set; } = null;
}

public enum UpdateChannel
{
    Release,
    PreRelease
}


public sealed class BehaviourConf : SharedBehaviourConfig
{
    public uint HoldTime { get; set; } = 250;
    public bool DisableWhileAfk { get; set; } = true;
    public bool ForceUnmute { get; set; }
}

public enum BoneAction
{
    None = 0,
    Shock = 1,
    Vibrate = 2,
    Sound = 3
}

public class JsonRange<T> where T : struct
{
    public required T Min { get; set; }
    public required T Max { get; set; }
}

public sealed class OpenShockConf
{
    public Uri Backend { get; set; } = new("https://api.openshock.app");
    public string Token { get; set; } = "";
    public IReadOnlyDictionary<Guid, ShockerConf> Shockers { get; set; } = new Dictionary<Guid, ShockerConf>();

    public sealed class ShockerConf
    {
        public bool Enabled { get; set; } = true;
    }
}

public sealed class OscConf
{
    public bool Hoscy { get; set; } = false;
    public ushort HoscySendPort { get; set; } = 9001;
    public bool QuestSupport { get; set; } = false;
    public bool OscQuery { get; set; } = true;
    public ushort OscSendPort { get; set; } = 9000;
    public ushort OscReceivePort { get; set; } = 9001;
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

    public uint? BoneHeldDurationLimit { get; set; } = null;
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

    [JsonIgnore]
    public TimeSpan TimeoutTimeSpan
    {
        get => TimeSpan.FromMilliseconds(Timeout);
        set => Timeout = (uint)value.TotalMilliseconds;
    } 
    
    public HoscyMessageType HoscyType { get; set; } = HoscyMessageType.Message;

    public string IgnoredKillSwitchActive { get; set; } = "Ignoring Shock, kill switch is active";
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
        public required bool Enabled { get; set; }
        public required string Local { get; set; }
        public required string Remote { get; set; }
        public required string RemoteWithCustomName { get; set; }
    }

    public enum HoscyMessageType
    {
        Message,
        Notification
    }
}