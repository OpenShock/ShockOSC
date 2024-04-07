using System.Text.Json;
using System.Text.Json.Serialization;
using OpenShock.SDK.CSharp.Models;
using OpenShock.ShockOsc.Models;
using Serilog;

namespace OpenShock.ShockOsc;

public static class ShockOscConfigManager
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ShockOscConfigManager));
    private static ShockOscConfig? _internalConfig;
    private static readonly string Path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\ShockOSC\config.json";

    public static ShockOscConfig ConfigInstance => _internalConfig!;

    static ShockOscConfigManager()
    {
        TryLoad();
    }

    private static void TryLoad()
    {
        if (_internalConfig != null) return;
        Logger.Information("Config file found, trying to load config from {Path}", Path);
        if (File.Exists(Path))
        {
            Logger.Verbose("Config file exists");
            var json = File.ReadAllText(Path);
            if (!string.IsNullOrWhiteSpace(json))
            {
                Logger.Verbose("Config file is not empty");
                try
                {
                    _internalConfig = JsonSerializer.Deserialize<ShockOscConfig>(json, Options);
                    Logger.Information("Successfully loaded config");
                }
                catch (JsonException e)
                {
                    Logger.Fatal(e, "Error during deserialization/loading of config");
                    Logger.Warning("Attempting to move old config and generate a new one");
                    File.Move(Path, Path + ".old");
                    Save();
                    json = File.ReadAllText(Path);
                    _internalConfig = JsonSerializer.Deserialize<ShockOscConfig>(json, Options);
                    Logger.Information("Successfully loaded config");
                    return;
                }
            }
        }

        if (_internalConfig != null) return;
        Logger.Information("No config file found (does not exist or empty), generating new one at {Path}", Path);
        _internalConfig = GetDefaultConfig();
        Save();
        var jsonNew = File.ReadAllText(Path);
        _internalConfig = JsonSerializer.Deserialize<ShockOscConfig>(jsonNew, Options);
        Logger.Information("New configuration file generated! Please configure it!");
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Save()
    {
        Logger.Information("Saving config");
        try
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(Path, JsonSerializer.Serialize(_internalConfig, Options));
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error occurred while saving new config file");
        }
    }

    private static ShockOscConfig GetDefaultConfig() => new()
    {
        Osc = new ShockOscConfig.OscConf
        {
            Chatbox = true,
            Hoscy = false,
            HoscySendPort = 9001,
            QuestSupport = false,
            OscQuery = true,
            OscSendPort = 9000,
            OscReceivePort = 9001
        },
        Chatbox = new ShockOscConfig.ChatboxConf
        {
            DisplayRemoteControl = true,
            HoscyType = ShockOscConfig.ChatboxConf.HoscyMessageType.Message,
            Prefix = null!,
            Types = null!,
            IgnoredKillSwitchActive = null!,
            IgnoredAfk = null!
        },
        Behaviour = new ShockOscConfig.BehaviourConf
        {
            RandomDuration = false,
            RandomIntensity = false,
            RandomDurationStep = 1000,
            DurationRange = new JsonRange { Min = 1000, Max = 5000 },
            IntensityRange = new JsonRange { Min = 1, Max = 30 },
            FixedDuration = 2000,
            FixedIntensity = 50,
            CooldownTime = 5000,
            HoldTime = 250,
            WhileBoneHeld = ShockOscConfig.BehaviourConf.BoneHeldAction.Vibrate,
            DisableWhileAfk = true,
            ForceUnmute = false
        },
        OpenShock = new ShockOscConfig.OpenShockConf
        {
            Shockers = new Dictionary<Guid, ShockOscConfig.ShockerConf>(),
            Token = "",
        },
        Groups = new Dictionary<Guid, ShockOscConfig.Group>()
    };

    public class ShockOscConfig
    {
        public required OscConf Osc { get; set; }
        public required BehaviourConf Behaviour { get; set; }
        public required OpenShockConf OpenShock { get; set; }
        public required ChatboxConf Chatbox { get; set; }
        
        public IDictionary<Guid, Group> Groups { get; set; } = new Dictionary<Guid, Group>();
            
        public Version? LastIgnoredVersion { get; set; }
        
        
        public sealed class Group
        {
            public required string Name { get; set; }
            public IList<Guid> Shockers { get; set; } = new List<Guid>();
        }
        

        public class ChatboxConf
        {
            public string Prefix { get; set; } = "[ShockOsc] ";
            public bool DisplayRemoteControl { get; set; } = true;

            [JsonConverter(typeof(JsonStringEnumConverter))]
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
                            Local = "⏸ '{ShockerName}'",
                            Remote = "⏸ '{ShockerName}' by {Name}",
                            RemoteWithCustomName = "⏸ '{ShockerName}' by {CustomName} [{Name}]"
                        }
                    },
                    {
                        ControlType.Shock, new ControlTypeConf
                        {
                            Enabled = true,
                            Local = "⚡ '{ShockerName}' {Intensity}%:{DurationSeconds}s",
                            Remote = "⚡ '{ShockerName}' {Intensity}%:{DurationSeconds}s by {Name}",
                            RemoteWithCustomName =
                                "⚡ '{ShockerName}' {Intensity}%:{DurationSeconds}s by {CustomName} [{Name}]"
                        }
                    },
                    {
                        ControlType.Vibrate, new ControlTypeConf
                        {
                            Enabled = true,
                            Local = "〜 '{ShockerName}' {Intensity}%:{DurationSeconds}s",
                            Remote = "〜 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {Name}",
                            RemoteWithCustomName =
                                "〜 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {CustomName} [{Name}]"
                        }
                    },
                    {
                        ControlType.Sound, new ControlTypeConf
                        {
                            Enabled = true,
                            Local = "🔈 '{ShockerName}' {Intensity}%:{DurationSeconds}s",
                            Remote = "🔈 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {Name}",
                            RemoteWithCustomName =
                                "🔈 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {CustomName} [{Name}]"
                        }
                    }
                };
            
            public class ControlTypeConf
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

        public class OscConf
        {
            public required bool Chatbox { get; set; }
            public required bool Hoscy { get; set; }
            public ushort HoscySendPort { get; set; } = 9001;
            public required bool QuestSupport { get; set; }
            public required bool OscQuery { get; set; }
            public ushort OscSendPort { get; set; } = 9000;
            public ushort OscReceivePort { get; set; } = 9001;
        }

        public class BehaviourConf
        {
            public required bool RandomIntensity { get; set; }
            public required bool RandomDuration { get; set; }
            public required uint RandomDurationStep { get; set; } = 1000;
            public required JsonRange DurationRange { get; set; }
            public required JsonRange IntensityRange { get; set; }
            public required byte FixedIntensity { get; set; }
            public required uint FixedDuration { get; set; }
            public required uint HoldTime { get; set; }
            public required uint CooldownTime { get; set; }
            public BoneHeldAction WhileBoneHeld { get; set; } = BoneHeldAction.Vibrate;
            public bool DisableWhileAfk { get; set; } = true;
            public bool ForceUnmute { get; set; } = false;

            public enum BoneHeldAction
            {
                Vibrate = 0,
                Shock = 1,
                None = 2
            }
        }

        public class OpenShockConf
        {
            public Uri Backend { get; set; } = new("https://api.shocklink.net");
            public required string Token { get; set; }
            public required IReadOnlyDictionary<Guid, ShockerConf> Shockers { get; set; }
        }
        
        public class ShockerConf
        {
            public required bool Enabled { get; set; } = true;
        }
    }
}