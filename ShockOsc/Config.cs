using System.Text.Json;
using System.Text.Json.Serialization;
using OpenShock.ShockOsc.Models;
using Serilog;

namespace OpenShock.ShockOsc;

public static class Config
{
    private static readonly ILogger Logger = Log.ForContext(typeof(Config));
    private static Conf? _internalConfig;
    private static readonly string Path = Directory.GetCurrentDirectory() + "/config.json";

    public static Conf ConfigInstance => _internalConfig!;

    static Config()
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
                    _internalConfig = JsonSerializer.Deserialize<Conf>(json, Options);
                    Logger.Information("Successfully loaded config");
                }
                catch (JsonException e)
                {
                    Logger.Fatal(e, "Error during deserialization/loading of config");
                    return;
                }
            }
        }

        if (_internalConfig != null) return;
        Logger.Information("No config file found (does not exist or empty), generating new one at {Path}", Path);
        _internalConfig = GetDefaultConfig();
        Save();
        var jsonNew = File.ReadAllText(Path);
        _internalConfig = JsonSerializer.Deserialize<Conf>(jsonNew, Options);
        Logger.Information("New configuration file generated! Please configure it!");
        Logger.Information("Press any key to exit...");
        Console.ReadKey();
        Environment.Exit(10);
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
            File.WriteAllText(Path, JsonSerializer.Serialize(_internalConfig, Options));
        }
        catch (Exception e)
        {
            Logger.Error(e, "Error occurred while saving new config file");
        }
    }

    private static Conf GetDefaultConfig() => new()
    {
        Osc = new Conf.OscConf
        {
            Chatbox = true,
            Hoscy = false,
            SendPort = 9000,
            HoscySendPort = 9001
        },
        Chatbox = new Conf.ChatboxConf
        {
            DisplayRemoteControl = true,
            HoscyType = Conf.ChatboxConf.HoscyMessageType.Message,
            Prefix = null!,
            Types = null!
        },
        Behaviour = new Conf.BehaviourConf
        {
            RandomDuration = true,
            RandomIntensity = true,
            RandomDurationStep = 1000,
            DurationRange = new JsonRange { Min = 1000, Max = 5000 },
            IntensityRange = new JsonRange { Min = 1, Max = 30 },
            FixedDuration = 2000,
            FixedIntensity = 50,
            CooldownTime = 5000,
            HoldTime = 250,
            WhileBoneHeld = Conf.BehaviourConf.BoneHeldAction.Vibrate,
            DisableWhileAfk = true,
            ForceUnmute = false
        },
        ShockLink = new Conf.OpenShockConf
        {
            Shockers = new Dictionary<string, Guid>(),
            UserHub = null!,
            ApiToken = "SET THIS TO YOUR OPENSHOCK API TOKEN",
        }
    };

    public class Conf
    {
        public required OscConf Osc { get; set; }
        public required BehaviourConf Behaviour { get; set; }
        public required OpenShockConf ShockLink { get; set; }
        public ChatboxConf Chatbox { get; set; } = new();
        public Version? LastIgnoredVersion { get; set; }

        public class ChatboxConf
        {
            public string Prefix { get; set; } = "[ShockOsc] ";
            public bool DisplayRemoteControl { get; set; } = true;
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public HoscyMessageType HoscyType { get; set; } = HoscyMessageType.Message;

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
                            RemoteWithCustomName = "⚡ '{ShockerName}' {Intensity}%:{DurationSeconds}s by {CustomName} [{Name}]"
                        }
                    },
                    {
                        ControlType.Vibrate, new ControlTypeConf
                        {
                            Enabled = true,
                            Local = "〜 '{ShockerName}' {Intensity}%:{DurationSeconds}s",
                            Remote = "〜 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {Name}",
                            RemoteWithCustomName = "〜 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {CustomName} [{Name}]"
                        }
                    },
                    {
                        ControlType.Sound, new ControlTypeConf
                        {
                            Enabled = true,
                            Local = "🔈 '{ShockerName}' {Intensity}%:{DurationSeconds}s",
                            Remote = "🔈 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {Name}",
                            RemoteWithCustomName = "🔈 '{ShockerName}' {Intensity}%:{DurationSeconds}s by {CustomName} [{Name}]"
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
            public required bool Chatbox { get; init; }
            public required bool Hoscy { get; init; }
            public required ushort SendPort { get; init; }
            public ushort HoscySendPort { get; init; } = 9001;
        }

        public class BehaviourConf
        {
            public required bool RandomIntensity { get; init; }
            public required bool RandomDuration { get; init; }
            public required uint RandomDurationStep { get; init; } = 1000;
            public required JsonRange DurationRange { get; init; }
            public required JsonRange IntensityRange { get; init; }
            public required byte FixedIntensity { get; init; }
            public required uint FixedDuration { get; init; }
            public required uint HoldTime { get; init; }
            public required uint CooldownTime { get; init; }
            public BoneHeldAction WhileBoneHeld { get; init; } = BoneHeldAction.Vibrate;
            public bool DisableWhileAfk { get; init; } = true;
            public bool ForceUnmute { get; init; } = false;
            
            public enum BoneHeldAction
            {
                Vibrate = 0,
                Shock = 1,
                None = 2
            }
        }

        public class OpenShockConf
        {
            public Uri UserHub { get; init; } = new("https://api.shocklink.net/1/hubs/user");
            public required string ApiToken { get; init; }
            public required IReadOnlyDictionary<string, Guid> Shockers { get; init; }
        }
    }
}