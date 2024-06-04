using System.Text.Json.Serialization;
using OpenShock.SDK.CSharp.Models;

namespace OpenShock.ShockOsc.Config;

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