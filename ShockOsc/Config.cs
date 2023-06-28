using System.Text.Json;
using Serilog;
using ShockLink.ShockOsc.Models;

namespace ShockLink.ShockOsc;

public static class Config
{
    private static readonly ILogger Logger = Log.ForContext(typeof(Config));
    private static Conf? _internalConfig;
    private static readonly string Path = Directory.GetCurrentDirectory() + "/config.json";
    public static Conf ConfigInstance
    {
        get
        {
            TryLoad();
            return _internalConfig!;
        }
    }

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
                    _internalConfig = JsonSerializer.Deserialize<Conf>(json);
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
        Logger.Information("No valid config file found, generating new one at {Path}", Path);
        _internalConfig = GetDefaultConfig();
        Save();
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
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
            ReceivePort = 9001,
            SendPort = 9000,
            HoscySendPort = 9001
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
            VibrateWhileBoneHeld = true,
            DisableWhileAfk = true,
            ForceUnmute = false
        },
        ShockLink = new Conf.ShockLinkConf
        {
            Shockers = new Dictionary<string, Guid>(),
            UserHub = new Uri("https://api.shocklink.net/1/hubs/user"),
            ApiToken = "SET THIS TO YOUR SHOCKLINK API TOKEN",
            ChatboxRemoteControls = true
        }
    };

    public class Conf
    {
        public required OscConf Osc { get; set; }
        public required BehaviourConf Behaviour { get; set; }
        public required ShockLinkConf ShockLink { get; set; }
        public Version? LastIgnoredVersion { get; set; }

        public class OscConf
        {
            public required bool Chatbox { get; set; }
            public required bool Hoscy { get; set; }
            public required uint ReceivePort { get; set; }
            public required uint SendPort { get; set; }
            public uint HoscySendPort { get; set; } = 9001;
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
            public bool VibrateWhileBoneHeld { get; set; } = true;
            public bool DisableWhileAfk { get; set; } = true;
            public bool ForceUnmute { get; set; } = false;
        }

        public class ShockLinkConf
        {
            public Uri UserHub { get; set; } = new("https://api.shocklink.net/1/hubs/user");
            public required string ApiToken { get; set; }
            public required IReadOnlyDictionary<string, Guid> Shockers { get; set; }
            public bool ChatboxRemoteControls { get; set; } = true;
        }
    }
}